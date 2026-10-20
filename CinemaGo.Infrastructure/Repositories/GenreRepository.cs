using CinemaGo.Domain.Entities;
using CinemaGo.Domain.Intefraces;
using CinemaGo.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CinemaGo.Infrastructure.Repositories;

public class GenreRepository : IGenreRepository
{
    private readonly CinemaDbContext _context;
    public GenreRepository(CinemaDbContext context)
    {
        _context = context;
    }

    public async Task<ICollection<Genre>> GetAllGenresAsync(CancellationToken cancellationToken)
    {
        return await _context.Genres.ToListAsync(cancellationToken);
    }

    public async Task<ICollection<Genre>> GetGenreByIdAsync(int id, CancellationToken cancellationToken)
    {
        return await _context.Genres.Where(g => g.Id == id).ToListAsync(cancellationToken);
    }

    public async Task AddGenreAsync(Genre genre, CancellationToken cancellationToken)
    {
        await _context.Genres.AddAsync(genre, cancellationToken);
    }

    public void UpdateGenreAsync(Genre genre)
    {
        _context.Genres.Update(genre);
    }

    public void DeleteGenreAsync(Genre genre)
    {
        _context.Genres.Remove(genre);
    }
}
