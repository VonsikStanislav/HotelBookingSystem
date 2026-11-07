namespace HotelBooking.Server.Events;

public interface IEventSubscriber
{
    string Name { get; }

    bool IsAvailable { get; }

    IReadOnlyCollection<string> EventTypes { get; }

    Task HandleAsync(
        BookingEvent @event,
        CancellationToken cancellationToken = default);
}