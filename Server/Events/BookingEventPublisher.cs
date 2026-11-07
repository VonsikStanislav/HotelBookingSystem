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
                $"[{DateTime.Now:HH:mm:ss.fff}] " +
                $"Client {@event.ClientId?.ToString() ?? "-"} → " +
                $"Publisher → PUBLISH | " +
                $"EventType = {@event.EventType}; " +
                $"EntityId = {@event.EntityId}");

            var result = _broker.Publish(@event);

            Console.WriteLine(
                $"[{DateTime.Now:HH:mm:ss.fff}] " +
                $"Publisher → RESULT | " +
                $"EventType = {@event.EventType}; " +
                $"Delivered = {result.DeliveredCount}; " +
                $"Success = {result.Success}");

            if (!result.Success)
            {
                Console.WriteLine(
                    $"[{DateTime.Now:HH:mm:ss.fff}] " +
                    $"Publisher → ERROR | " +
                    string.Join("; ", result.Errors));
            }

            return result;
        }
        catch (Exception exception)
        {
            Console.WriteLine(
                $"[{DateTime.Now:HH:mm:ss.fff}] " +
                $"Publisher → ERROR | {exception.Message}");

            return PublicationResult.Failed(exception.Message);
        }
    }
}