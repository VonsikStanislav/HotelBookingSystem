namespace HotelBooking.Server.Events;

public sealed class BookingEventBroker
{
    private readonly string _name;

    private readonly Dictionary<string, List<IEventSubscriber>> _subscribers =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly object _syncRoot = new();

    public BookingEventBroker(string name = "Broker")
    {
        _name = name;
    }

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
            $"{_name} → SUBSCRIBE | " +
            $"Subscriber = {subscriber.Name}; " +
            $"EventType = {eventType}");
    }

    public Task<PublicationResult> PublishAsync(
        BookingEvent @event,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine(
            $"[{DateTime.Now:HH:mm:ss.fff}] " +
            $"Broker → EVENT RECEIVED | " +
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
        var deliveryTasks = subscribers.Select(
            subscriber => DeliverAsync(
                subscriber,
                @event,
                errors,
                cancellationToken));

        return Task.WhenAll(deliveryTasks).ContinueWith(
            _ =>
            {
                var deliveredCount = subscribers.Count(
                    subscriber => subscriber.IsAvailable);

                return errors.Count == 0
                    ? PublicationResult.Published(deliveredCount)
                    : new PublicationResult(false, deliveredCount, errors);
            });
    }

    private static async Task DeliverAsync(
        IEventSubscriber subscriber,
        BookingEvent @event,
        ICollection<string> errors,
        CancellationToken cancellationToken)
    {
        Console.WriteLine(
            $"[{DateTime.Now:HH:mm:ss.fff}] " +
            $"Broker → {subscriber.Name} | " +
            $"EventType = {@event.EventType}");

        if (!subscriber.IsAvailable)
        {
            var unavailableError =
                $"Subscriber '{subscriber.Name}' недоступен.";

            errors.Add(unavailableError);

            Console.WriteLine(
                $"[{DateTime.Now:HH:mm:ss.fff}] " +
                $"{subscriber.Name} → UNAVAILABLE | " +
                $"EventType = {@event.EventType}; " +
                $"Error = {unavailableError}");

            return;
        }

        try
        {
            await subscriber.HandleAsync(
                @event,
                cancellationToken);

            Console.WriteLine(
                $"[{DateTime.Now:HH:mm:ss.fff}] " +
                $"{subscriber.Name} → SUCCESS | " +
                $"EventType = {@event.EventType}");
        }
        catch (Exception exception)
        {
            var error =
                $"Subscriber '{subscriber.Name}': {exception.Message}";

            errors.Add(error);

            Console.WriteLine(
                $"[{DateTime.Now:HH:mm:ss.fff}] " +
                $"{subscriber.Name} → ERROR | " +
                $"EventType = {@event.EventType}; " +
                $"Error = {exception.Message}");
        }
    }
}