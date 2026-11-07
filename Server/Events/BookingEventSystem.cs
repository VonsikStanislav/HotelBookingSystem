namespace HotelBooking.Server.Events;

public sealed class BookingEventSystem
{
    public BookingEventSystem()
    {
        PrimaryBroker = new BookingEventBroker("PrimaryBroker");
        SecondaryBroker = new BookingEventBroker("SecondaryBroker");

        BrokerCluster = new BookingBrokerCluster(
            PrimaryBroker,
            SecondaryBroker);

        Publisher = new BookingEventPublisher(
            BrokerCluster);

        RegisterSubscribers();
    }

    public BookingEventBroker PrimaryBroker { get; }

    public BookingEventBroker SecondaryBroker { get; }

    public BookingBrokerCluster BrokerCluster { get; }

    public BookingEventPublisher Publisher { get; }

    private void RegisterSubscribers()
    {
        var subscribers = new IEventSubscriber[]
        {
            new LoggerSubscriber(),
            new RoomServiceSubscriber(),
            new NotificationServiceSubscriber(),
            new AuditSubscriber(),
            new AnalyticsSubscriber()
        };

        foreach (var subscriber in subscribers)
        {
            foreach (var eventType in subscriber.EventTypes)
            {
                BrokerCluster.Subscribe(
                    eventType,
                    subscriber);
            }
        }
    }
}