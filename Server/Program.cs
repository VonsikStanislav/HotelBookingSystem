
using HotelBooking.Server;
using HotelBooking.Server.Events;

var primaryConnectionString =
    "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=secret";

var replicaConnectionString =
    "Host=localhost;Port=5433;Database=postgres;Username=postgres;Password=secret";

var server = new global::HotelBooking.Server.Server(
    "127.0.0.1",
    5000,
    primaryConnectionString,
    replicaConnectionString);

Console.WriteLine("=== HOTEL BOOKING SERVER — ЛР №5 ===");
Console.WriteLine("Primary: localhost:5432");
Console.WriteLine("Replica: localhost:5433");
Console.WriteLine("Для остановки сервера нажмите Ctrl+C.");

server.TimeResponseDelayMilliseconds = 500;

using var cts = new CancellationTokenSource();

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

await server.StartAsync(cts.Token);
