namespace HotelBooking.Server.Events;

public sealed record PublicationResult(
    bool Success,
    int DeliveredCount,
    IReadOnlyList<string> Errors)
{
    public static PublicationResult Published(int deliveredCount) =>
        new(true, deliveredCount, Array.Empty<string>());

    public static PublicationResult Failed(string error) =>
        new(false, 0, new[] { error });
}