using System.Collections.Concurrent;

namespace HotelBooking.Server.Events;

public sealed class AnalyticsSubscriber : IEventSubscriber
{
    private readonly ConcurrentDictionary<string, int> _eventCounters = new();

    public string Name => "AnalyticsService";

    public bool IsAvailable => true;

    public IReadOnlyCollection<string> EventTypes { get; } =
        new[]
        {
            "BookingCreated",
            "BookingCancelled",
            "PaymentCompleted"
        };

    public IReadOnlyDictionary<string, int> Statistics =>
        new Dictionary<string, int>(_eventCounters);

    public Task HandleAsync(
        BookingEvent @event,
        CancellationToken cancellationToken = default)
    {
        _eventCounters.AddOrUpdate(
            @event.EventType,
            1,
            (_, count) => count + 1);

        Console.WriteLine(
            $"[{DateTime.Now:HH:mm:ss.fff}] " +
            $"AnalyticsService ? SUCCESS | " +
            $"EventType = {@event.EventType}; " +
            $"EntityId = {@event.EntityId}");

        return Task.CompletedTask;
    }
}