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
            $"[{DateTime.Now:HH:mm:ss.fff}] " +
            $"Broker → SUBSCRIBE | " +
            $"Subscriber = {subscriber.Name}; EventType = {eventType}");
    }

    public PublicationResult Publish(BookingEvent @event)
    {
        Console.WriteLine(
            $"[{DateTime.Now:HH:mm:ss.fff}] " +
            $"Broker → EVENT RECEIVED | " +
            $"Client {@event.ClientId?.ToString() ?? "-"}; " +
            $"EventType = {@event.EventType}; " +
            $"EntityId = {@event.EntityId}");

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
            Console.WriteLine(
                $"[{DateTime.Now:HH:mm:ss.fff}] " +
                $"Broker → {subscriber.Name} | " +
                $"EventType = {@event.EventType}");

            try
            {
                subscriber.Handle(@event);
                deliveredCount++;

                Console.WriteLine(
                    $"[{DateTime.Now:HH:mm:ss.fff}] " +
                    $"{subscriber.Name} → SUCCESS | " +
                    $"EventType = {@event.EventType}; " +
                    $"EntityId = {@event.EntityId}");
            }
            catch (Exception exception)
            {
                var error =
                    $"{subscriber.Name}: {exception.Message}";

                errors.Add(error);

                Console.WriteLine(
                    $"[{DateTime.Now:HH:mm:ss.fff}] " +
                    $"{subscriber.Name} → ERROR | " +
                    $"EventType = {@event.EventType}; " +
                    $"Error = {exception.Message}");
            }
        }

        return errors.Count == 0
            ? PublicationResult.Published(deliveredCount)
            : new PublicationResult(false, deliveredCount, errors);
    }
}