using AutoMapper;
using CinemaGo.Application.Profiles;
using CinemaGo.Application.Services.Implementations;
using CinemaGo.Domain.DTOs.BookingDTOs;
using CinemaGo.Domain.Entities;
using CinemaGo.Domain.Enums;
using CinemaGo.Domain.Exceptions;
using CinemaGo.Domain.Intefraces;
using Microsoft.Extensions.Logging.Abstractions;

namespace CinemaGo.Tests;

public class BookingServiceTests
{
    private readonly FakeSessionRepository _sessions = new();
    private readonly FakeSeatRepository _seats = new();
    private readonly FakeBookingRepository _bookings = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly BookingService _service;

    public BookingServiceTests()
    {
        var mapper = new MapperConfiguration(
            configuration => configuration.AddProfile<BookingProfile>(),
            NullLoggerFactory.Instance).CreateMapper();

        _service = new BookingService(
            _bookings,
            _sessions,
            _seats,
            _unitOfWork,
            mapper);

        _sessions.Session = new Session
        {
            Id = 1,
            HallId = 2,
            StartsAt = DateTime.UtcNow.AddDays(1),
            StandardPrice = 10,
            VipPrice = 20,
            Movie = new Movie { Title = "Dune" }
        };

        _seats.Seat = new Seat
        {
            Id = 3,
            HallId = 2,
            RowNumber = 4,
            SeatNumber = 5,
            IsVip = true
        };
    }

    [Fact]
    public async Task BookTicket_SavesAndReturnsBookingDetails()
    {
        var result = await _service.BookTicketAsync(
            new CreateBookingDTO { SessionId = 1, SeatId = 3 },
            userId: 7,
            CancellationToken.None);

        Assert.Equal(1, _unitOfWork.SaveCount);
        Assert.NotNull(_bookings.AddedBooking);
        Assert.Equal(7, _bookings.AddedBooking.UserId);
        Assert.Equal(BookingStatus.Active, _bookings.AddedBooking.Status);
        Assert.Same(_bookings.AddedBooking, _bookings.AddedBooking.Ticket!.Booking);
        Assert.Equal("Dune", result.MovieTitle);
        Assert.Equal(_sessions.Session.StartsAt, result.SessionStartsAt);
        Assert.Equal(4, result.RowNumber);
        Assert.Equal(5, result.SeatNumber);
        Assert.True(result.IsVip);
        Assert.Equal("Active", result.Status);
        Assert.Equal(20m, result.FinalPrice);
        Assert.Equal(_bookings.AddedBooking.Ticket.TicketCode, result.TicketCode);
    }

    [Fact]
    public async Task BookTicket_StandardSeat_UsesStandardPrice()
    {
        _seats.Seat.IsVip = false;

        var result = await _service.BookTicketAsync(
            new CreateBookingDTO { SessionId = 1, SeatId = 3 },
            userId: 7,
            CancellationToken.None);

        Assert.Equal(10m, result.FinalPrice);
        Assert.Equal(10m, _bookings.AddedBooking!.Ticket!.FinalPrice);
        Assert.False(result.IsVip);
    }

    [Fact]
    public async Task BookTicket_SeatInAnotherHall_ThrowsValidationAndDoesNotSave()
    {
        _seats.Seat.HallId = 99;

        await Assert.ThrowsAsync<ValidationException>(() => _service.BookTicketAsync(
            new CreateBookingDTO { SessionId = 1, SeatId = 3 },
            userId: 7,
            CancellationToken.None));

        Assert.Null(_bookings.AddedBooking);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task BookTicket_SessionAlreadyStarted_ThrowsValidationAndDoesNotSave()
    {
        _sessions.Session.StartsAt = DateTime.UtcNow.AddMinutes(-1);

        await Assert.ThrowsAsync<ValidationException>(() => _service.BookTicketAsync(
            new CreateBookingDTO { SessionId = 1, SeatId = 3 },
            userId: 7,
            CancellationToken.None));

        Assert.Null(_bookings.AddedBooking);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task BookTicket_UnknownSession_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.BookTicketAsync(
            new CreateBookingDTO { SessionId = 404, SeatId = 3 },
            userId: 7,
            CancellationToken.None));

        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task BookTicket_UnknownSeat_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.BookTicketAsync(
            new CreateBookingDTO { SessionId = 1, SeatId = 404 },
            userId: 7,
            CancellationToken.None));

        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task BookTicket_CancelledSession_ThrowsValidationAndDoesNotSave()
    {
        _sessions.Session.Status = SessionStatus.Cancelled;

        await Assert.ThrowsAsync<ValidationException>(() => _service.BookTicketAsync(
            new CreateBookingDTO { SessionId = 1, SeatId = 3 },
            userId: 7,
            CancellationToken.None));

        Assert.Null(_bookings.AddedBooking);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task BookTicket_SeatAlreadyTaken_ThrowsConflictAndDoesNotSave()
    {
        _bookings.SeatTaken = true;

        await Assert.ThrowsAsync<ConflictException>(() => _service.BookTicketAsync(
            new CreateBookingDTO { SessionId = 1, SeatId = 3 },
            userId: 7,
            CancellationToken.None));

        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task CancelBooking_OtherUsersBooking_ThrowsForbidden()
    {
        var booking = await SeedBookingAsync(ownerId: 7);

        await Assert.ThrowsAsync<ForbiddenException>(() => _service.CancelBookingAsync(
            booking.Id, userId: 8, isAdmin: false, CancellationToken.None));

        Assert.Equal(BookingStatus.Active, booking.Status);
    }

    [Fact]
    public async Task CancelBooking_Admin_CanCancelAnyBooking()
    {
        var booking = await SeedBookingAsync(ownerId: 7);

        await _service.CancelBookingAsync(
            booking.Id, userId: 99, isAdmin: true, CancellationToken.None);

        Assert.Equal(BookingStatus.Cancelled, booking.Status);
    }

    [Fact]
    public async Task CancelBooking_Owner_CancelsOwnBooking()
    {
        var booking = await SeedBookingAsync(ownerId: 7);

        await _service.CancelBookingAsync(
            booking.Id, userId: 7, isAdmin: false, CancellationToken.None);

        Assert.Equal(BookingStatus.Cancelled, booking.Status);
    }

    private async Task<Booking> SeedBookingAsync(int ownerId)
    {
        await _service.BookTicketAsync(
            new CreateBookingDTO { SessionId = 1, SeatId = 3 },
            ownerId,
            CancellationToken.None);

        return _bookings.AddedBooking!;
    }

    private sealed class FakeSessionRepository : ISessionRepository
    {
        public Session Session { get; set; } = null!;

        public Task<ICollection<Session>> GetAllSessionsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<ICollection<Session>>(new List<Session> { Session });

        public Task<Session?> GetSessionByIdAsync(int id, CancellationToken cancellationToken) =>
            Task.FromResult(id == Session.Id ? Session : null);

        public Task<ICollection<Session>> GetSessionsByHallAsync(int hallId, CancellationToken cancellationToken) =>
            Task.FromResult<ICollection<Session>>(new List<Session>());

        public Task AddSessionAsync(Session session, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public void UpdateSessionAsync(Session session) { }
        public void DeleteSessionAsync(Session session) { }
    }

    private sealed class FakeSeatRepository : ISeatRepository
    {
        public Seat Seat { get; set; } = null!;

        public Task<ICollection<Seat>> GetAllSeatsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<ICollection<Seat>>(new List<Seat> { Seat });

        public Task<Seat?> GetSeatByIdAsync(int id, CancellationToken cancellationToken) =>
            Task.FromResult(id == Seat.Id ? Seat : null);

        public Task AddSeatAsync(Seat seat, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public void UpdateSeatAsync(Seat seat) { }
        public void DeleteSeatAsync(Seat seat) { }
    }

    private sealed class FakeBookingRepository : IBookingRepository
    {
        public Booking? AddedBooking { get; private set; }
        public bool SeatTaken { get; set; }

        public Task<ICollection<Booking>> GetAllBookingsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<ICollection<Booking>>(AddedBooking is null
                ? new List<Booking>()
                : new List<Booking> { AddedBooking });

        public Task<ICollection<Booking>> GetBookingsByUserAsync(int userId, CancellationToken cancellationToken) =>
            Task.FromResult<ICollection<Booking>>(AddedBooking?.UserId == userId
                ? new List<Booking> { AddedBooking }
                : new List<Booking>());

        public Task<Booking?> GetBookingByIdAsync(int id, CancellationToken cancellationToken) =>
            Task.FromResult(AddedBooking?.Id == id ? AddedBooking : null);

        public Task AddBookingAsync(Booking booking, CancellationToken cancellationToken)
        {
            AddedBooking = booking;
            return Task.CompletedTask;
        }

        public void UpdateBookingAsync(Booking booking) { }
        public void DeleteBookingAsync(Booking booking) { }

        public Task<bool> HasActiveBookingAsync(
            int sessionId,
            int seatId,
            CancellationToken cancellationToken) => Task.FromResult(SeatTaken);
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveCount { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return Task.FromResult(1);
        }
    }
}
