namespace HotelBooking.Server.Events;

public sealed class AuditSubscriber : IEventSubscriber
{
    public string Name => "AuditSubscriber";

    public bool IsAvailable => true;

    public IReadOnlyCollection<string> EventTypes { get; } =
        new[]
        {
            "BookingCreated"
        };

    public async Task HandleAsync(
        BookingEvent @event,
        CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        Console.WriteLine(
            $"[{DateTime.Now:HH:mm:ss.fff}] " +
            $"AuditSubscriber → SUCCESS | " +
            $"EventType = {@event.EventType}; " +
            $"BookingId = {@event.EntityId}");
    }
}