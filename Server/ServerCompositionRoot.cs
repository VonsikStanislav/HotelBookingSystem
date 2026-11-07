using HotelBooking.Server.Events;

namespace HotelBooking.Server;

public static class ServerCompositionRoot
{
    public static BookingService CreateBookingService(DataStore store)
    {
        var eventSystem = new BookingEventSystem();

        return new BookingService(
            store,
            eventSystem.Publisher);
    }
}