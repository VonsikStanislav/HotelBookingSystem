using HotelBooking.Client;

var offset = args.Length > 0 && int.TryParse(args[0], out var parsedOffset) ? parsedOffset : 7;
var client = new Client("127.0.0.1", 5000, offset);

Console.WriteLine("=== HOTEL BOOKING CLIENT — ЛР №3 ===");
Console.WriteLine("Можно запустить два экземпляра Client одновременно.");
Console.WriteLine();

try
{
    await client.RunAsync();
}
catch (Exception ex)
{
    Console.WriteLine($"Ошибка клиента: {ex.Message}");
    Console.WriteLine("Проверьте, что сервер запущен.");
}
