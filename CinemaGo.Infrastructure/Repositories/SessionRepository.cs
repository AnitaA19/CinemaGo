using CinemaGo.Domain.Entities;
using CinemaGo.Domain.Intefraces;
using CinemaGo.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CinemaGo.Infrastructure.Repositories;

public class SessionRepository : ISessionRepository
{
    private readonly CinemaDbContext _context;

    public SessionRepository(CinemaDbContext context)
    {
        _context = context;
    }

    public async Task<ICollection<Session>> GetAllSessionsAsync(
        CancellationToken cancellationToken)
    {
        return await _context.Sessions
            .Include(s => s.Movie)
            .Include(s => s.Hall)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<Session?> GetSessionByIdAsync(
        int id,
        CancellationToken cancellationToken)
    {
        return await _context.Sessions
     .Include(s => s.Movie)
     .Include(s => s.Hall)
     .Include(s => s.Bookings)
     .FirstOrDefaultAsync(
         s => s.Id == id,
         cancellationToken);
    }

    public async Task<ICollection<Session>> GetSessionsByHallAsync(
        int hallId,
        CancellationToken cancellationToken)
    {
        return await _context.Sessions
            .Include(s => s.Movie)
            .Where(s => s.HallId == hallId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task AddSessionAsync(
        Session session,
        CancellationToken cancellationToken)
    {
        await _context.Sessions.AddAsync(session, cancellationToken);
    }

    public void UpdateSessionAsync(Session session)
    {
        _context.Sessions.Update(session);
    }

    public void DeleteSessionAsync(Session session)
    {
        _context.Sessions.Remove(session);
    }
}