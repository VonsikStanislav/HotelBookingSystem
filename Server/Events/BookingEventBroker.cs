namespace HotelBooking.Server.Events;

public sealed class BookingEventBroker
{
    private readonly Dictionary<string, List<IEventSubscriber>> _subscribers = new(
        StringComparer.OrdinalIgnoreCase);

    private readonly object _syncRoot = new();

    public void Subscribe(
        string eventType,
        IEventSubscriber subscriber)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentNullException.ThrowIfNull(subscriber);

        lock (_syncRoot)
        {
            if (!_subscribers.TryGetValue(eventType, out var subscribers))
            {
                subscribers = new List<IEventSubscriber>();
                _subscribers[eventType] = subscribers;
            }

            if (subscribers.All(x => x.Name != subscriber.Name))
                subscribers.Add(subscriber);
        }

        Console.WriteLine(
            $"[BROKER] Subscriber '{subscriber.Name}' подписан на '{eventType}'.");
    }

    public PublicationResult Publish(BookingEvent @event)
    {
        List<IEventSubscriber> subscribers;

        lock (_syncRoot)
        {
            subscribers = _subscribers.TryGetValue(@event.EventType, out var registered)
                ? registered.ToList()
                : new List<IEventSubscriber>();
        }

        var errors = new List<string>();
        var deliveredCount = 0;

        foreach (var subscriber in subscribers)
        {
            try
            {
                subscriber.Handle(@event);
                deliveredCount++;

                Console.WriteLine(
                    $"[BROKER] Событие '{@event.EventType}' доставлено " +
                    $"подписчику '{subscriber.Name}'.");
            }
            catch (Exception exception)
            {
                var error =
                    $"Subscriber '{subscriber.Name}': {exception.Message}";

                errors.Add(error);

                Console.WriteLine(
                    $"[BROKER ERROR] Не удалось доставить '{@event.EventType}': {error}");
            }
        }

        return errors.Count == 0
            ? PublicationResult.Published(deliveredCount)
            : new PublicationResult(false, deliveredCount, errors);
    }
}