using CinemaGo.Application.Services.Implementations;
using CinemaGo.Domain.Entities;
using CinemaGo.Domain.Exceptions;
using CinemaGo.Domain.Intefraces;
using Microsoft.Extensions.Logging.Abstractions;

namespace CinemaGo.Tests;

public class TicketServiceTests
{
    private const int TicketId = 12;
    private const int OwnerId = 7;
    private const int OtherUserId = 8;

    private readonly FakeTicketRepository _ticketRepository = new();
    private readonly TicketService _service;

    public TicketServiceTests()
    {
        _service = new TicketService(
            _ticketRepository,
            NullLogger<TicketService>.Instance);
    }

    [Fact]
    public async Task GetTicketById_ExistingTicket_ReturnsTicketDetails()
    {
        var startsAt = new DateTime(2026, 11, 15, 19, 30, 0, DateTimeKind.Utc);
        _ticketRepository.Ticket = CreateTicket(OwnerId, startsAt);

        var result = await _service.GetTicketByIdAsync(
            TicketId,
            OwnerId,
            isAdmin: false,
            CancellationToken.None);

        Assert.Equal("TICKET-12", result.TicketCode);
        Assert.Equal(25.50m, result.FinalPrice);
        Assert.Equal("Dune", result.MovieTitle);
        Assert.Equal(startsAt, result.SessionStartsAt);
        Assert.Equal("Hall 3", result.HallName);
        Assert.Equal(4, result.RowNumber);
        Assert.Equal(5, result.SeatNumber);
    }

    [Fact]
    public async Task GetTicketById_TicketDoesNotExist_ThrowsNotFoundException()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetTicketByIdAsync(
            TicketId,
            OwnerId,
            isAdmin: false,
            CancellationToken.None));
    }

    [Fact]
    public async Task GetTicketById_CustomerRequestsOwnTicket_ReturnsTicket()
    {
        _ticketRepository.Ticket = CreateTicket(OwnerId);

        var result = await _service.GetTicketByIdAsync(
            TicketId,
            OwnerId,
            isAdmin: false,
            CancellationToken.None);

        Assert.Equal(TicketId, result.Id);
        Assert.Equal("TICKET-12", result.TicketCode);
    }

    [Fact]
    public async Task GetTicketById_CustomerRequestsAnotherUsersTicket_ThrowsForbiddenException()
    {
        _ticketRepository.Ticket = CreateTicket(OwnerId);

        await Assert.ThrowsAsync<ForbiddenException>(() => _service.GetTicketByIdAsync(
            TicketId,
            OtherUserId,
            isAdmin: false,
            CancellationToken.None));
    }

    [Fact]
    public async Task GetTicketById_AdminRequestsAnotherUsersTicket_ReturnsTicket()
    {
        _ticketRepository.Ticket = CreateTicket(OwnerId);

        var result = await _service.GetTicketByIdAsync(
            TicketId,
            OtherUserId,
            isAdmin: true,
            CancellationToken.None);

        Assert.Equal(TicketId, result.Id);
        Assert.Equal("TICKET-12", result.TicketCode);
    }

    private static Ticket CreateTicket(int ownerId, DateTime? startsAt = null)
    {
        var hall = new Hall { Id = 3, Name = "Hall 3" };
        var movie = new Movie { Id = 5, Title = "Dune" };
        var seat = new Seat
        {
            Id = 9,
            RowNumber = 4,
            SeatNumber = 5,
            IsVip = true
        };
        var session = new Session
        {
            Id = 6,
            StartsAt = startsAt ?? new DateTime(2026, 11, 15, 19, 30, 0, DateTimeKind.Utc),
            Movie = movie,
            Hall = hall
        };
        var booking = new Booking
        {
            Id = 10,
            UserId = ownerId,
            Session = session,
            Seat = seat
        };

        return new Ticket
        {
            Id = TicketId,
            BookingId = booking.Id,
            Booking = booking,
            TicketCode = "TICKET-12",
            FinalPrice = 25.50m
        };
    }

    private sealed class FakeTicketRepository : ITicketRepository
    {
        public Ticket? Ticket { get; set; }

        public Task<ICollection<Ticket>> GetAllTicketsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<ICollection<Ticket>>(Ticket is null
                ? new List<Ticket>()
                : new List<Ticket> { Ticket });

        public Task<Ticket?> GetTicketByIdAsync(int id, CancellationToken cancellationToken) =>
            Task.FromResult(Ticket?.Id == id ? Ticket : null);

        public Task AddTicketAsync(Ticket ticket, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public void UpdateTicketAsync(Ticket ticket) { }

        public void DeleteTicketAsync(Ticket ticket) { }
    }
}
