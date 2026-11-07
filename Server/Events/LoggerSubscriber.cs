namespace HotelBooking.Server.Events;

public sealed class LoggerSubscriber : IEventSubscriber
{
    public string Name => "Logger";

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
            $"Logger → START | EventType = {@event.EventType}");

        await Task.Delay(1000, cancellationToken);

        Console.WriteLine(
            $"[{DateTime.Now:HH:mm:ss.fff}] " +
            $"Logger → END | EventType = {@event.EventType}");
    }
}