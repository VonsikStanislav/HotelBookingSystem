using System.Text.Json.Serialization;

namespace HotelBooking.Shared;

public class Response
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public object? Data { get; set; }

    public static Response Ok(string message, object? data = null)
    {
        return new Response
        {
            Success = true,
            Message = message,
            Data = data
        };
    }

    public static Response Fail(string message, object? data = null)
    {
        return new Response
        {
            Success = false,
            Message = message,
            Data = data
        };
    }
}
