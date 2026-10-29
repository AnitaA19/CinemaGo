using CinemaGo.Domain.Entities;

namespace CinemaGo.Domain.Intefraces;

public interface IMovieRepository
{
    Task<ICollection<Movie>> GetAllMoviesAsync(CancellationToken cancellationToken);
    Task<(ICollection<Movie> Movies, int TotalCount)> GetMoviesPagedAsync(string? title, int? genreId, int page, int pageSize, CancellationToken cancellationToken);
    Task<Movie?> GetMovieByIdAsync(
      int id,
      CancellationToken cancellationToken);
    Task AddMovieAsync(Movie movie, CancellationToken cancellationToken);
    void UpdateMovieAsync(Movie movie);
    void DeleteMovieAsync(Movie movie);
}
