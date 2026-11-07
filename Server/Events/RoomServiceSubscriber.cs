namespace HotelBooking.Server.Events;

public sealed class RoomServiceSubscriber : IEventSubscriber
{
    public string Name => "RoomService";

    public bool IsAvailable { get; private set; } = true;

    public IReadOnlyCollection<string> EventTypes { get; } =
        new[]
        {
            "BookingCreated",
            "BookingCancelled"
        };

    public void SetAvailability(bool isAvailable)
    {
        IsAvailable = isAvailable;

        Console.WriteLine(
            $"[{DateTime.Now:HH:mm:ss.fff}] " +
            $"RoomService → " +
            $"{(isAvailable ? "AVAILABLE" : "UNAVAILABLE")}");
    }

    public async Task HandleAsync(
        BookingEvent @event,
        CancellationToken cancellationToken = default)
    {
        var startedAt = DateTime.Now;

        Console.WriteLine(
            $"[{startedAt:HH:mm:ss.fff}] " +
            $"RoomService → START | EventType = {@event.EventType}");

        await Task.Delay(3000, cancellationToken);

        Console.WriteLine(
            $"[{DateTime.Now:HH:mm:ss.fff}] " +
            $"RoomService → END | EventType = {@event.EventType}");
    }
}