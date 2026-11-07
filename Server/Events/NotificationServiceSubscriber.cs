namespace HotelBooking.Server.Events;

public sealed class NotificationServiceSubscriber : IEventSubscriber
{
    public string Name => "NotificationService";

    public bool IsAvailable => true;

    public IReadOnlyCollection<string> EventTypes { get; } =
        new[]
        {
            "BookingCreated",
            "BookingCancelled",
            "PaymentCompleted"
        };

    public async Task HandleAsync(
        BookingEvent @event,
        CancellationToken cancellationToken = default)
    {
        var startedAt = DateTime.Now;

        Console.WriteLine(
            $"[{startedAt:HH:mm:ss.fff}] " +
            $"NotificationService → START | " +
            $"EventType = {@event.EventType}");

        await Task.Delay(2000, cancellationToken);

        Console.WriteLine(
            $"[{DateTime.Now:HH:mm:ss.fff}] " +
            $"NotificationService → END | " +
            $"EventType = {@event.EventType}");
    }
}