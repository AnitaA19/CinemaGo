using CinemaGo.Domain.Entities;
using CinemaGo.Domain.Intefraces;
using CinemaGo.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CinemaGo.Infrastructure.Repositories;

public class MovieRepository : IMovieRepository
{
    private readonly CinemaDbContext _context;
    public MovieRepository(CinemaDbContext context)
    {
        _context = context;
    }

    public async Task<ICollection<Movie>> GetAllMoviesAsync(CancellationToken cancellationToken)
    {
        return await _context.Movies.ToListAsync(cancellationToken);
    }

    public async Task<Movie?> GetMovieByIdAsync(int id, CancellationToken cancellationToken)
    {
        return await _context.Movies.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
    }

    public async Task AddMovieAsync(Movie movie, CancellationToken cancellationToken)
    {
        await _context.Movies.AddAsync(movie, cancellationToken);
    }

    public void UpdateMovieAsync(Movie movie)
    {
        _context.Movies.Update(movie);
    }

    public void DeleteMovieAsync(Movie movie)
    {
        _context.Movies.Remove(movie);
    }
}
