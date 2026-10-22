using CinemaGo.Domain.Entities;

namespace CinemaGo.Domain.Intefraces;

public interface ITicketRepository
{
    Task<ICollection<Ticket>> GetAllTicketsAsync(CancellationToken cancellationToken);
    Task<Ticket?> GetTicketByIdAsync(int id, CancellationToken cancellationToken);
    Task AddTicketAsync(Ticket ticket, CancellationToken cancellationToken);
    void UpdateTicketAsync(Ticket ticket);
    void DeleteTicketAsync(Ticket ticket);
}
