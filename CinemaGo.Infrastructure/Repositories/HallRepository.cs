using CinemaGo.Domain.Entities;
using CinemaGo.Domain.Intefraces;
using CinemaGo.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CinemaGo.Infrastructure.Repositories;

public class HallRepository : IHallRepository
{
    private readonly CinemaDbContext _context;
    public HallRepository(CinemaDbContext context)
    {
        _context = context;
    }

    public async Task<ICollection<Hall>> GetAllHallsAsync(CancellationToken cancellationToken)
    {
        return await _context.Halls.ToListAsync(cancellationToken);
    }

    public async Task<Hall?> GetHallByIdAsync(int id, CancellationToken cancellationToken)
    {
        return await _context.Halls.FirstOrDefaultAsync(h => h.Id == id, cancellationToken);
    }

    public async Task AddHallAsync(Hall hall, CancellationToken cancellationToken)
    {
        await _context.Halls.AddAsync(hall, cancellationToken);
    }

    public void UpdateHallAsync(Hall hall)
    {
        _context.Halls.Update(hall);
    }

    public void DeleteHallAsync(Hall hall)
    {
        _context.Halls.Remove(hall);
    }
}
