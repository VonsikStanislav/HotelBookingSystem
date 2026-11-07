namespace HotelBooking.Server.Events;

public sealed class BookingEventSystem
{
    public BookingEventSystem()
    {
        Broker = new BookingEventBroker();
        Publisher = new BookingEventPublisher(Broker);

        RegisterSubscribers();
    }

    public BookingEventBroker Broker { get; }

    public BookingEventPublisher Publisher { get; }

    private void RegisterSubscribers()
    {
        var subscribers = new IEventSubscriber[]
        {
            new LoggerSubscriber(),
            new RoomServiceSubscriber(),
            new NotificationServiceSubscriber()
        };

        foreach (var subscriber in subscribers)
        {
            foreach (var eventType in subscriber.EventTypes)
                Broker.Subscribe(eventType, subscriber);
        }
    }
}