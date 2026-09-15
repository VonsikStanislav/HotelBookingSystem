using HotelBooking.Server;
using HotelBooking.Client;
using HotelBooking.Shared;
using Xunit;

namespace HotelBooking.Tests;

public class BookingServiceTests
{
    [Fact]
    public void CreateBooking_ValidData_ShouldCreateBooking()
    {
        var store = new DataStore();
        var service = new BookingService(store);

        var result = service.CreateBooking(
            guestId: 1,
            roomId: 101,
            checkIn: new DateTime(2026, 10, 1),
            checkOut: new DateTime(2026, 10, 5));

        Assert.True(result.Success);
        Assert.Equal("Бронирование успешно создано.", result.Message);
        Assert.Single(store.Bookings);
        Assert.Equal(480m, store.Bookings[0].TotalPrice);
    }

    [Fact]
    public void RequestHandler_UnknownOperation_ShouldReturnError()
    {
        var store = new DataStore();
        var service = new BookingService(store);
        var handler = new RequestHandler(service);

        var request = new Request
        {
            Operation = "DeleteHotel"
        };

        var result = handler.Handle(request);

        Assert.False(result.Success);
        Assert.Contains("Неизвестная операция", result.Message);
    }

    [Fact]
    public void CreateBooking_InvalidDates_ShouldReturnError()
    {
        var store = new DataStore();
        var service = new BookingService(store);

        var result = service.CreateBooking(
            guestId: 1,
            roomId: 101,
            checkIn: new DateTime(2026, 10, 10),
            checkOut: new DateTime(2026, 10, 5));

        Assert.False(result.Success);
        Assert.Equal(
            "Дата выезда должна быть позже даты заезда.",
            result.Message);
    }

    [Fact]
    public async Task Client_UnavailableServer_ShouldThrowSocketException()
    {
        var client = new HotelBooking.Client.Client(
            "127.0.0.1",
            59999);

        var request = new Request
        {
            Operation = "CheckAvailability",
            Data = System.Text.Json.JsonSerializer.SerializeToElement(
                new AvailabilityData
                {
                    RoomId = 101,
                    CheckIn = new DateTime(2026, 10, 1),
                    CheckOut = new DateTime(2026, 10, 5)
                })
        };

        await Assert.ThrowsAsync<System.Net.Sockets.SocketException>(
            () => client.SendRequestAsync(request));
    }

    [Fact]
    public void CancelBooking_ExistingBooking_ShouldCancelBooking()
    {
        var store = new DataStore();
        var service = new BookingService(store);

        service.CreateBooking(
            1,
            101,
            new DateTime(2026, 10, 1),
            new DateTime(2026, 10, 5));

        var result = service.CancelBooking(1);

        Assert.True(result.Success);
        Assert.Equal("Cancelled", store.Bookings[0].Status);
    }

    [Fact]
    public void PayBooking_ExistingBooking_ShouldCompletePayment()
    {
        var store = new DataStore();
        var service = new BookingService(store);

        service.CreateBooking(
            1,
            101,
            new DateTime(2026, 10, 1),
            new DateTime(2026, 10, 5));

        var result = service.PayBooking(1);

        Assert.True(result.Success);
        Assert.Equal("Paid", store.Bookings[0].Status);
        Assert.Single(store.Payments);
        Assert.Equal(480m, store.Payments[0].Amount);
    }
}
