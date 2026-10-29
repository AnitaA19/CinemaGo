using CinemaGo.Domain.Entities;
using CinemaGo.Domain.Intefraces;
using CinemaGo.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CinemaGo.Infrastructure.Repositories;

public class SeatRepository : ISeatRepository
{
    private readonly CinemaDbContext _context;
    public SeatRepository(CinemaDbContext context)
    {
        _context = context;
    }

    public async Task<ICollection<Seat>> GetAllSeatsAsync(CancellationToken cancellationToken)
    {
        return await _context.Seats.ToListAsync(cancellationToken);
    }

    public async Task<ICollection<Seat>> GetSeatsByHallIdAsync(int hallId, CancellationToken cancellationToken)
    {
        return await _context.Seats
            .Where(s => s.HallId == hallId)
            .OrderBy(s => s.RowNumber)
            .ThenBy(s => s.SeatNumber)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<Seat?> GetSeatByIdAsync(int id, CancellationToken cancellationToken)
    {
        return await _context.Seats.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task AddSeatAsync(Seat seat, CancellationToken cancellationToken)
    {
        await _context.Seats.AddAsync(seat, cancellationToken);
    }

    public void UpdateSeatAsync(Seat seat)
    {
        _context.Seats.Update(seat);
    }

    public void DeleteSeatAsync(Seat seat)
    {
        _context.Seats.Remove(seat);
    }
}
