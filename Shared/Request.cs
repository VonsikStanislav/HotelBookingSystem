using System.Text.Json;
using System.Text.Json.Serialization;

namespace HotelBooking.Shared;

public class Request
{
    [JsonPropertyName("operation")]
    public string Operation { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public JsonElement? Data { get; set; }
}

public class CreateBookingData
{
    public int GuestId { get; set; }
    public int RoomId { get; set; }
    public DateTime CheckIn { get; set; }
    public DateTime CheckOut { get; set; }
}

public class BookingIdData
{
    public int BookingId { get; set; }
}

public class AvailabilityData
{
    public int RoomId { get; set; }
    public DateTime CheckIn { get; set; }
    public DateTime CheckOut { get; set; }
}
