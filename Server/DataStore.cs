using HotelBooking.Shared;

namespace HotelBooking.Server;

public class DataStore
{
    public List<Guest> Guests { get; } = new();
    public List<Hotel> Hotels { get; } = new();
    public List<Room> Rooms { get; } = new();
    public List<Booking> Bookings { get; } = new();
    public List<Payment> Payments { get; } = new();

    public object SyncRoot { get; } = new();

    private int _nextBookingId = 1;
    private int _nextPaymentId = 1;

    public DataStore()
    {
        Seed();
    }

    public int GetNextBookingId() => _nextBookingId++;

    public int GetNextPaymentId() => _nextPaymentId++;

    private void Seed()
    {
        Guests.AddRange(new[]
        {
            new Guest
            {
                Id = 1,
                Name = "Иван Петров",
                Phone = "+375291112233",
                Email = "ivan@example.com"
            },
            new Guest
            {
                Id = 2,
                Name = "Анна Смирнова",
                Phone = "+375292223344",
                Email = "anna@example.com"
            }
        });

        Hotels.AddRange(new[]
        {
            new Hotel
            {
                Id = 1,
                Name = "Grand Minsk",
                Address = "Минск, проспект Победителей, 9"
            },
            new Hotel
            {
                Id = 2,
                Name = "Central Hotel",
                Address = "Минск, улица Ленина, 15"
            }
        });

        Rooms.AddRange(new[]
        {
            new Room
            {
                Id = 101,
                HotelId = 1,
                Number = "101",
                Type = "Standard",
                PricePerNight = 120m
            },
            new Room
            {
                Id = 102,
                HotelId = 1,
                Number = "102",
                Type = "Deluxe",
                PricePerNight = 180m
            },
            new Room
            {
                Id = 201,
                HotelId = 2,
                Number = "201",
                Type = "Standard",
                PricePerNight = 90m
            },
            new Room
            {
                Id = 202,
                HotelId = 2,
                Number = "202",
                Type = "Suite",
                PricePerNight = 250m
            }
        });
    }
}
