using HotelBooking.Server.Events;

namespace HotelBooking.Tests;

public sealed class BookingEventBrokerTests
{
    [Fact]
    public void EventContainsRequiredData()
    {
        var timestamp = DateTime.UtcNow;

        var @event = new BookingEvent(
            "BookingCreated",
            10,
            timestamp,
            new Dictionary<string, object?>
            {
                ["RoomId"] = 1,
                ["GuestId"] = 2
            },
            ClientId: 2);

        Assert.Equal("BookingCreated", @event.EventType);
        Assert.Equal(10, @event.EntityId);
        Assert.Equal(timestamp, @event.Timestamp);
        Assert.Equal(2, @event.ClientId);
    }

    [Fact]
    public async Task EventIsDeliveredToSubscribedSubscriber()
    {
        var broker = new BookingEventBroker();
        var subscriber = new TestSubscriber("Logger", "BookingCreated");

        broker.Subscribe("BookingCreated", subscriber);

        var result = await broker.PublishAsync(CreateEvent());

        Assert.True(result.Success);
        Assert.Equal(1, subscriber.ReceivedCount);
        Assert.Equal(1, result.DeliveredCount);
    }

    [Fact]
    public async Task EventIsNotDeliveredToUnsubscribedSubscriber()
    {
        var broker = new BookingEventBroker();
        var subscriber = new TestSubscriber("PaymentSubscriber", "PaymentCompleted");

        broker.Subscribe("PaymentCompleted", subscriber);

        var result = await broker.PublishAsync(CreateEvent());

        Assert.True(result.Success);
        Assert.Equal(0, subscriber.ReceivedCount);
    }

    [Fact]
    public async Task OneEventIsDeliveredToSeveralSubscribers()
    {
        var broker = new BookingEventBroker();

        var logger = new TestSubscriber("Logger", "BookingCreated");
        var audit = new TestSubscriber("AuditSubscriber", "BookingCreated");
        var notification = new TestSubscriber(
            "NotificationService",
            "BookingCreated");

        broker.Subscribe("BookingCreated", logger);
        broker.Subscribe("BookingCreated", audit);
        broker.Subscribe("BookingCreated", notification);

        var result = await broker.PublishAsync(CreateEvent());

        Assert.True(result.Success);
        Assert.Equal(1, logger.ReceivedCount);
        Assert.Equal(1, audit.ReceivedCount);
        Assert.Equal(1, notification.ReceivedCount);
        Assert.Equal(3, result.DeliveredCount);
    }

    [Fact]
    public async Task SubscriberErrorDoesNotStopBroker()
    {
        var broker = new BookingEventBroker();

        var faulty = new FaultySubscriber();
        var logger = new TestSubscriber("Logger", "BookingCreated");

        broker.Subscribe("BookingCreated", faulty);
        broker.Subscribe("BookingCreated", logger);

        var result = await broker.PublishAsync(CreateEvent());

        Assert.False(result.Success);
        Assert.Single(result.Errors);
        Assert.Equal(1, logger.ReceivedCount);
    }

    [Fact]
    public async Task NewSubscriberReceivesExistingEventWithoutPublisherChanges()
    {
        var broker = new BookingEventBroker();
        var newSubscriber = new TestSubscriber(
            "NewSubscriber",
            "BookingCreated");

        broker.Subscribe("BookingCreated", newSubscriber);

        var result = await broker.PublishAsync(CreateEvent());

        Assert.True(result.Success);
        Assert.Equal(1, newSubscriber.ReceivedCount);
    }

    private static BookingEvent CreateEvent()
    {
        return new BookingEvent(
            "BookingCreated",
            10,
            DateTime.UtcNow,
            new Dictionary<string, object?>
            {
                ["RoomId"] = 1,
                ["GuestId"] = 2
            },
            ClientId: 2);
    }

    private sealed class TestSubscriber : IEventSubscriber
    {
        private readonly string _eventType;

        public TestSubscriber(
            string name,
            string eventType)
        {
            Name = name;
            _eventType = eventType;
        }

        public string Name { get; }

        public bool IsAvailable => true;

        public IReadOnlyCollection<string> EventTypes =>
            new[] { _eventType };

        public int ReceivedCount { get; private set; }

        public Task HandleAsync(
            BookingEvent @event,
            CancellationToken cancellationToken = default)
        {
            ReceivedCount++;
            return Task.CompletedTask;
        }
    }
}