using HotelBooking.Shared;
using HotelBooking.Server;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace HotelBooking.Server;

public class Server
{
    private readonly string _host;
    private readonly int _port;
    private readonly RequestHandler _requestHandler;
    private readonly JsonSerializerOptions _jsonOptions;
    private TcpListener? _listener;

    public Server(string host, int port)
    {
        _host = host;
        _port = port;

        var dataStore = new DataStore();
        var bookingService = new BookingService(dataStore);
        _requestHandler = new RequestHandler(bookingService);

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var address = IPAddress.Parse(_host);
        _listener = new TcpListener(address, _port);
        _listener.Start();

        Console.WriteLine($"Сервер запущен: {_host}:{_port}");
        Console.WriteLine("Ожидание подключений...");
        Console.WriteLine();

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var acceptTask = _listener.AcceptTcpClientAsync(cancellationToken);

                TcpClient client;
                try
                {
                    client = await acceptTask;
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                _ = Task.Run(() => HandleClientAsync(client, cancellationToken), cancellationToken);
            }
        }
        finally
        {
            _listener.Stop();
            Console.WriteLine("Сервер остановлен.");
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        var remoteEndPoint = client.Client.RemoteEndPoint?.ToString() ?? "unknown";

        Console.WriteLine($"[CONNECT] Подключение клиента: {remoteEndPoint}");

        try
        {
            using (client)
            using (NetworkStream stream = client.GetStream())
            using (var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true)
            {
                AutoFlush = true,
                NewLine = "\n"
            })
            {
                while (!cancellationToken.IsCancellationRequested && client.Connected)
                {
                    var line = await reader.ReadLineAsync(cancellationToken);

                    if (line is null)
                        break;

                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    Response response;

                    try
                    {
                        var request = JsonSerializer.Deserialize<Request>(line, _jsonOptions);

                        if (request is null)
                        {
                            response = Response.Fail("Некорректный JSON-запрос.");
                        }
                        else
                        {
                            response = _requestHandler.Handle(request);
                        }
                    }
                    catch (JsonException)
                    {
                        response = Response.Fail("Некорректный JSON-запрос или входные данные.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERROR] {ex.Message}");
                        response = Response.Fail("Внутренняя ошибка сервера.");
                    }

                    var responseJson = JsonSerializer.Serialize(response, _jsonOptions);
                    await writer.WriteLineAsync(responseJson);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (IOException ex)
        {
            Console.WriteLine($"[NETWORK ERROR] {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CLIENT ERROR] {ex.Message}");
        }
        finally
        {
            Console.WriteLine($"[DISCONNECT] Клиент отключён: {remoteEndPoint}");
        }
    }
}
