using CinemaGo.Domain.DTOs.MovieDTOs;

namespace CinemaGo.Application.Services.Interfaces;

public interface IMovieService
{
    Task<ICollection<MovieDTO>> GetAllMoviesAsync(CancellationToken cancellationToken);
    Task<PagedMoviesDTO> GetMoviesAsync(string? title, int? genreId, int page, int pageSize, CancellationToken cancellationToken);
    Task<MovieDTO?> GetMovieByIdAsync(int id, CancellationToken cancellationToken);
    Task<MovieDTO> AddMovieAsync(CreateMovieDTO createMovieDto, CancellationToken cancellationToken);
    Task<MovieDTO?> UpdateMovieAsync(int id, UpdateMovieDTO updateMovieDto, CancellationToken cancellationToken);
    Task<bool> DeleteMovieAsync(int id, CancellationToken cancellationToken);
}
