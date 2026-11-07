using HotelBooking.Server.Events;
using Npgsql;
using HotelBooking.Shared;

namespace HotelBooking.Server;

/// <summary>Хранение и изменение бронирований в PostgreSQL.</summary>
public sealed class PostgresBookingService : IBookingService
{
    private bool _usingReplica;
    private readonly string _connectionString;
    private readonly string? _replicaConnectionString;
    private readonly object _syncRoot = new();
    private readonly BookingEventPublisher _eventPublisher;

    public PostgresBookingService(
    string connectionString,
    string? replicaConnectionString = null)
    : this(
        connectionString,
        new BookingEventPublisher(
            new BookingEventBroker()),
        replicaConnectionString)
{
}

public PostgresBookingService(
    string connectionString,
    BookingEventPublisher eventPublisher,
    string? replicaConnectionString = null)
{
    _connectionString = connectionString;
    _eventPublisher = eventPublisher;
    _replicaConnectionString = replicaConnectionString;
}

    public object SyncRoot => _syncRoot;

    public bool IsPrimaryAvailable() => IsDatabaseAvailable(_connectionString);

    public bool IsReplicaAvailable() =>
        !string.IsNullOrWhiteSpace(_replicaConnectionString)
        && IsDatabaseAvailable(_replicaConnectionString);
    private static bool IsDatabaseAvailable(string connectionString)
    {
        try
        {
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();
            using var command = new NpgsqlCommand("SELECT 1", connection);
            command.ExecuteScalar();
            return true;
        }
        catch (NpgsqlException)
        {
            return false;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }
    private string GetActiveConnectionString()
    {
        if (IsPrimaryAvailable())
        {
            if (_usingReplica)
            {
                Console.WriteLine("[Primary] RECOVERY");
                Console.WriteLine("[SYNC] Начало синхронизации Replica → Primary");

                if (!SynchronizeReplicaToPrimary())
                {
                    throw new InvalidOperationException(
                        "Primary восстановлен, но синхронизация не удалась.");
                }

                _usingReplica = false;
            }

            Console.WriteLine("[Primary] AVAILABLE");
            return _connectionString;
        }

        Console.WriteLine("[Primary] UNAVAILABLE");

        if (IsReplicaAvailable())
        {
            _usingReplica = true;

            Console.WriteLine("[Failover] START");
            Console.WriteLine("[Replica] AVAILABLE");

            return _replicaConnectionString!;
        }

        throw new InvalidOperationException(
            "Primary и Replica недоступны.");
    }

    public async Task<Response> CreateBookingAsync(int guestId, int roomId, DateTime checkIn,
        DateTime checkOut, CancellationToken cancellationToken = default)
    {
        if (checkOut.Date <= checkIn.Date)
            return Response.Fail("Дата выезда должна быть позже даты заезда.");

        // Задержка сохраняет условия эксперимента с двумя конкурентными клиентами.
        await Task.Delay(1500, cancellationToken);

        lock (_syncRoot)
        {
            Log("LOCK", roomId, "Вход в критическую секцию");
            using var connection = new NpgsqlConnection(GetActiveConnectionString());
            connection.Open();
            using var transaction = connection.BeginTransaction();
            try
            {
                using var guestCmd = new NpgsqlCommand("SELECT \"Name\" FROM \"Guests\" WHERE \"Id\"=@id", connection, transaction);
                guestCmd.Parameters.AddWithValue("id", guestId);
                var guestName = guestCmd.ExecuteScalar() as string;
                if (guestName is null) { transaction.Rollback(); Log("FAIL", roomId, $"Гость #{guestId} не найден"); return Response.Fail($"Гость с ID {guestId} не найден."); }

                using var roomCmd = new NpgsqlCommand("SELECT \"AvailablePlaces\", \"PricePerNight\" FROM \"Rooms\" WHERE \"Id\"=@id FOR UPDATE", connection, transaction);
                roomCmd.Parameters.AddWithValue("id", roomId);
                using var roomReader = roomCmd.ExecuteReader();
                if (!roomReader.Read()) { roomReader.Close(); transaction.Rollback(); Log("FAIL", roomId, "Номер не найден"); return Response.Fail($"Номер с ID {roomId} не найден."); }
                var available = roomReader.GetInt32(0);
                var price = roomReader.GetDecimal(1);
                roomReader.Close();
                Log("CHECK", roomId, $"AvailablePlaces = {available}");
                if (available <= 0) { transaction.Rollback(); Log("FAIL", roomId, "Свободных мест нет"); return Response.Fail("Свободных номеров не осталось."); }

                using var overlap = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM \"Bookings\" WHERE \"RoomId\"=@room AND \"Status\" <> 'Cancelled' AND \"CheckIn\" < @out AND \"CheckOut\" > @in)", connection, transaction);
                overlap.Parameters.AddWithValue("room", roomId); overlap.Parameters.AddWithValue("in", checkIn.Date); overlap.Parameters.AddWithValue("out", checkOut.Date);
                if ((bool)overlap.ExecuteScalar()!) { transaction.Rollback(); Log("FAIL", roomId, "Номер занят на эти даты"); return Response.Fail("Номер занят на указанные даты."); }

                var total = (checkOut.Date - checkIn.Date).Days * price;
                using var insert = new NpgsqlCommand("INSERT INTO \"Bookings\" (\"RoomId\",\"GuestName\",\"CheckIn\",\"CheckOut\",\"TotalPrice\",\"Status\") VALUES (@room,@guest,@in,@out,@total,'Confirmed') RETURNING \"Id\"", connection, transaction);
                insert.Parameters.AddWithValue("room", roomId); insert.Parameters.AddWithValue("guest", guestName); insert.Parameters.AddWithValue("in", checkIn.Date); insert.Parameters.AddWithValue("out", checkOut.Date); insert.Parameters.AddWithValue("total", total);
                var bookingId = Convert.ToInt32(insert.ExecuteScalar());

                using var update = new NpgsqlCommand("UPDATE \"Rooms\" SET \"AvailablePlaces\"=\"AvailablePlaces\"-1 WHERE \"Id\"=@id AND \"AvailablePlaces\">0", connection, transaction);
                update.Parameters.AddWithValue("id", roomId);
                if (update.ExecuteNonQuery() != 1) throw new InvalidOperationException("Не удалось обновить доступность номера.");
                transaction.Commit();
                if (IsPrimaryAvailable())
                {
                    ReplicateCreateBooking(
                        bookingId, roomId, guestName, checkIn, checkOut, total);
                }
                else
                {
                    Console.WriteLine(
                        "[Failover] Бронирование выполнено через Replica; репликация пропущена.");
                }
                Log("SUCCESS", roomId, $"Booking #{bookingId}; AvailablePlaces = {available - 1}; COMMIT");
                Log("UNLOCK", roomId, "Выход из критической секции");
                PublishEvent(
                    "BookingCreated",
                    bookingId,
                    new Dictionary<string, object?>
                    {
                        ["GuestId"] = guestId,
                        ["RoomId"] = roomId,
                        ["CheckIn"] = checkIn.Date,
                        ["CheckOut"] = checkOut.Date,
                        ["TotalPrice"] = total
                    });
                return Response.Ok("Бронирование успешно создано в PostgreSQL.", new { bookingId, guestId, guestName, roomId, checkIn = checkIn.Date, checkOut = checkOut.Date, totalPrice = total, availableBefore = available, availableAfter = available - 1 });
            }
            catch (Exception ex)
            {
                try { transaction.Rollback(); } catch { }
                Log("ERROR/ROLLBACK", roomId, ex.Message);
                Log("UNLOCK", roomId, "Выход из критической секции");
                return Response.Fail($"Ошибка создания бронирования: {ex.Message}");
            }
        }
    }

    public Response CheckAvailability(int roomId, DateTime checkIn, DateTime checkOut)
    {
        if (checkOut.Date <= checkIn.Date) return Response.Fail("Дата выезда должна быть позже даты заезда.");
        using var connection = new NpgsqlConnection(GetActiveConnectionString()); connection.Open();
        using var cmd = new NpgsqlCommand("SELECT r.\"Id\", r.\"HotelId\", h.\"Name\", r.\"RoomNumber\", r.\"AvailablePlaces\", r.\"PricePerNight\", NOT EXISTS (SELECT 1 FROM \"Bookings\" b WHERE b.\"RoomId\"=r.\"Id\" AND b.\"Status\" <> 'Cancelled' AND b.\"CheckIn\" < @out AND b.\"CheckOut\" > @in) AS dates_free FROM \"Rooms\" r JOIN \"Hotels\" h ON h.\"Id\"=r.\"HotelId\" WHERE r.\"Id\"=@id", connection);
        cmd.Parameters.AddWithValue("id", roomId); cmd.Parameters.AddWithValue("in", checkIn.Date); cmd.Parameters.AddWithValue("out", checkOut.Date);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return Response.Fail($"Номер с ID {roomId} не найден.");
        var availablePlaces = reader.GetInt32(4); var datesFree = reader.GetBoolean(6); var available = availablePlaces > 0 && datesFree;
        return Response.Ok(available ? "Номер доступен." : "Номер занят или свободных мест нет.", new { roomId, hotelId = reader.GetInt32(1), hotelName = reader.GetString(2), roomNumber = reader.GetString(3), availablePlaces, pricePerNight = reader.GetDecimal(5), available, checkIn = checkIn.Date, checkOut = checkOut.Date });
    }

    public Response CancelBooking(int bookingId)
    {
        using var connection = new NpgsqlConnection(GetActiveConnectionString()); connection.Open(); using var tx = connection.BeginTransaction();
        try
        {
            using var cmd = new NpgsqlCommand("SELECT \"RoomId\",\"Status\" FROM \"Bookings\" WHERE \"Id\"=@id FOR UPDATE", connection, tx); cmd.Parameters.AddWithValue("id", bookingId);
            using var reader = cmd.ExecuteReader(); if (!reader.Read()) { reader.Close(); tx.Rollback(); return Response.Fail($"Бронирование с ID {bookingId} не найдено."); }
            var roomId = reader.GetInt32(0); var status = reader.GetString(1); reader.Close();
            if (status == "Cancelled") { tx.Rollback(); return Response.Fail("Бронирование уже отменено."); }
            if (status == "Paid") { tx.Rollback(); return Response.Fail("Оплаченное бронирование нельзя отменить."); }
            using var upd = new NpgsqlCommand("UPDATE \"Bookings\" SET \"Status\"='Cancelled' WHERE \"Id\"=@id", connection, tx); upd.Parameters.AddWithValue("id", bookingId); upd.ExecuteNonQuery();
            using var room = new NpgsqlCommand("UPDATE \"Rooms\" SET \"AvailablePlaces\"=\"AvailablePlaces\"+1 WHERE \"Id\"=@id", connection, tx); room.Parameters.AddWithValue("id", roomId); room.ExecuteNonQuery(); tx.Commit();
            PublishEvent(
                "BookingCancelled",
                bookingId,
                new Dictionary<string, object?>
                {
                    ["RoomId"] = roomId,
                    ["Status"] = "Cancelled"
                }
            );
            return Response.Ok("Бронирование отменено.", new { bookingId, roomId, status = "Cancelled" });
        }
        catch (Exception ex) { try { tx.Rollback(); } catch { } return Response.Fail($"Ошибка отмены бронирования: {ex.Message}"); }
    }
    private void ReplicateCreateBooking(
    int bookingId,
    int roomId,
    string guestName,
    DateTime checkIn,
    DateTime checkOut,
    decimal totalPrice)
    {
        if (string.IsNullOrWhiteSpace(_replicaConnectionString))
        {
            Log("REPLICATION", roomId, "Replica не настроена — копирование пропущено.");
            return;
        }

        try
        {
            using var connection = new NpgsqlConnection(_replicaConnectionString);
            connection.Open();

            using var transaction = connection.BeginTransaction();

            using var insert = new NpgsqlCommand(
                """
            INSERT INTO "Bookings"
                ("Id", "RoomId", "GuestName", "CheckIn", "CheckOut", "TotalPrice", "Status")
            VALUES
                (@id, @room, @guest, @checkIn, @checkOut, @total, 'Confirmed')
            """,
                connection,
                transaction);

            insert.Parameters.AddWithValue("id", bookingId);
            insert.Parameters.AddWithValue("room", roomId);
            insert.Parameters.AddWithValue("guest", guestName);
            insert.Parameters.AddWithValue("checkIn", checkIn.Date);
            insert.Parameters.AddWithValue("checkOut", checkOut.Date);
            insert.Parameters.AddWithValue("total", totalPrice);

            insert.ExecuteNonQuery();

            // Обновляем состояние номера в резервной базе.
            using var update = new NpgsqlCommand(
                """
            UPDATE "Rooms"
            SET "AvailablePlaces" = "AvailablePlaces" - 1
            WHERE "Id" = @room AND "AvailablePlaces" > 0
            """,
                connection,
                transaction);

            update.Parameters.AddWithValue("room", roomId);

            if (update.ExecuteNonQuery() != 1)
            {
                throw new InvalidOperationException(
                    "Не удалось обновить доступность номера в Replica.");
            }

            // Синхронизируем последовательность ID с явно вставленным ID.
            using var sequence = new NpgsqlCommand(
                """
                SELECT setval(
                    pg_get_serial_sequence('"Bookings"', 'Id')::regclass,
                    GREATEST(COALESCE((SELECT MAX("Id") FROM "Bookings"), 1), 1),
                    true
                )
                """,
                connection,
                transaction);

            sequence.ExecuteNonQuery();

            transaction.Commit();

            Log("REPLICATION", roomId,
                $"Успешно: бронирование #{bookingId} скопировано в Replica.");
        }
        catch (Exception ex)
        {
            Log("REPLICATION ERROR", roomId,
                $"Primary сохранён, но копирование в Replica не удалось: {ex.Message}");
        }
    }
    public Response PayBooking(int bookingId)
    {
        using var connection = new NpgsqlConnection(GetActiveConnectionString()); connection.Open(); using var tx = connection.BeginTransaction();
        try
        {
            using var cmd = new NpgsqlCommand("SELECT \"TotalPrice\",\"Status\" FROM \"Bookings\" WHERE \"Id\"=@id FOR UPDATE", connection, tx); cmd.Parameters.AddWithValue("id", bookingId);
            using var reader = cmd.ExecuteReader(); if (!reader.Read()) { reader.Close(); tx.Rollback(); return Response.Fail($"Бронирование с ID {bookingId} не найдено."); }
            var amount = reader.GetDecimal(0); var status = reader.GetString(1); reader.Close();
            if (status == "Cancelled") { tx.Rollback(); return Response.Fail("Нельзя оплатить отменённое бронирование."); }
            if (status == "Paid") { tx.Rollback(); return Response.Fail("Бронирование уже оплачено."); }
            using var payment = new NpgsqlCommand("INSERT INTO \"Payments\" (\"BookingId\",\"Amount\",\"Status\",\"PaidAt\") VALUES (@id,@amount,'Completed',NOW()) RETURNING \"Id\"", connection, tx); payment.Parameters.AddWithValue("id", bookingId); payment.Parameters.AddWithValue("amount", amount); var paymentId = Convert.ToInt32(payment.ExecuteScalar());
            using var update = new NpgsqlCommand("UPDATE \"Bookings\" SET \"Status\"='Paid' WHERE \"Id\"=@id", connection, tx); update.Parameters.AddWithValue("id", bookingId); update.ExecuteNonQuery(); tx.Commit();
            PublishEvent(
                "PaymentCompleted",
                bookingId,
                new Dictionary<string, object?>
                {
                    ["PaymentId"] = paymentId,
                    ["Amount"] = amount,
                    ["Status"] = "Completed"
                });
            return Response.Ok("Бронирование успешно оплачено.", new { bookingId, paymentId, amount, status = "Paid" });
        }
        catch (Exception ex) { try { tx.Rollback(); } catch { } return Response.Fail($"Ошибка оплаты: {ex.Message}"); }
    }
    public bool SynchronizeReplicaToPrimary()
    {
        if (string.IsNullOrWhiteSpace(_replicaConnectionString))
            return false;

        try
        {
            using var replica = new NpgsqlConnection(_replicaConnectionString);
            using var primary = new NpgsqlConnection(_connectionString);

            replica.Open();
            primary.Open();

            using var transaction = primary.BeginTransaction();

            try
            {
                string[] tables =
                {
                "Hotels", "Guests", "Rooms", "Bookings", "Payments"
            };

                foreach (string table in tables)
                {
                    var columns = new List<string>();

                    using (var schemaCmd = new NpgsqlCommand(
                        """
                    SELECT column_name
                    FROM information_schema.columns
                    WHERE table_schema = 'public' AND table_name = @table
                    ORDER BY ordinal_position
                    """, replica))
                    {
                        schemaCmd.Parameters.AddWithValue("table", table);

                        using var schemaReader = schemaCmd.ExecuteReader();
                        while (schemaReader.Read())
                            columns.Add(schemaReader.GetString(0));
                    }

                    if (columns.Count == 0)
                        throw new InvalidOperationException(
                            $"Таблица {table} не найдена в Replica.");

                    string Q(string name) =>
                        "\"" + name.Replace("\"", "\"\"") + "\"";

                    string columnList = string.Join(", ", columns.Select(Q));
                    string parameters = string.Join(", ",
                        columns.Select((_, i) => $"@p{i}"));

                    string updates = string.Join(", ",
                        columns.Where(c => c != "Id")
                               .Select(c => $"{Q(c)} = EXCLUDED.{Q(c)}"));

                    using var select = new NpgsqlCommand(
                        $"SELECT {columnList} FROM {Q(table)}", replica);

                    using var reader = select.ExecuteReader();

                    while (reader.Read())
                    {
                        string sql = updates.Length > 0
                            ? $"""
                          INSERT INTO {Q(table)} ({columnList})
                          VALUES ({parameters})
                          ON CONFLICT ("Id") DO UPDATE SET {updates}
                          """
                            : $"""
                          INSERT INTO {Q(table)} ({columnList})
                          VALUES ({parameters})
                          ON CONFLICT ("Id") DO NOTHING
                          """;

                        using var cmd = new NpgsqlCommand(sql, primary, transaction);

                        for (int i = 0; i < columns.Count; i++)
                        {
                            cmd.Parameters.AddWithValue(
                                $"p{i}",
                                reader.IsDBNull(i) ? DBNull.Value : reader.GetValue(i));
                        }

                        cmd.ExecuteNonQuery();
                    }

                    Console.WriteLine(
                        $"[{DateTime.Now:HH:mm:ss.fff}] [SYNC] Таблица {table} синхронизирована.");
                }

                // Обновляем последовательности ID, где они существуют.
                foreach (string table in tables)
                {
                    using var getSequence = new NpgsqlCommand(
                        "SELECT pg_get_serial_sequence(@table, 'Id')",
                        primary, transaction);

                    getSequence.Parameters.AddWithValue("table", $"public.\"{table}\"");
                    var sequence = getSequence.ExecuteScalar() as string;

                    if (!string.IsNullOrWhiteSpace(sequence))
                    {
                        string quotedTable = "\"" + table + "\"";

                        using var setSequence = new NpgsqlCommand(
                            $"""
                        SELECT setval(
                            @sequence::regclass,
                            GREATEST(COALESCE(
                                (SELECT MAX("Id") FROM {quotedTable}), 1), 1),
                            true)
                        """, primary, transaction);

                        setSequence.Parameters.AddWithValue("sequence", sequence);
                        setSequence.ExecuteScalar();
                    }
                }

                transaction.Commit();

                Console.WriteLine(
                    $"[{DateTime.Now:HH:mm:ss.fff}] [SYNC] Replica → Primary: SUCCESS");

                return true;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"[{DateTime.Now:HH:mm:ss.fff}] [SYNC ERROR] {ex.Message}");

            return false;
        }
    }



    private static void Log(string action, int roomId, string message) => Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [Room #{roomId}] → {action} | {message}");
    private void PublishEvent(
    string eventType,
    int bookingId,
    IReadOnlyDictionary<string, object?> data)
    {
        var @event = new BookingEvent(
            eventType,
            bookingId,
            DateTime.UtcNow,
            data);

        var result = _eventPublisher.Publish(@event);

        if (!result.Success)
        {
            Log(
                "EVENT ERROR",
                data.TryGetValue("RoomId", out var value) && value is int roomId
                    ? roomId
                    : 0,
                string.Join("; ", result.Errors));
        }
    }
}
