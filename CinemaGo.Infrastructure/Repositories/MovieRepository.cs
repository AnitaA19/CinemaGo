using CinemaGo.Domain.DTOs.MovieDTOs;
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
        return await _context.Movies
            .Include(m => m.Genres)
            .ToListAsync(cancellationToken);
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
            query = query.Where(m => m.Genres.Any(genre => genre.Id == genreId.Value));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var movies = await query
            .Include(m => m.Genres)
            .OrderBy(m => m.Title)
            .ThenBy(m => m.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (movies, totalCount);
    }

    public async Task<Movie?> GetMovieByIdAsync(int id, CancellationToken cancellationToken)
    {
        return await _context.Movies
            .Include(m => m.Genres)
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
    }

    public async Task AddMovieAsync(Movie movie, CancellationToken cancellationToken)
    {
        await _context.Movies.AddAsync(movie, cancellationToken);
    }

    public void UpdateMovieAsync(Movie movie)
    {
        // Tracked movies (with their genre links) are picked up by change tracking; Update would mark new links as modified.
        if (_context.Entry(movie).State == EntityState.Detached)
        {
            _context.Movies.Update(movie);
        }
    }

    public void DeleteMovieAsync(Movie movie)
    {
        _context.Movies.Remove(movie);
    }

    public async Task<ICollection<MovieSalesDTO>> GetSalesByMovieAsync(CancellationToken cancellationToken)
    {
        return await _context.Tickets
            .Where(ticket => ticket.Booking.Status == CinemaGo.Domain.Enums.BookingStatus.Active)
            .GroupBy(ticket => new { ticket.Booking.Session.MovieId, ticket.Booking.Session.Movie.Title })
            .Select(group => new MovieSalesDTO
            {
                MovieId = group.Key.MovieId,
                Title = group.Key.Title,
                TicketCount = group.Count(),
                Revenue = group.Sum(ticket => ticket.FinalPrice)
            })
            .OrderByDescending(sales => sales.Revenue)
            .ThenBy(sales => sales.Title)
            .ToListAsync(cancellationToken);
    }
}


