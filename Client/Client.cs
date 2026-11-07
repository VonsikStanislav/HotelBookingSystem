using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Globalization;
using HotelBooking.Shared;

namespace HotelBooking.Client;

public class Client
{
    private readonly string _host;
    private readonly int _port;
    private readonly RequestBuilder _requestBuilder;
    private readonly TimeSpan _clockOffset;
    private TimeSpan? _calculatedOffset;

    public Client(string host, int port, int clockOffsetSeconds = 7)
    {
        _host = host;
        _port = port;
        _requestBuilder = new RequestBuilder();
        _clockOffset = TimeSpan.FromSeconds(clockOffsetSeconds);
    }

    private DateTime GetSimulatedLocalTime() => DateTime.UtcNow + _clockOffset;

    private async Task SynchronizeTimeAsync(bool calculateCorrection)
    {
        var localAtRequest = GetSimulatedLocalTime();
        var requestUtc = DateTime.UtcNow;
        var response = await SendRequestAsync(new Request { Operation = "GetServerTime" });
        var responseUtc = DateTime.UtcNow;

        if (!response.Success || response.Data is null)
        {
            PrintResponse(response);
            return;
        }

        // При десериализации поля object в System.Text.Json обычно получается JsonElement.
        // Читаем его напрямую, не сериализуем повторно: это исключает ошибку
        // InvalidOperationException при неожиданном типе JSON-значения.
        JsonElement data;
        if (response.Data is JsonElement jsonElement)
        {
            data = jsonElement;
        }
        else
        {
            using var dataDocument = JsonDocument.Parse(JsonSerializer.Serialize(response.Data));
            data = dataDocument.RootElement.Clone();
        }

        if (data.ValueKind != JsonValueKind.Object ||
            !data.TryGetProperty("serverTimeUtc", out var serverTimeElement))
        {
            Console.WriteLine("Сервер вернул ответ без поля serverTimeUtc.");
            Console.WriteLine($"Полученные данные: {data.GetRawText()}");
            return;
        }

        DateTime serverUtc;
        if (serverTimeElement.ValueKind == JsonValueKind.String &&
            DateTimeOffset.TryParse(
                serverTimeElement.GetString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsedServerTime))
        {
            serverUtc = parsedServerTime.UtcDateTime;
        }
        else
        {
            Console.WriteLine("Поле serverTimeUtc имеет неожиданный формат.");
            Console.WriteLine($"Полученное значение: {serverTimeElement.GetRawText()}");
            return;
        }

        var serverDelayMs = data.TryGetProperty("timeResponseDelayMilliseconds", out var delayElement)
            && delayElement.ValueKind == JsonValueKind.Number
                ? delayElement.GetInt32()
                : 0;

        var rtt = responseUtc - requestUtc;
        // RTT включает искусственную задержку сервера. Вычитаем её,
        // чтобы оценить именно сетевую задержку в обоих направлениях.
        var estimatedNetworkRtt = TimeSpan.FromMilliseconds(
            Math.Max(0, rtt.TotalMilliseconds - serverDelayMs));
        var estimatedNetworkDelay = TimeSpan.FromTicks(estimatedNetworkRtt.Ticks / 2);

        // serverUtc фиксируется при получении запроса сервером. При симметричной
        // сети серверное время приблизительно равно локальному времени отправки
        // плюс половина сетевого RTT. Искусственная задержка сервера из RTT исключена.
        var estimatedClientTimeAtServerReceive = localAtRequest + estimatedNetworkDelay;
        var offset = serverUtc - estimatedClientTimeAtServerReceive;
        var estimatedServerTimeAtResponse = serverUtc
            + TimeSpan.FromMilliseconds(serverDelayMs)
            + estimatedNetworkDelay;

        Console.WriteLine("=== ИЗМЕРЕНИЕ ВРЕМЕНИ ===");
        Console.WriteLine($"Client local time до запроса: {localAtRequest:HH:mm:ss.fff} UTC (смоделировано)");
        Console.WriteLine($"Server time при получении:    {serverUtc:HH:mm:ss.fff} UTC");
        Console.WriteLine($"Request time:                 {requestUtc:HH:mm:ss.fff} UTC");
        Console.WriteLine($"Response time:                {responseUtc:HH:mm:ss.fff} UTC");
        Console.WriteLine($"RTT:                          {rtt.TotalMilliseconds:F2} мс");
        Console.WriteLine($"Server artificial delay:      {serverDelayMs} мс");
        Console.WriteLine($"Estimated network delay:      {estimatedNetworkDelay.TotalMilliseconds:F2} мс (RTT без server delay / 2)");
        Console.WriteLine($"Calculated offset:            {offset.TotalSeconds:+0.000;-0.000;0.000} сек.");

        if (calculateCorrection)
        {
            _calculatedOffset = offset;
            Console.WriteLine("=== ПОСЛЕ СИНХРОНИЗАЦИИ ===");
            Console.WriteLine($"Local time:                   {GetSimulatedLocalTime():HH:mm:ss.fff} UTC");
            Console.WriteLine($"Server time (оценка сейчас):  {estimatedServerTimeAtResponse:HH:mm:ss.fff} UTC");
            Console.WriteLine($"Calculated offset:            {_calculatedOffset.Value.TotalSeconds:+0.000;-0.000;0.000} сек.");
            Console.WriteLine($"Corrected time:               {(GetSimulatedLocalTime() + _calculatedOffset.Value):HH:mm:ss.fff} UTC");
        }
        else
        {
            Console.WriteLine("Поправка не сохранена. Выберите пункт 6, чтобы синхронизировать часы.");
        }
    }

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };


    public async Task RunAsync()
    {
        while (true)
        {
            ShowMenu();

            Console.Write("Выберите операцию: ");
            var choice = Console.ReadLine();

            if (choice == "0")
            {
                Console.WriteLine("Клиент завершён.");
                return;
            }

            if (choice == "5" || choice == "6")
            {
                try
                {
                    await SynchronizeTimeAsync(choice == "6");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка клиента: {ex.Message}");
                }
                Console.WriteLine();
                continue;
            }

            if (choice == "7")
            {
                Console.WriteLine($"Local time:       {GetSimulatedLocalTime():HH:mm:ss.fff} UTC");
                Console.WriteLine($"Configured offset: {_clockOffset.TotalSeconds:+0;-0;0} сек.");
                Console.WriteLine(_calculatedOffset is null
                    ? "Синхронизация ещё не выполнялась."
                    : $"Calculated offset: {_calculatedOffset.Value.TotalSeconds:+0.000;-0.000;0.000} сек.");
                if (_calculatedOffset is not null)
                    Console.WriteLine($"Corrected time:   {(GetSimulatedLocalTime() + _calculatedOffset.Value):HH:mm:ss.fff} UTC");
                Console.WriteLine();
                continue;
            }

            if (choice == "8" || choice == "9")
            {
                try
                {
                    var response = choice == "8"
                        ? await SendRawAsync("{invalid-json")
                        : await SendRequestAsync(new Request { Operation = "NotAnOperation" });
                    PrintResponse(response);
                }
                catch (Exception ex) { Console.WriteLine($"Ошибка: {ex.Message}"); }
                Console.WriteLine();
                continue;
            }

            Request? request = choice switch
            {
                "1" => _requestBuilder.BuildCreateBookingRequest(),
                "2" => _requestBuilder.BuildCancelBookingRequest(),
                "3" => _requestBuilder.BuildCheckAvailabilityRequest(),
                "4" => _requestBuilder.BuildPayBookingRequest(),
                _ => null
            };

            if (request is null)
            {
                Console.WriteLine("Неизвестный пункт меню.");
                Console.WriteLine();
                continue;
            }

            try
            {
                var response = await SendRequestAsync(request);
                PrintResponse(response);
            }
            catch (SocketException)
            {
                Console.WriteLine("Ошибка: сервер недоступен.");
            }
            catch (IOException)
            {
                Console.WriteLine("Ошибка сетевого взаимодействия.");
            }
            catch (JsonException)
            {
                Console.WriteLine("Ошибка обработки JSON-ответа.");
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Операция отменена.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
            }

            Console.WriteLine();
        }
    }

    public async Task<Response> SendRequestAsync(
        Request request,
        CancellationToken cancellationToken = default)
    {
        using var client = new TcpClient();

        await client.ConnectAsync(
            _host,
            _port,
            cancellationToken);

        await using var stream = client.GetStream();

        using var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            leaveOpen: true);

        using var writer = new StreamWriter(
            stream,
            new UTF8Encoding(false),
            leaveOpen: true)
        {
            AutoFlush = true
        };

        var json = JsonSerializer.Serialize(
            request,
            _jsonOptions);

        await writer.WriteLineAsync(json);

        var responseJson = await reader.ReadLineAsync(
            cancellationToken);

        if (string.IsNullOrWhiteSpace(responseJson))
            throw new IOException("Сервер не вернул ответ.");

        return JsonSerializer.Deserialize<Response>(
                   responseJson,
                   _jsonOptions)
               ?? throw new JsonException(
                   "Некорректный ответ сервера.");
    }

    public async Task<Response> SendRawAsync(
        string rawJson,
        CancellationToken cancellationToken = default)
    {
        using var client = new TcpClient();

        await client.ConnectAsync(
            _host,
            _port,
            cancellationToken);

        await using var stream = client.GetStream();

        using var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            leaveOpen: true);

        using var writer = new StreamWriter(
            stream,
            new UTF8Encoding(false),
            leaveOpen: true)
        {
            AutoFlush = true
        };

        await writer.WriteLineAsync(rawJson);

        var responseJson = await reader.ReadLineAsync(
            cancellationToken);

        if (string.IsNullOrWhiteSpace(responseJson))
            throw new IOException("Сервер не вернул ответ.");

        return JsonSerializer.Deserialize<Response>(
                   responseJson,
                   _jsonOptions)
               ?? throw new JsonException(
                   "Некорректный ответ сервера.");
    }

    private static void ShowMenu()
    {
        Console.WriteLine("=== МЕНЮ ===");
        Console.WriteLine("1. Создать бронирование");
        Console.WriteLine("2. Отменить бронирование");
        Console.WriteLine("3. Проверить доступность номера");
        Console.WriteLine("4. Оплатить бронирование");
        Console.WriteLine("5. Получить время сервера");
        Console.WriteLine("6. Синхронизировать время");
        Console.WriteLine("7. Показать состояние часов");
        Console.WriteLine("8. Тест: некорректный JSON");
        Console.WriteLine("9. Тест: неизвестная операция");
        Console.WriteLine("0. Выход");
        Console.WriteLine();
    }

    private static void PrintResponse(Response response)
    {
        Console.WriteLine("=== ОТВЕТ СЕРВЕРА ===");
        Console.WriteLine(
            $"Успех: {(response.Success ? "Да" : "Нет")}");
        Console.WriteLine($"Сообщение: {response.Message}");

        if (response.Data is not null)
        {
            Console.WriteLine("Данные:");

            Console.WriteLine(
                JsonSerializer.Serialize(
                    response.Data,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    }));
        }
    }
}
