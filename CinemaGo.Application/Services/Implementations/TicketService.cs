using CinemaGo.Application.Services.Interfaces;
using CinemaGo.Domain.DTOs.TicketDTOs;
using CinemaGo.Domain.Exceptions;
using CinemaGo.Domain.Intefraces;
using Microsoft.Extensions.Logging;

namespace CinemaGo.Application.Services.Implementations;

public class TicketService : ITicketService
{
    private readonly ITicketRepository _ticketRepository;
    private readonly ILogger<TicketService> _logger;

    public TicketService(ITicketRepository ticketRepository, ILogger<TicketService> logger)
    {
        _ticketRepository = ticketRepository;
        _logger = logger;
    }

    public async Task<TicketDTO> GetTicketByIdAsync(
        int id,
        int userId,
        bool isAdmin,
        CancellationToken cancellationToken)
    {
        var ticket = await _ticketRepository.GetTicketByIdAsync(id, cancellationToken);

        if (ticket is null)
        {
            _logger.LogWarning("Ticket {TicketId} not found", id);
            throw new NotFoundException($"Ticket with ID {id} not found.");
        }

        // Ownership is checked through the related booking.
        if (!isAdmin && ticket.Booking.UserId != userId)
        {
            _logger.LogWarning(
                "User {UserId} tried to access ticket {TicketId} of another user", userId, id);
            throw new ForbiddenException("You cannot access another user's ticket.");
        }

        var booking = ticket.Booking;

        return new TicketDTO
        {
            Id = ticket.Id,
            BookingId = ticket.BookingId,
            TicketCode = ticket.TicketCode,
            FinalPrice = ticket.FinalPrice,
            MovieTitle = booking.Session.Movie.Title,
            SessionStartsAt = booking.Session.StartsAt,
            HallName = booking.Session.Hall.Name,
            RowNumber = booking.Seat.RowNumber,
            SeatNumber = booking.Seat.SeatNumber,
            IsVip = booking.Seat.IsVip
        };
    }
}
