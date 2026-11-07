using System.Text.Json;
using HotelBooking.Shared;

namespace HotelBooking.Server;

public class RequestHandler
{
    private readonly IBookingService _bookingService;
    public int TimeResponseDelayMilliseconds { get; set; } = 0;

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public RequestHandler(IBookingService bookingService)
    {
        _bookingService = bookingService;
    }

    public async Task<Response> HandleAsync(
        Request request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
            return Response.Fail("Запрос отсутствует.");

        if (string.IsNullOrWhiteSpace(request.Operation))
            return Response.Fail("Не указана операция.");

        try
        {
            return request.Operation.Trim().ToLowerInvariant() switch
            {
                "getservertime" => await HandleGetServerTimeAsync(cancellationToken),

                "createbooking" =>
                    await HandleCreateBookingAsync(request, cancellationToken),

                "cancelbooking" =>
                    HandleCancelBooking(request),

                "checkavailability" =>
                    HandleCheckAvailability(request),

                "paybooking" =>
                    HandlePayBooking(request),

                _ =>
                    Response.Fail($"Неизвестная операция: {request.Operation}")
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

    private async Task<Response> HandleGetServerTimeAsync(CancellationToken cancellationToken)
    {
        // Фиксируем время сервера при получении запроса, затем искусственно задерживаем ответ.
        var serverTimeUtc = DateTime.UtcNow;
        if (TimeResponseDelayMilliseconds > 0)
            await Task.Delay(TimeResponseDelayMilliseconds, cancellationToken);
        return Response.Ok("Время сервера получено.", new
        {
            serverTimeUtc,
            timeResponseDelayMilliseconds = TimeResponseDelayMilliseconds
        });
    }

    private async Task<Response> HandleCreateBookingAsync(
        Request request,
        CancellationToken cancellationToken)
    {
        var data = Deserialize<CreateBookingData>(request);

        return await _bookingService.CreateBookingAsync(
            data.GuestId,
            data.RoomId,
            data.CheckIn,
            data.CheckOut,
            cancellationToken);
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
        if (request.Data is null ||
            request.Data.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            throw new JsonException("Не указано поле data.");
        }

        var result = JsonSerializer.Deserialize<T>(
            request.Data.Value.GetRawText(),
            _jsonOptions);

        return result ?? throw new JsonException(
            "Некорректные данные запроса.");
    }

}
