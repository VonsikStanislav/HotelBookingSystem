using HotelBooking.Client;

var client = new Client("127.0.0.1", 5000);

Console.WriteLine("=== HOTEL BOOKING CLIENT ===");
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
