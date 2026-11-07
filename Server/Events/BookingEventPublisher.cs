namespace HotelBooking.Server.Events;

public sealed class BookingEventPublisher
{
    private readonly BookingEventBroker _broker;

    public BookingEventPublisher(BookingEventBroker broker)
    {
        _broker = broker;
    }

    public PublicationResult Publish(BookingEvent @event)
    {
        try
        {
            Console.WriteLine(
                $"[PUBLISHER] Публикация '{@event.EventType}', " +
                $"EntityId = {@event.EntityId}.");

            var result = _broker.Publish(@event);

            if (!result.Success)
            {
                Console.WriteLine(
                    $"[PUBLISHER ERROR] Событие '{@event.EventType}' " +
                    "доставлено не всем подписчикам.");
            }

            return result;
        }
        catch (Exception exception)
        {
            Console.WriteLine(
                $"[PUBLISHER ERROR] Ошибка публикации: {exception.Message}");

            return PublicationResult.Failed(exception.Message);
        }
    }
}