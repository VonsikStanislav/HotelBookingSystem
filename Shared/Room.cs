namespace HotelBooking.Shared;

public class Room
{
    public int Id { get; set; }
    public int HotelId { get; set; }
    public string Number { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public decimal PricePerNight { get; set; }

    // Общий ресурс для эксперимента Race Condition.
    public int AvailablePlaces { get; set; }
}
