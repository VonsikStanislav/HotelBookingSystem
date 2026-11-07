using System.Collections.Concurrent;
using System.Threading.Channels;

namespace HotelBooking.Server.Events;

public sealed class QueuedBookingEventBroker : IAsyncDisposable
{
    private readonly Channel<BookingEvent> _queue =
        Channel.CreateUnbounded<BookingEvent>();

    private readonly Dictionary<string, List<IEventSubscriber>> _subscribers = new(
        StringComparer.OrdinalIgnoreCase);

    private readonly object _syncRoot = new();
    private readonly CancellationTokenSource _shutdown = new();
    private readonly int _maxAttempts;

    private Task? _worker;

    public QueuedBookingEventBroker(int maxAttempts = 3)
    {
        if (maxAttempts < 1)
            throw new ArgumentOutOfRangeException(nameof(maxAttempts));

        _maxAttempts = maxAttempts;
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
            $"QueueBroker ? SUBSCRIBE | " +
            $"Subscriber = {subscriber.Name}; " +
            $"EventType = {eventType}");
    }

    public void Start()
    {
        _worker ??= Task.Run(ProcessQueueAsync);
    }

    public async Task<PublicationResult> PublishAsync(
        BookingEvent @event,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(@event.EventType))
            return PublicationResult.Failed("Тип события не указан.");

        if (@event.EntityId <= 0)
            return PublicationResult.Failed("Некорректный EntityId.");

        try
        {
            await _queue.Writer.WriteAsync(
                @event,
                cancellationToken);

            Console.WriteLine(
                $"[{DateTime.Now:HH:mm:ss.fff}] " +
                $"QueueBroker ? QUEUED | " +
                $"EventType = {@event.EventType}; " +
                $"EntityId = {@event.EntityId}");

            return PublicationResult.Published(0);
        }
        catch (Exception exception)
        {
            Console.WriteLine(
                $"[{DateTime.Now:HH:mm:ss.fff}] " +
                $"QueueBroker ? ERROR | {exception.Message}");

            return PublicationResult.Failed(exception.Message);
        }
    }

    private async Task ProcessQueueAsync()
    {
        try
        {
            await foreach (var @event in _queue.Reader.ReadAllAsync(_shutdown.Token))
            {
                List<IEventSubscriber> subscribers;

                lock (_syncRoot)
                {
                    subscribers = _subscribers.TryGetValue(@event.EventType, out var registered)
                        ? registered.ToList()
                        : new List<IEventSubscriber>();
                }

                foreach (var subscriber in subscribers)
                    await DeliverWithRetryAsync(subscriber, @event);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task DeliverWithRetryAsync(
        IEventSubscriber subscriber,
        BookingEvent @event)
    {
        for (var attempt = 1; attempt <= _maxAttempts; attempt++)
        {
            try
            {
                Console.WriteLine(
                    $"[{DateTime.Now:HH:mm:ss.fff}] " +
                    $"QueueBroker ? {subscriber.Name} | " +
                    $"Attempt = {attempt}");

                if (!subscriber.IsAvailable)
                {
                    throw new InvalidOperationException(
                        $"Subscriber '{subscriber.Name}' недоступен.");
                }

                await subscriber.HandleAsync(@event, _shutdown.Token);

                Console.WriteLine(
                    $"[{DateTime.Now:HH:mm:ss.fff}] " +
                    $"{subscriber.Name} ? SUCCESS | " +
                    $"Attempt = {attempt}");

                return;
            }
            catch (Exception exception) when (attempt < _maxAttempts)
            {
                Console.WriteLine(
                    $"[{DateTime.Now:HH:mm:ss.fff}] " +
                    $"{subscriber.Name} ? FAIL | " +
                    $"Attempt = {attempt}; " +
                    $"Error = {exception.Message}");

                await Task.Delay(
                    TimeSpan.FromMilliseconds(500),
                    _shutdown.Token);
            }
            catch (Exception exception)
            {
                Console.WriteLine(
                    $"[{DateTime.Now:HH:mm:ss.fff}] " +
                    $"{subscriber.Name} ? DEAD LETTER | " +
                    $"Attempts = {_maxAttempts}; " +
                    $"Error = {exception.Message}");
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _queue.Writer.TryComplete();
        _shutdown.Cancel();

        if (_worker is not null)
        {
            try
            {
                await _worker;
            }
            catch (OperationCanceledException)
            {
            }
        }

        _shutdown.Dispose();
    }
}