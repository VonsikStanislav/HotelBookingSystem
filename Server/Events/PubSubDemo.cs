namespace HotelBooking.Server.Events;

public static class PubSubDemo
{
    public static void Run()
    {
        var system = new BookingEventSystem();

        var @event = new BookingEvent(
            "BookingCreated",
            125,
            DateTime.UtcNow,
            new Dictionary<string, object?>
            {
                ["RoomId"] = 10,
                ["GuestId"] = 4,
                ["TotalPrice"] = 25000m
            });

        var result = system.Publisher.Publish(@event);

        Console.WriteLine(
            $"Результат публикации: Success = {result.Success}; " +
            $"DeliveredCount = {result.DeliveredCount}");
    }
}