namespace HotelBooking.Server.Events;

public sealed class BookingEventPublisher
{
    private readonly BookingBrokerCluster _brokerCluster;

    public BookingEventPublisher(
        BookingBrokerCluster brokerCluster)
    {
        ArgumentNullException.ThrowIfNull(brokerCluster);

        _brokerCluster = brokerCluster;
    }

    public BookingEventPublisher(
        BookingEventBroker broker)
        : this(new BookingBrokerCluster(broker))
    {
    }

    public async Task<PublicationResult> PublishAsync(
        BookingEvent @event,
        CancellationToken cancellationToken = default)
    {
        try
        {
            Console.WriteLine(
                $"[{DateTime.Now:HH:mm:ss.fff}] " +
                $"Client {@event.ClientId?.ToString() ?? "-"} → " +
                $"Publisher → PUBLISH | " +
                $"EventType = {@event.EventType}; " +
                $"EntityId = {@event.EntityId}");

            var result = await _brokerCluster.PublishAsync(
                @event,
                cancellationToken);

            Console.WriteLine(
                $"[{DateTime.Now:HH:mm:ss.fff}] " +
                $"Publisher → RESULT | " +
                $"Success = {result.Success}; " +
                $"Delivered = {result.DeliveredCount}");

            return result;
        }
        catch (Exception exception)
        {
            Console.WriteLine(
                $"[{DateTime.Now:HH:mm:ss.fff}] " +
                $"Publisher → ERROR | {exception.Message}");

            return PublicationResult.Failed(
                exception.Message);
        }
    }

    public PublicationResult Publish(
        BookingEvent @event)
    {
        return PublishAsync(@event)
            .GetAwaiter()
            .GetResult();
    }
}