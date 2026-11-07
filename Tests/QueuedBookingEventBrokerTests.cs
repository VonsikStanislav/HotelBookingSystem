using HotelBooking.Server.Events;

namespace HotelBooking.Tests;

public sealed class QueuedBookingEventBrokerTests
{
    [Fact]
    public async Task BrokerRetriesDelivery()
    {
        await using var broker = new QueuedBookingEventBroker(3);

        var subscriber = new FlakySubscriber(
            failuresBeforeSuccess: 2);

        broker.Subscribe("BookingCreated", subscriber);
        broker.Start();

        var result = await broker.PublishAsync(
            new BookingEvent(
                "BookingCreated",
                10,
                DateTime.UtcNow,
                new Dictionary<string, object?>()));

        Assert.True(result.Success);

        await Task.Delay(2000);
    }
}