using HotelBooking.Server.Events;
using HotelBooking.Shared;

namespace HotelBooking.Server;

public class BookingService : IBookingService
{
    // Задержка добавлена специально для эксперимента ЛР №2.
    public const int ExperimentDelayMilliseconds = 10_000;

    private readonly DataStore _store;
    private readonly BookingEventPublisher _eventPublisher;

    public BookingService(
        DataStore store,
        BookingEventPublisher eventPublisher)
    {
        _store = store;
        _eventPublisher = eventPublisher;
    }

    public async Task<Response> CreateBookingAsync(
        int guestId,
        int roomId,
        DateTime checkIn,
        DateTime checkOut,
        CancellationToken cancellationToken = default)
    {
        if (checkOut.Date <= checkIn.Date)
            return Response.Fail("Дата выезда должна быть позже даты заезда.");

        // Задержка моделирует конкурентные запросы, но находится ДО критической секции.
        await Task.Delay(1500, cancellationToken);

        Booking booking;
        int availableBefore;
        int availableAfter;

        // Критическая секция: проверка и изменение общего ресурса — одна атомарная операция.
        lock (_store.SyncRoot)
        {
            LogResource("LOCK", roomId, "Вход в критическую секцию (lock).");
            var guest = _store.Guests.FirstOrDefault(x => x.Id == guestId);
            if (guest is null)
            {
                LogResource("UNLOCK", roomId, "Выход из критической секции.");
                return Response.Fail($"Гость с ID {guestId} не найден.");
            }

            var room = _store.Rooms.FirstOrDefault(x => x.Id == roomId);
            if (room is null)
            {
                LogResource("UNLOCK", roomId, "Выход из критической секции.");
                return Response.Fail($"Номер с ID {roomId} не найден.");
            }

            availableBefore = room.AvailablePlaces;
            LogResource("CHECK", roomId, $"AvailablePlaces = {availableBefore}");

            if (availableBefore <= 0)
            {
                LogResource("FAIL", roomId, "Свободных номеров не осталось.");
                LogResource("UNLOCK", roomId, "Выход из критической секции.");
                return Response.Fail("Свободных номеров не осталось.");
            }

            if (HasOverlappingBooking(roomId, checkIn, checkOut))
            {
                LogResource("FAIL", roomId, "Номер уже забронирован на эти даты.");
                LogResource("UNLOCK", roomId, "Выход из критической секции.");
                return Response.Fail("Номер занят на указанные даты.");
            }

            room.AvailablePlaces--;
            availableAfter = room.AvailablePlaces;

            var nights = (checkOut.Date - checkIn.Date).Days;
            booking = new Booking
            {
                Id = _store.GetNextBookingId(),
                GuestId = guestId,
                RoomId = roomId,
                CheckIn = checkIn.Date,
                CheckOut = checkOut.Date,
                Status = "Created",
                TotalPrice = nights * room.PricePerNight
            };
            _store.Bookings.Add(booking);
            LogResource("SUCCESS", roomId, $"Booking #{booking.Id}; AvailablePlaces = {availableAfter}");
        }
        LogResource("UNLOCK", roomId, "Выход из критической секции.");

        var events = new[]
        {
            $"BookingCreated: Booking #{booking.Id}",
            $"RoomReserved: Room #{roomId}"
        };
        PublishEvent(
            "BookingCreated",
            booking.Id,
            new Dictionary<string, object?>
            {
                ["GuestId"] = guestId,
                ["RoomId"] = roomId,
                ["CheckIn"] = checkIn.Date,
                ["CheckOut"] = checkOut.Date,
                ["TotalPrice"] = booking.TotalPrice
            });
        return Response.Ok("Бронирование успешно создано.", new
        {
            booking,
            resource = new { roomId, availableBefore, availableAfter },
            events
        });
    }

    private static void LogResource(string action, int roomId, string message)
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [Resource Room #{roomId}] → {action} | {message}");
    }

    public Response CancelBooking(int bookingId)
    {
        lock (_store.SyncRoot)
        {
            var booking = _store.Bookings.FirstOrDefault(
                x => x.Id == bookingId);

            if (booking is null)
                return Response.Fail(
                    $"Бронирование с ID {bookingId} не найдено.");

            if (booking.Status == "Cancelled")
                return Response.Fail(
                    "Бронирование уже отменено.");

            if (booking.Status == "Paid")
                return Response.Fail(
                    "Оплаченное бронирование нельзя отменить в ЛР №2.");

            booking.Status = "Cancelled";

            var room = _store.Rooms.FirstOrDefault(
                x => x.Id == booking.RoomId);

            if (room is not null)
                room.AvailablePlaces++;

            var events = new[]
            {
                $"BookingCancelled: Booking #{booking.Id}"
            };

            PublishEvent(
                "BookingCancelled",
                booking.Id,
                new Dictionary<string, object?>
                {
                    ["RoomId"] = booking.RoomId,
                    ["Status"] = booking.Status
                });

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
            return Response.Fail(
                "Дата выезда должна быть позже даты заезда.");

        lock (_store.SyncRoot)
        {
            var room = _store.Rooms.FirstOrDefault(
                x => x.Id == roomId);

            if (room is null)
                return Response.Fail(
                    $"Номер с ID {roomId} не найден.");

            var available =
                room.AvailablePlaces > 0 &&
                !HasOverlappingBooking(
                    roomId,
                    checkIn,
                    checkOut);

            return Response.Ok(
                available
                    ? "Номер доступен."
                    : "Номер занят или свободных экземпляров ресурса нет.",
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
            var booking = _store.Bookings.FirstOrDefault(
                x => x.Id == bookingId);

            if (booking is null)
                return Response.Fail(
                    $"Бронирование с ID {bookingId} не найдено.");

            if (booking.Status == "Cancelled")
                return Response.Fail(
                    "Нельзя оплатить отменённое бронирование.");

            if (booking.Status == "Paid")
                return Response.Fail(
                    "Бронирование уже оплачено.");

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

            PublishEvent(
                "PaymentCompleted",
                booking.Id,
                new Dictionary<string, object?>
                {
                    ["PaymentId"] = payment.Id,
                    ["Amount"] = payment.Amount,
                    ["PaidAt"] = payment.PaidAt
                });

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

    private void PublishEvent(
        string eventType,
        int bookingId,
        IReadOnlyDictionary<string, object?> data)
    {
        var @event = new BookingEvent(
            eventType,
            bookingId,
            DateTime.UtcNow,
            data);

        var result = _eventPublisher.Publish(@event);

        if (!result.Success)
        {
            Console.WriteLine(
                $"[BOOKING SERVICE] Ошибка доставки '{@event.EventType}': " +
                string.Join("; ", result.Errors));
        }
    }
}
