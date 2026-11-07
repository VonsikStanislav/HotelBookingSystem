using Npgsql;
using HotelBooking.Server;
using Xunit;

namespace HotelBooking.Tests;

// Эти интеграционные тесты работают с PostgreSQL из Program.cs.
// Тесты в коллекции выполняются последовательно, потому что используют комнату Id=1.
[CollectionDefinition("PostgreSQL tests", DisableParallelization = true)]
public sealed class PostgreSqlTestCollection { }

[Collection("PostgreSQL tests")]
public sealed class PostgreSqlTransactionTests
{
    private const string ConnectionString =
        "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=secret";
    private const int RoomId = 1;
    private const int GuestId = 2;

    private static readonly DateTime NoTransactionIn = new(2098, 1, 10);
    private static readonly DateTime CommitIn = new(2098, 2, 10);
    private static readonly DateTime RollbackIn = new(2098, 3, 10);
    private static readonly DateTime ConcurrentIn = new(2098, 4, 10);

    [Fact]
    public async void WithoutTransaction_ErrorAfterInsert_LeavesPartialChange()
    {
        var guestName = "LAB4_NO_TX_" + Guid.NewGuid().ToString("N");
        using var connection = OpenConnection();
        var originalPlaces = GetAvailablePlaces(connection);
        var bookingId = 0;

        try
        {

            // INSERT выполняется в режиме autocommit: запись сохраняется сразу.
            bookingId = InsertBooking(connection, guestName, NoTransactionIn);

            // Имитируем ошибку между INSERT и UPDATE.
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await Task.Yield();
                throw new InvalidOperationException("Искусственная ошибка после INSERT");
            });

            Assert.True(BookingExists(connection, bookingId));
            Assert.Equal(originalPlaces, GetAvailablePlaces(connection));
            Console.WriteLine("[NO TRANSACTION] INSERT сохранён; UPDATE не выполнен; частичное изменение подтверждено.");
        }
        finally
        {
            if (bookingId != 0)
                DeleteBooking(connection, bookingId);
            SetAvailablePlaces(connection, originalPlaces);
        }
    }

    [Fact]
    public void TransactionWithoutError_Commit_SavesBothChanges()
    {
        using var connection = OpenConnection();
        var originalPlaces = GetAvailablePlaces(connection);
        SetAvailablePlaces(connection, 1);
        var bookingId = 0;

        try
        {
            using var transaction = connection.BeginTransaction();
            bookingId = InsertBooking(connection, "LAB4_COMMIT_" + Guid.NewGuid().ToString("N"), CommitIn, transaction);
            DecrementAvailablePlaces(connection, transaction);
            transaction.Commit();

            Assert.True(BookingExists(connection, bookingId));
            Assert.Equal(0, GetAvailablePlaces(connection));
            Console.WriteLine($"[COMMIT] Booking #{bookingId} сохранён; AvailablePlaces уменьшен; COMMIT выполнен.");
        }
        finally
        {
            if (bookingId != 0)
                DeleteBooking(connection, bookingId);
            SetAvailablePlaces(connection, originalPlaces);
        }
    }

    [Fact]
    public void TransactionWithError_Rollback_RemovesAllChanges()
    {
        using var connection = OpenConnection();
        var originalPlaces = GetAvailablePlaces(connection);
        SetAvailablePlaces(connection, 1);
        var guestName = "LAB4_ROLLBACK_" + Guid.NewGuid().ToString("N");

        try
        {
            using var transaction = connection.BeginTransaction();
            try
            {
                InsertBooking(connection, guestName, RollbackIn, transaction);
                DecrementAvailablePlaces(connection, transaction);

                // Имитируем сбой после обеих SQL-операций, но до COMMIT.
                throw new InvalidOperationException("Искусственная ошибка внутри транзакции");
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Искусственная ошибка"))
            {
                transaction.Rollback();
                Console.WriteLine("[ROLLBACK] Искусственная ошибка; обе операции отменены.");
            }

            Assert.False(BookingExistsByGuestName(connection, guestName));
            Assert.Equal(1, GetAvailablePlaces(connection));
        }
        finally
        {
            DeleteBookingsByGuestName(connection, guestName);
            SetAvailablePlaces(connection, originalPlaces);
        }
    }

    [Fact]
    public async Task TwoConcurrentRequests_OneFreePlace_OnlyOneSucceeds()
    {
        using var connection = OpenConnection();
        var originalPlaces = GetAvailablePlaces(connection);
        SetAvailablePlaces(connection, 1);
        var service = new PostgresBookingService(ConnectionString);

        try
        {
            var first = service.CreateBookingAsync(GuestId, RoomId, ConcurrentIn, ConcurrentIn.AddDays(2));
            var second = service.CreateBookingAsync(GuestId, RoomId, ConcurrentIn, ConcurrentIn.AddDays(2));
            var responses = await Task.WhenAll(first, second);

            Assert.Single(responses.Where(x => x.Success));
            Assert.Single(responses.Where(x => !x.Success));
            Assert.Equal(0, GetAvailablePlaces(connection));
            Assert.Equal(1, CountBookingsForDates(connection, ConcurrentIn, ConcurrentIn.AddDays(2)));
            Console.WriteLine("[CONCURRENCY] Два запроса на один ресурс: один SUCCESS, один FAIL.");
        }
        finally
        {
            DeleteBookingsForDates(connection, ConcurrentIn, ConcurrentIn.AddDays(2));
            SetAvailablePlaces(connection, originalPlaces);
        }
    }

    private static NpgsqlConnection OpenConnection()
    {
        var connection = new NpgsqlConnection(ConnectionString);
        connection.Open();
        return connection;
    }

    private static int GetAvailablePlaces(NpgsqlConnection connection)
    {
        using var cmd = new NpgsqlCommand("SELECT \"AvailablePlaces\" FROM \"Rooms\" WHERE \"Id\"=@id", connection);
        cmd.Parameters.AddWithValue("id", RoomId);
        var value = cmd.ExecuteScalar();
        Assert.NotNull(value);
        return Convert.ToInt32(value);
    }

    private static void SetAvailablePlaces(NpgsqlConnection connection, int value)
    {
        using var cmd = new NpgsqlCommand("UPDATE \"Rooms\" SET \"AvailablePlaces\"=@value WHERE \"Id\"=@id", connection);
        cmd.Parameters.AddWithValue("value", value);
        cmd.Parameters.AddWithValue("id", RoomId);
        Assert.Equal(1, cmd.ExecuteNonQuery());
    }

    private static int InsertBooking(NpgsqlConnection connection, string guestName, DateTime checkIn, NpgsqlTransaction? transaction = null)
    {
        using var cmd = new NpgsqlCommand(
            "INSERT INTO \"Bookings\" (\"RoomId\",\"GuestName\",\"CheckIn\",\"CheckOut\",\"TotalPrice\",\"Status\") " +
            "SELECT @room,@guest,@in,@out,\"PricePerNight\" * (@nights),'Confirmed' FROM \"Rooms\" WHERE \"Id\"=@room RETURNING \"Id\"",
            connection, transaction);
        cmd.Parameters.AddWithValue("room", RoomId);
        cmd.Parameters.AddWithValue("guest", guestName);
        cmd.Parameters.AddWithValue("in", checkIn.Date);
        cmd.Parameters.AddWithValue("out", checkIn.AddDays(2).Date);
        cmd.Parameters.AddWithValue("nights", 2);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    private static void DecrementAvailablePlaces(NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        using var cmd = new NpgsqlCommand("UPDATE \"Rooms\" SET \"AvailablePlaces\"=\"AvailablePlaces\"-1 WHERE \"Id\"=@id AND \"AvailablePlaces\">0", connection, transaction);
        cmd.Parameters.AddWithValue("id", RoomId);
        Assert.Equal(1, cmd.ExecuteNonQuery());
    }

    private static bool BookingExists(NpgsqlConnection connection, int bookingId)
    {
        using var cmd = new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM \"Bookings\" WHERE \"Id\"=@id)", connection);
        cmd.Parameters.AddWithValue("id", bookingId);
        return (bool)cmd.ExecuteScalar()!;
    }

    private static bool BookingExistsByGuestName(NpgsqlConnection connection, string guestName)
    {
        using var cmd = new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM \"Bookings\" WHERE \"GuestName\"=@name)", connection);
        cmd.Parameters.AddWithValue("name", guestName);
        return (bool)cmd.ExecuteScalar()!;
    }

    private static int CountBookingsForDates(NpgsqlConnection connection, DateTime checkIn, DateTime checkOut)
    {
        using var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM \"Bookings\" WHERE \"RoomId\"=@room AND \"CheckIn\"=@in AND \"CheckOut\"=@out", connection);
        cmd.Parameters.AddWithValue("room", RoomId);
        cmd.Parameters.AddWithValue("in", checkIn.Date);
        cmd.Parameters.AddWithValue("out", checkOut.Date);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    private static void DeleteBooking(NpgsqlConnection connection, int bookingId)
    {
        using var cmd = new NpgsqlCommand("DELETE FROM \"Bookings\" WHERE \"Id\"=@id", connection);
        cmd.Parameters.AddWithValue("id", bookingId);
        cmd.ExecuteNonQuery();
    }

    private static void DeleteBookingsByGuestName(NpgsqlConnection connection, string guestName)
    {
        using var cmd = new NpgsqlCommand("DELETE FROM \"Bookings\" WHERE \"GuestName\"=@name", connection);
        cmd.Parameters.AddWithValue("name", guestName);
        cmd.ExecuteNonQuery();
    }

    private static void DeleteBookingsForDates(NpgsqlConnection connection, DateTime checkIn, DateTime checkOut)
    {
        using var cmd = new NpgsqlCommand("DELETE FROM \"Bookings\" WHERE \"RoomId\"=@room AND \"CheckIn\"=@in AND \"CheckOut\"=@out", connection);
        cmd.Parameters.AddWithValue("room", RoomId);
        cmd.Parameters.AddWithValue("in", checkIn.Date);
        cmd.Parameters.AddWithValue("out", checkOut.Date);
        cmd.ExecuteNonQuery();
    }
}
