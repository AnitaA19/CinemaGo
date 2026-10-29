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

public async Task<(ICollection<Movie> Movies, int TotalCount)> GetMoviesPagedAsync(
        string? title, int? genreId, int page, int pageSize, CancellationToken cancellationToken)
    {
        IQueryable<Movie> query = _context.Movies.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(title))
        {
            query = query.Where(m => EF.Functions.Like(m.Title, $"%{title}%"));
        }

        if (genreId.HasValue)
        {
            query = query.Where(m => m.MovieGenres.Any(mg => mg.GenreId == genreId.Value));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var movies = await query
            .OrderBy(m => m.Title)
            .ThenBy(m => m.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (movies, totalCount);
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
