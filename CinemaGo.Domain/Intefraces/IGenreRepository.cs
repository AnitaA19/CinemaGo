using CinemaGo.Domain.Entities;

namespace CinemaGo.Domain.Intefraces;

public interface IGenreRepository
{
    Task<ICollection<Genre>> GetAllGenresAsync(CancellationToken cancellationToken);
    Task<Genre?> GetGenreByIdAsync(int id, CancellationToken cancellationToken);
    Task AddGenreAsync(Genre genre, CancellationToken cancellationToken);
    void UpdateGenreAsync(Genre genre);
    void DeleteGenreAsync(Genre genre);
}
