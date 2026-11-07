using System.Text.Json;
using HotelBooking.Server;
using HotelBooking.Shared;
using Xunit;

namespace HotelBooking.Tests;

public sealed class PostgreSqlErrorHandlingTests
{
    [Fact]
    public async Task InvalidDates_ShouldReturnFailureWithoutDatabaseAccess()
    {
        var service = new PostgresBookingService(
            "Host=127.0.0.1;Port=1;Database=missing;Username=missing;Password=missing;Timeout=1");

        var response = await service.CreateBookingAsync(
            guestId: 2,
            roomId: 1,
            checkIn: new DateTime(2026, 10, 30),
            checkOut: new DateTime(2026, 10, 26));

        Assert.False(response.Success);
        Assert.Contains("Дата выезда", response.Message);
    }

    [Fact]
    public async Task DatabaseConnectionFailure_ShouldBecomeResponseInsteadOfCrashingHandler()
    {
        var service = new PostgresBookingService(
            "Host=127.0.0.1;Port=1;Database=missing;Username=missing;Password=missing;Timeout=1");
        var handler = new RequestHandler(service);
        var request = new Request
        {
            Operation = "CheckAvailability",
            Data = JsonSerializer.SerializeToElement(new AvailabilityData
            {
                RoomId = 1,
                CheckIn = new DateTime(2026, 11, 1),
                CheckOut = new DateTime(2026, 11, 3)
            })
        };

        var response = await handler.HandleAsync(request);

        Assert.False(response.Success);
        Assert.Contains("Ошибка обработки запроса", response.Message);
    }

    [Fact]
    public async Task MissingRoom_ShouldReturnFailure()
    {
        var connectionString =
            "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=secret";
        var service = new PostgresBookingService(connectionString);

        var response = service.CheckAvailability(
            roomId: -999999,
            checkIn: new DateTime(2098, 5, 10),
            checkOut: new DateTime(2098, 5, 12));

        Assert.False(response.Success);
        Assert.Contains("не найден", response.Message, StringComparison.OrdinalIgnoreCase);
    }
}
