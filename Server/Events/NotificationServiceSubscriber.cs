namespace HotelBooking.Server.Events;

public sealed class NotificationServiceSubscriber : IEventSubscriber
{
    public string Name => "NotificationService";

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
            $"[NOTIFICATION] Отправлено уведомление: " +
            $"{@event.EventType}, бронирование #{ @event.EntityId}.");
    }
}