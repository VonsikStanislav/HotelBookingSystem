using HotelBooking.Shared;
using HotelBooking.Server;
using System.Text.Json;

namespace HotelBooking.Server;

public class RequestHandler
{
    private readonly BookingService _bookingService;
    private readonly JsonSerializerOptions _jsonOptions;

    public RequestHandler(BookingService bookingService)
    {
        _bookingService = bookingService;

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public Response Handle(Request request)
    {
        if (request is null)
            return Response.Fail("Запрос отсутствует.");

        if (string.IsNullOrWhiteSpace(request.Operation))
            return Response.Fail("Не указана операция.");

        try
        {
            return request.Operation.Trim().ToLowerInvariant() switch
            {
                "createbooking" => HandleCreateBooking(request),
                "cancelbooking" => HandleCancelBooking(request),
                "checkavailability" => HandleCheckAvailability(request),
                "paybooking" => HandlePayBooking(request),
                _ => Response.Fail($"Неизвестная операция: {request.Operation}")
            };
        }
        catch (JsonException)
        {
            return Response.Fail("Некорректные входные данные.");
        }
        catch (Exception ex)
        {
            return Response.Fail($"Ошибка обработки запроса: {ex.Message}");
        }
    }

    private Response HandleCreateBooking(Request request)
    {
        var data = Deserialize<CreateBookingData>(request);
        return _bookingService.CreateBooking(
            data.GuestId,
            data.RoomId,
            data.CheckIn,
            data.CheckOut);
    }

    private Response HandleCancelBooking(Request request)
    {
        var data = Deserialize<BookingIdData>(request);
        return _bookingService.CancelBooking(data.BookingId);
    }

    private Response HandleCheckAvailability(Request request)
    {
        var data = Deserialize<AvailabilityData>(request);
        return _bookingService.CheckAvailability(
            data.RoomId,
            data.CheckIn,
            data.CheckOut);
    }

    private Response HandlePayBooking(Request request)
    {
        var data = Deserialize<BookingIdData>(request);
        return _bookingService.PayBooking(data.BookingId);
    }

    private T Deserialize<T>(Request request)
    {
        if (request.Data.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            throw new JsonException("Не указано поле data.");

        var result = JsonSerializer.Deserialize<T>(
            request.Data.GetRawText(),
            _jsonOptions);

        return result ?? throw new JsonException("Некорректное поле data.");
    }
}
