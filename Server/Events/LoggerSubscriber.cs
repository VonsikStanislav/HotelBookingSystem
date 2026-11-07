namespace HotelBooking.Server.Events;

public sealed class LoggerSubscriber : IEventSubscriber
{
    public string Name => "Logger";

    public IReadOnlyCollection<string> EventTypes { get; } =
        new[]
        {
            "BookingCreated",
            "BookingCancelled",
            "PaymentCompleted"
        };

    public void Handle(BookingEvent @event)
    {
        Console.WriteLine(
            $"[LOGGER] EventType = {@event.EventType}; " +
            $"EntityId = {@event.EntityId}; " +
            $"Timestamp = {@event.Timestamp:O}");
    }
}