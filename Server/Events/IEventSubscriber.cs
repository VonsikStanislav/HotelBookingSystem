namespace HotelBooking.Server.Events;

public interface IEventSubscriber
{
    string Name { get; }

    IReadOnlyCollection<string> EventTypes { get; }

    void Handle(BookingEvent @event);
}