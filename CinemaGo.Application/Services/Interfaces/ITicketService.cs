using CinemaGo.Domain.DTOs.TicketDTOs;

namespace CinemaGo.Application.Services.Interfaces;

public interface ITicketService
{
    /// <exception cref="CinemaGo.Domain.Exceptions.NotFoundException">Ticket does not exist.</exception>
    /// <exception cref="CinemaGo.Domain.Exceptions.ForbiddenException">Non-admin requests another user's ticket.</exception>
    Task<TicketDTO> GetTicketByIdAsync(
        int id,
        int userId,
        bool isAdmin,
        CancellationToken cancellationToken);
}
