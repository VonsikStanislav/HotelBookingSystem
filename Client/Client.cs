using HotelBooking.Client;
using HotelBooking.Shared;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace HotelBooking.Client;

public class Client
{
    private readonly string _host;
    private readonly int _port;
    private readonly RequestBuilder _requestBuilder;

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public Client(string host, int port)
    {
        _host = host;
        _port = port;
        _requestBuilder = new RequestBuilder();
    }

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
                break;
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

        await client.ConnectAsync(_host, _port, cancellationToken);

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

        var json = JsonSerializer.Serialize(request, _jsonOptions);

        await writer.WriteLineAsync(json);

        var responseJson = await reader.ReadLineAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(responseJson))
            throw new IOException("Сервер не вернул ответ.");

        return JsonSerializer.Deserialize<Response>(
                   responseJson,
                   _jsonOptions)
               ?? throw new JsonException("Некорректный ответ сервера.");
    }

    private static void ShowMenu()
    {
        Console.WriteLine("=== МЕНЮ ===");
        Console.WriteLine("1. Создать бронирование");
        Console.WriteLine("2. Отменить бронирование");
        Console.WriteLine("3. Проверить доступность номера");
        Console.WriteLine("4. Оплатить бронирование");
        Console.WriteLine("0. Выход");
        Console.WriteLine();
    }

    private static void PrintResponse(Response response)
    {
        Console.WriteLine("=== ОТВЕТ СЕРВЕРА ===");
        Console.WriteLine($"Успех: {(response.Success ? "Да" : "Нет")}");
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
