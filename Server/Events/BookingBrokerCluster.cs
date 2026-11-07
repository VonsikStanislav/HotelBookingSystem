namespace HotelBooking.Server.Events;

public sealed class BookingBrokerCluster
{
    private readonly BookingEventBroker[] _brokers;
    private readonly object _syncRoot = new();

    private int _nextBrokerIndex;

    public BookingBrokerCluster(
        params BookingEventBroker[] brokers)
    {
        if (brokers.Length == 0)
        {
            throw new ArgumentException(
                "Необходимо указать хотя бы один Broker.",
                nameof(brokers));
        }

        _brokers = brokers;
    }

    public void Subscribe(
        string eventType,
        IEventSubscriber subscriber)
    {
        foreach (var broker in _brokers)
        {
            broker.Subscribe(
                eventType,
                subscriber);
        }
    }

    public async Task<PublicationResult> PublishAsync(
        BookingEvent @event,
        CancellationToken cancellationToken = default)
    {
        var brokerIndex = GetNextBrokerIndex();
        var broker = _brokers[brokerIndex];

        Console.WriteLine(
            $"[{DateTime.Now:HH:mm:ss.fff}] " +
            $"BrokerCluster → Broker #{brokerIndex + 1} | " +
            $"EventType = {@event.EventType}; " +
            $"EntityId = {@event.EntityId}");

        try
        {
            var result = await broker.PublishAsync(
                @event,
                cancellationToken);

            Console.WriteLine(
                $"[{DateTime.Now:HH:mm:ss.fff}] " +
                $"BrokerCluster → Broker #{brokerIndex + 1} | " +
                $"Success = {result.Success}");

            return result;
        }
        catch (Exception exception)
        {
            Console.WriteLine(
                $"[{DateTime.Now:HH:mm:ss.fff}] " +
                $"BrokerCluster → Broker #{brokerIndex + 1} ERROR | " +
                exception.Message);

            return PublicationResult.Failed(
                $"Broker #{brokerIndex + 1}: {exception.Message}");
        }
    }

    private int GetNextBrokerIndex()
    {
        lock (_syncRoot)
        {
            var index = _nextBrokerIndex;
            _nextBrokerIndex =
                (_nextBrokerIndex + 1) % _brokers.Length;

            return index;
        }
    }
}