using HotelBooking.Server.Events;

namespace HotelBooking.Tests;

internal sealed class FlakySubscriber : IEventSubscriber
{
    private readonly int _failuresBeforeSuccess;
    private int _attempts;

    public FlakySubscriber(
        int failuresBeforeSuccess)
    {
        _failuresBeforeSuccess = failuresBeforeSuccess;
    }

    public string Name => "FlakySubscriber";

    public bool IsAvailable => true;

    public IReadOnlyCollection<string> EventTypes { get; } =
        new[]
        {
            "BookingCreated"
        };

    public Task HandleAsync(
        BookingEvent @event,
        CancellationToken cancellationToken = default)
    {
        _attempts++;

        if (_attempts <= _failuresBeforeSuccess)
        {
            throw new InvalidOperationException(
                $"Ошибка на попытке {_attempts}.");
        }

        return Task.CompletedTask;
    }
}