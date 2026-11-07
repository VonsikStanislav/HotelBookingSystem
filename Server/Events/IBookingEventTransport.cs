namespace HotelBooking.Server.Events;

public interface IBookingEventTransport
{
    void Subscribe(
        string eventType,
        IEventSubscriber subscriber);

    Task<PublicationResult> PublishAsync(
        BookingEvent @event,
        CancellationToken cancellationToken = default);
}