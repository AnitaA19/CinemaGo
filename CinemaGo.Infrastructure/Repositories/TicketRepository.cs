using CinemaGo.Domain.Entities;
using CinemaGo.Domain.Intefraces;
using CinemaGo.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CinemaGo.Infrastructure.Repositories;

public class TicketRepository : ITicketRepository
{
    private readonly CinemaDbContext _context;
    public TicketRepository(CinemaDbContext context)
    {
        _context = context;
    }

    public async Task<ICollection<Ticket>> GetAllTicketsAsync(CancellationToken cancellationToken)
    {
        return await _context.Tickets.ToListAsync(cancellationToken);
    }

    public async Task<Ticket?> GetTicketByIdAsync(int id, CancellationToken cancellationToken)
    {
        return await _context.Tickets
            .Include(t => t.Booking).ThenInclude(b => b.Session).ThenInclude(s => s.Movie)
            .Include(t => t.Booking).ThenInclude(b => b.Session).ThenInclude(s => s.Hall)
            .Include(t => t.Booking).ThenInclude(b => b.Seat)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task AddTicketAsync(Ticket ticket, CancellationToken cancellationToken)
    {
        await _context.Tickets.AddAsync(ticket, cancellationToken);
    }

    public void UpdateTicketAsync(Ticket ticket)
    {
        _context.Tickets.Update(ticket);
    }

    public void DeleteTicketAsync(Ticket ticket)
    {
        _context.Tickets.Remove(ticket);
    }
}
