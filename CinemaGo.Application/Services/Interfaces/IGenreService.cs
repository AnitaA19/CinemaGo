using CinemaGo.Domain.DTOs.GenreDTOs;

namespace CinemaGo.Application.Services.Interfaces;

public interface IGenreService
{
    Task<ICollection<GenreDTO>> GetAllGenresAsync(CancellationToken cancellationToken);
    Task<GenreDTO?> GetGenreByIdAsync(int id, CancellationToken cancellationToken);
    Task<GenreDTO> AddGenreAsync(CreateGenreDTO dto, CancellationToken cancellationToken);
    Task<GenreDTO?> UpdateGenreAsync(int id, UpdateGenreDTO dto, CancellationToken cancellationToken);
    Task<bool> DeleteGenreAsync(int id, CancellationToken cancellationToken);
}
