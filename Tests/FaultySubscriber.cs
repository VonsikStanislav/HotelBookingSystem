using HotelBooking.Server.Events;

namespace HotelBooking.Tests;

internal sealed class FaultySubscriber : IEventSubscriber
{
    public string Name => "FaultySubscriber";

    public bool IsAvailable => true;

    public IReadOnlyCollection<string> EventTypes { get; } =
        new[]
        {
            "BookingCreated"
        };

    public Task HandleAsync(
        BookingEvent @event,
        CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Тестовая ошибка Subscriber.");
    }
}