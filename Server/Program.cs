using HotelBooking.Server;

var server = new Server("127.0.0.1", 5000);

Console.WriteLine("=== HOTEL BOOKING SERVER ===");
Console.WriteLine("Сервер запускается...");
Console.WriteLine("Остановка: Ctrl+C");
Console.WriteLine();

using var cts = new CancellationTokenSource();

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

await server.StartAsync(cts.Token);
