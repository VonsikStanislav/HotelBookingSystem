using HotelBooking.Shared;

namespace HotelBooking.Server;

public class BookingService
{
    private readonly DataStore _store;

    public BookingService(DataStore store)
    {
        _store = store;
    }

    public Response CreateBooking(
        int guestId,
        int roomId,
        DateTime checkIn,
        DateTime checkOut)
    {
        if (checkOut.Date <= checkIn.Date)
            return Response.Fail("Дата выезда должна быть позже даты заезда.");

        lock (_store.SyncRoot)
        {
            var guest = _store.Guests.FirstOrDefault(x => x.Id == guestId);
            if (guest is null)
                return Response.Fail($"Гость с ID {guestId} не найден.");

            var room = _store.Rooms.FirstOrDefault(x => x.Id == roomId);
            if (room is null)
                return Response.Fail($"Номер с ID {roomId} не найден.");

            if (HasOverlappingBooking(roomId, checkIn, checkOut))
                return Response.Fail("Номер недоступен на указанные даты.");

            var nights = (checkOut.Date - checkIn.Date).Days;
            var totalPrice = nights * room.PricePerNight;

            var booking = new Booking
            {
                Id = _store.GetNextBookingId(),
                GuestId = guestId,
                RoomId = roomId,
                CheckIn = checkIn.Date,
                CheckOut = checkOut.Date,
                Status = "Created",
                TotalPrice = totalPrice
            };

            _store.Bookings.Add(booking);

            var events = new[]
            {
                $"BookingCreated: Booking #{booking.Id}",
                $"RoomReserved: Room #{room.Id}"
            };

            return Response.Ok(
                "Бронирование успешно создано.",
                new
                {
                    booking,
                    events
                });
        }
    }

    public Response CancelBooking(int bookingId)
    {
        lock (_store.SyncRoot)
        {
            var booking = _store.Bookings.FirstOrDefault(x => x.Id == bookingId);

            if (booking is null)
                return Response.Fail($"Бронирование с ID {bookingId} не найдено.");

            if (booking.Status == "Cancelled")
                return Response.Fail("Бронирование уже отменено.");

            if (booking.Status == "Paid")
                return Response.Fail("Оплаченное бронирование нельзя отменить в ЛР №1.");

            booking.Status = "Cancelled";

            var events = new[]
            {
                $"BookingCancelled: Booking #{booking.Id}"
            };

            return Response.Ok(
                "Бронирование отменено.",
                new
                {
                    booking,
                    events
                });
        }
    }

    public Response CheckAvailability(
        int roomId,
        DateTime checkIn,
        DateTime checkOut)
    {
        if (checkOut.Date <= checkIn.Date)
            return Response.Fail("Дата выезда должна быть позже даты заезда.");

        lock (_store.SyncRoot)
        {
            var room = _store.Rooms.FirstOrDefault(x => x.Id == roomId);

            if (room is null)
                return Response.Fail($"Номер с ID {roomId} не найден.");

            var available = !HasOverlappingBooking(roomId, checkIn, checkOut);

            return Response.Ok(
                available
                    ? "Номер доступен."
                    : "Номер занят на указанные даты.",
                new
                {
                    room,
                    available,
                    checkIn = checkIn.Date,
                    checkOut = checkOut.Date
                });
        }
    }

    public Response PayBooking(int bookingId)
    {
        lock (_store.SyncRoot)
        {
            var booking = _store.Bookings.FirstOrDefault(x => x.Id == bookingId);

            if (booking is null)
                return Response.Fail($"Бронирование с ID {bookingId} не найдено.");

            if (booking.Status == "Cancelled")
                return Response.Fail("Нельзя оплатить отменённое бронирование.");

            if (booking.Status == "Paid")
                return Response.Fail("Бронирование уже оплачено.");

            var payment = new Payment
            {
                Id = _store.GetNextPaymentId(),
                BookingId = booking.Id,
                Amount = booking.TotalPrice,
                Status = "Completed",
                PaidAt = DateTime.UtcNow
            };

            _store.Payments.Add(payment);
            booking.Status = "Paid";

            var events = new[]
            {
                $"PaymentCompleted: Booking #{booking.Id}"
            };

            return Response.Ok(
                "Бронирование успешно оплачено.",
                new
                {
                    booking,
                    payment,
                    events
                });
        }
    }

    private bool HasOverlappingBooking(
        int roomId,
        DateTime checkIn,
        DateTime checkOut)
    {
        return _store.Bookings.Any(booking =>
            booking.RoomId == roomId &&
            booking.Status != "Cancelled" &&
            checkIn.Date < booking.CheckOut.Date &&
            checkOut.Date > booking.CheckIn.Date);
    }
}
