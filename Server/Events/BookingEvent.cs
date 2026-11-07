namespace HotelBooking.Server.Events;

public sealed record BookingEvent(
    string EventType,
    int EntityId,
    DateTime Timestamp,
    IReadOnlyDictionary<string, object?> Data);