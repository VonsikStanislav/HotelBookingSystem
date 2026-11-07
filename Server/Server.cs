using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using HotelBooking.Shared;
using HotelBooking.Server.Events;

namespace HotelBooking.Server;

public class Server
{
    private readonly string _host;
    private readonly int _port;
    private readonly RequestHandler _requestHandler;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly TaskCompletionSource<bool> _started =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private TcpListener? _listener;
    private int _nextClientId;


    public Server(
        string host,
        int port,
        string? postgresConnectionString = null,
        string? replicaConnectionString = null)
    {
        _host = host;
        _port = port;

        DataStore = new DataStore();

        var eventSystem = new BookingEventSystem();

        var bookingService = new BookingService(
            DataStore,
            eventSystem.Publisher);

        _requestHandler = new RequestHandler(bookingService);

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
    }


    public Task WhenStarted => _started.Task;

    // Доступ только для демонстрации и Unit-тестов ЛР №2.
    public DataStore DataStore { get; }

    public int TimeResponseDelayMilliseconds
    {
        get => _requestHandler.TimeResponseDelayMilliseconds;
        set => _requestHandler.TimeResponseDelayMilliseconds = Math.Max(0, value);
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _listener = new TcpListener(IPAddress.Parse(_host), _port);
        _listener.Start();

        _started.TrySetResult(true);

        Log("SERVER", $"Запущен на {_host}:{_port}");
        Log("SERVER", "Ожидание подключений...");

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                TcpClient client;

                try
                {
                    client = await _listener.AcceptTcpClientAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }

                var clientId = Interlocked.Increment(ref _nextClientId);

                // Важно для ЛР №2:
                // каждый подключившийся клиент обрабатывается в отдельной Task.
                _ = Task.Run(
                    () => HandleClientAsync(client, clientId, cancellationToken));
            }
        }
        finally
        {
            _listener.Stop();
            Log("SERVER", "Остановлен.");
        }
    }

    private async Task HandleClientAsync(
        TcpClient client,
        int clientId,
        CancellationToken cancellationToken)
    {
        var endpoint = client.Client.RemoteEndPoint?.ToString() ?? "unknown";

        Console.WriteLine();
        LogClient(clientId, null, "CONNECT", $"Подключение: {endpoint}");

        try
        {
            using (client)
            await using (var stream = client.GetStream())
            using (var reader = new StreamReader(
                       stream,
                       Encoding.UTF8,
                       detectEncodingFromByteOrderMarks: true,
                       leaveOpen: true))
            using (var writer = new StreamWriter(
                       stream,
                       new UTF8Encoding(false),
                       leaveOpen: true)
            {
                AutoFlush = true
            })
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    string? line;

                    try
                    {
                        line = await reader.ReadLineAsync(cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (IOException ex)
                    {
                        LogClient(clientId, null, "NETWORK ERROR", ex.Message);
                        break;
                    }

                    if (line is null)
                    {
                        LogClient(clientId, null, "DISCONNECT", "Клиент отключился.");
                        break;
                    }

                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    Request? request;
                    try
                    {
                        request = JsonSerializer.Deserialize<Request>(
                            line,
                            _jsonOptions);
                    }
                    catch (JsonException)
                    {
                        LogClient(clientId, null, "INVALID JSON", "Некорректный JSON.");
                        await TryWriteResponseAsync(
                            writer,
                            Response.Fail("Некорректный JSON-запрос."));
                        continue;
                    }

                    if (request is null)
                    {
                        await TryWriteResponseAsync(
                            writer,
                            Response.Fail("Пустой запрос."));
                        continue;
                    }

                    var operation = string.IsNullOrWhiteSpace(request.Operation)
                        ? "UNKNOWN"
                        : request.Operation;

                    var taskId = Task.CurrentId?.ToString() ?? "-";
                    var threadId = Environment.CurrentManagedThreadId;
                    var startedAt = DateTime.Now;
                    var stopwatch = Stopwatch.StartNew();

                    LogClient(
                        clientId,
                        taskId,
                        $"START {operation}",
                        $"Начало: {startedAt:HH:mm:ss.fff}, Thread: {threadId}");

                    Response response;

                    try
                    {
                        response = await _requestHandler.HandleAsync(
                            request,
                            cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        response = Response.Fail("Обработка запроса отменена.");
                    }
                    catch (Exception ex)
                    {
                        // Ошибка одного клиента не должна завершать сервер.
                        LogClient(
                            clientId,
                            taskId,
                            "PROCESS ERROR",
                            ex.Message);

                        response = Response.Fail("Ошибка при обработке запроса.");
                    }

                    stopwatch.Stop();

                    LogClient(
                        clientId,
                        taskId,
                        $"END {operation}",
                        $"Завершение: {DateTime.Now:HH:mm:ss.fff}, " +
                        $"Thread: {Environment.CurrentManagedThreadId}, " +
                        $"Длительность: {stopwatch.Elapsed.TotalSeconds:F3} сек.");

                    try
                    {
                        await TryWriteResponseAsync(writer, response);
                    }
                    catch (IOException)
                    {
                        // Клиент мог отключиться до получения ответа.
                        LogClient(
                            clientId,
                            taskId,
                            "DISCONNECT",
                            "Клиент отключился во время отправки ответа.");
                        break;
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (IOException ex)
        {
            LogClient(clientId, null, "NETWORK ERROR", ex.Message);
        }
        catch (Exception ex)
        {
            LogClient(clientId, null, "CLIENT ERROR", ex.Message);
        }
        finally
        {
            LogClient(clientId, null, "CLOSED", "Обработка подключения завершена.");
            Console.WriteLine();
        }
    }

    private static async Task TryWriteResponseAsync(
        StreamWriter writer,
        Response response)
    {
        var json = JsonSerializer.Serialize(
            response,
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

        await writer.WriteLineAsync(json);
    }

    private static void Log(
        string source,
        string message)
    {
        Console.WriteLine(
            $"[{DateTime.Now:HH:mm:ss.fff}] [{source}] {message}");
    }

    private static void LogClient(
        int clientId,
        string? taskId,
        string action,
        string message)
    {
        var taskPart = taskId is null ? "" : $" [Task {taskId}]";

        Console.WriteLine(
            $"[{DateTime.Now:HH:mm:ss.fff}] " +
            $"Client {clientId}{taskPart} → {action} | {message}");
    }
}

