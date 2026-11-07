using System.Diagnostics;
using System.Text.Json;
using HotelBooking.Client;
using HotelBooking.Server;
using HotelBooking.Shared;
using Xunit;

namespace HotelBooking.Tests;

public class BookingServiceLab2Tests
{
    [Fact]
    public async Task OneClient_ShouldProcessRequest()
    {
        await using var host = await TestServerHost.StartAsync(5101);

        var client = new Client.Client("127.0.0.1", 5101);

        var request = CreateAvailabilityRequest(
            roomId: 101);

        var response = await client.SendRequestAsync(request);

        Assert.True(response.Success);
        Assert.Contains(
            "доступ",
            response.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TwoClients_ShouldBeProcessed()
    {
        await using var host = await TestServerHost.StartAsync(5102);

        var client1 = new Client.Client("127.0.0.1", 5102);
        var client2 = new Client.Client("127.0.0.1", 5102);

        var request1 = CreateAvailabilityRequest(101);
        var request2 = CreateAvailabilityRequest(102);

        var responses = await Task.WhenAll(
            client1.SendRequestAsync(request1),
            client2.SendRequestAsync(request2));

        Assert.Equal(2, responses.Length);
        Assert.All(responses, response => Assert.True(response.Success));
    }

    [Fact]
    public async Task ParallelProcessing_WithTenSecondDelay_ShouldOverlap()
    {
        await using var host = await TestServerHost.StartAsync(5103);

        var client1 = new Client.Client("127.0.0.1", 5103);
        var client2 = new Client.Client("127.0.0.1", 5103);

        var request1 = CreateBookingRequest(1, 101);
        var request2 = CreateBookingRequest(2, 102);

        var stopwatch = Stopwatch.StartNew();

        var firstTask = client1.SendRequestAsync(request1);

        await Task.Delay(500);

        var secondTask = client2.SendRequestAsync(request2);

        var responses = await Task.WhenAll(
            firstTask,
            secondTask);

        stopwatch.Stop();

        Assert.All(
            responses,
            response => Assert.True(response.Success));

        // При последовательной обработке было бы примерно 20 секунд.
        // При параллельной обработке две операции занимают около 10 секунд.
        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(17),
            $"Фактическое время: {stopwatch.Elapsed.TotalSeconds:F2} сек.");
    }

    [Fact]
    public async Task Lock_TwoClients_ShouldBookOneResourceOnlyOnce()
    {
        await using var host = await TestServerHost.StartAsync(5104);

        var client1 = new Client.Client("127.0.0.1", 5104);
        var client2 = new Client.Client("127.0.0.1", 5104);

        var request1 = CreateBookingRequest(
            guestId: 1,
            roomId: 101);

        var request2 = CreateBookingRequest(
            guestId: 2,
            roomId: 101);

        var responses = await Task.WhenAll(
            client1.SendRequestAsync(request1),
            client2.SendRequestAsync(request2));

        Assert.Single(responses.Where(response => response.Success));
        Assert.Single(responses.Where(response => !response.Success));

        var room = host.Server.DataStore.Rooms.First(x => x.Id == 101);
        Assert.Equal(1, host.Server.DataStore.Bookings.Count(x => x.RoomId == 101));
        Assert.Equal(0, room.AvailablePlaces);
    }

    [Fact]
    public async Task GetServerTime_ShouldReturnUtcTimeAndAccountForRtt()
    {
        await using var host = await TestServerHost.StartAsync(5105);
        host.Server.TimeResponseDelayMilliseconds = 150;

        var client = new Client.Client("127.0.0.1", 5105, clockOffsetSeconds: 7);
        var response = await client.SendRequestAsync(new Request
        {
            Operation = "GetServerTime"
        });

        Assert.True(response.Success);
        Assert.NotNull(response.Data);

        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(response.Data));
        Assert.True(doc.RootElement.TryGetProperty("serverTimeUtc", out var serverTime));
        Assert.True(serverTime.GetDateTime() <= DateTime.UtcNow);
        Assert.Equal(150, doc.RootElement.GetProperty("timeResponseDelayMilliseconds").GetInt32());
    }

    [Fact]
    public void UnknownOperation_ShouldReturnError()
    {
        var store = new DataStore();
        var service = new BookingService(store);
        var handler = new RequestHandler(service);

        var request = new Request
        {
            Operation = "DeleteHotel"
        };

        var response = handler.HandleAsync(request).GetAwaiter().GetResult();

        Assert.False(response.Success);
        Assert.Contains(
            "Неизвестная операция",
            response.Message);
    }

    [Fact]
    public void InvalidDates_ShouldReturnError()
    {
        var store = new DataStore();
        var service = new BookingService(store);

        var response = service.CreateBookingAsync(
            guestId: 1,
            roomId: 101,
            checkIn: new DateTime(2026, 10, 10),
            checkOut: new DateTime(2026, 10, 5))
            .GetAwaiter()
            .GetResult();

        Assert.False(response.Success);
        Assert.Contains(
            "Дата выезда",
            response.Message);
    }

    private static Request CreateAvailabilityRequest(int roomId)
    {
        return new Request
        {
            Operation = "CheckAvailability",
            Data = JsonSerializer.SerializeToElement(
                new AvailabilityData
                {
                    RoomId = roomId,
                    CheckIn = new DateTime(2026, 10, 1),
                    CheckOut = new DateTime(2026, 10, 5)
                })
        };
    }

    private static Request CreateBookingRequest(
        int guestId,
        int roomId)
    {
        return new Request
        {
            Operation = "CreateBooking",
            Data = JsonSerializer.SerializeToElement(
                new CreateBookingData
                {
                    GuestId = guestId,
                    RoomId = roomId,
                    CheckIn = new DateTime(2026, 10, 1),
                    CheckOut = new DateTime(2026, 10, 5)
                })
        };
    }
}

internal sealed class TestServerHost : IAsyncDisposable
{
    private readonly CancellationTokenSource _cts;
    private readonly Task _serverTask;

    public Server.Server Server { get; }

    private TestServerHost(
        Server.Server server,
        CancellationTokenSource cts,
        Task serverTask)
    {
        Server = server;
        _cts = cts;
        _serverTask = serverTask;
    }

    public static async Task<TestServerHost> StartAsync(int port)
    {
        var server = new Server.Server("127.0.0.1", port);

        var cts = new CancellationTokenSource();

        var serverTask = server.StartAsync(cts.Token);

        await server.WhenStarted;

        return new TestServerHost(
            server,
            cts,
            serverTask);
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();

        try
        {
            await _serverTask;
        }
        catch (OperationCanceledException)
        {
        }

        _cts.Dispose();
    }
}
