namespace HotelBooking.Server.Events;

public sealed class RoomServiceSubscriber : IEventSubscriber
{
    public string Name => "RoomService";

    public IReadOnlyCollection<string> EventTypes { get; } =
        new[]
        {
            "BookingCreated",
            "BookingCancelled"
        };

    public void Handle(BookingEvent @event)
    {
        if (@event.EventType == "BookingCreated")
        {
            Console.WriteLine(
                $"[ROOM SERVICE] Номер заблокирован для бронирования " +
                $"#{@event.EntityId}.");
        }
        else if (@event.EventType == "BookingCancelled")
        {
            Console.WriteLine(
                $"[ROOM SERVICE] Ресурс освобождён для бронирования " +
                $"#{@event.EntityId}.");
        }
    }
}