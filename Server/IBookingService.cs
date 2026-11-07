using HotelBooking.Shared;

namespace HotelBooking.Server;

public interface IBookingService
{
    Task<Response> CreateBookingAsync(
        int guestId,
        int roomId,
        DateTime checkIn,
        DateTime checkOut,
        CancellationToken cancellationToken = default);

    Response CheckAvailability(
        int roomId,
        DateTime checkIn,
        DateTime checkOut);

    Response CancelBooking(int bookingId);

    Response PayBooking(int bookingId);
}