using System.Text.Json;
using HotelBooking.Shared;

namespace HotelBooking.Client;

public class RequestBuilder
{
    public Request BuildCreateBookingRequest()
    {
        Console.WriteLine();
        Console.WriteLine("=== СОЗДАНИЕ БРОНИРОВАНИЯ ===");

        var guestId = ReadInt("ID гостя: ");
        var roomId = ReadInt("ID номера: ");
        var checkIn = ReadDate("Дата заезда (yyyy-MM-dd): ");
        var checkOut = ReadDate("Дата выезда (yyyy-MM-dd): ");

        return new Request
        {
            Operation = "CreateBooking",
            Data = JsonSerializer.SerializeToElement(
                new CreateBookingData
                {
                    GuestId = guestId,
                    RoomId = roomId,
                    CheckIn = checkIn,
                    CheckOut = checkOut
                })
        };
    }

    public Request BuildCancelBookingRequest()
    {
        Console.WriteLine();
        Console.WriteLine("=== ОТМЕНА БРОНИРОВАНИЯ ===");

        var bookingId = ReadInt("ID бронирования: ");

        return new Request
        {
            Operation = "CancelBooking",
            Data = JsonSerializer.SerializeToElement(
                new BookingIdData
                {
                    BookingId = bookingId
                })
        };
    }

    public Request BuildCheckAvailabilityRequest()
    {
        Console.WriteLine();
        Console.WriteLine("=== ПРОВЕРКА ДОСТУПНОСТИ ===");

        var roomId = ReadInt("ID номера: ");
        var checkIn = ReadDate("Дата заезда (yyyy-MM-dd): ");
        var checkOut = ReadDate("Дата выезда (yyyy-MM-dd): ");

        return new Request
        {
            Operation = "CheckAvailability",
            Data = JsonSerializer.SerializeToElement(
                new AvailabilityData
                {
                    RoomId = roomId,
                    CheckIn = checkIn,
                    CheckOut = checkOut
                })
        };
    }

    public Request BuildPayBookingRequest()
    {
        Console.WriteLine();
        Console.WriteLine("=== ОПЛАТА БРОНИРОВАНИЯ ===");

        var bookingId = ReadInt("ID бронирования: ");

        return new Request
        {
            Operation = "PayBooking",
            Data = JsonSerializer.SerializeToElement(
                new BookingIdData
                {
                    BookingId = bookingId
                })
        };
    }

    private static int ReadInt(string message)
    {
        while (true)
        {
            Console.Write(message);

            if (int.TryParse(Console.ReadLine(), out var value) && value > 0)
                return value;

            Console.WriteLine("Введите положительное целое число.");
        }
    }

    private static DateTime ReadDate(string message)
    {
        while (true)
        {
            Console.Write(message);

            if (DateTime.TryParse(
                    Console.ReadLine(),
                    out var value))
            {
                return value.Date;
            }

            Console.WriteLine("Введите корректную дату.");
        }
    }
}
