using CinemaGo.Domain.Entities;

namespace CinemaGo.Domain.Intefraces;

public interface IHallRepository
{
    Task<ICollection<Hall>> GetAllHallsAsync(CancellationToken cancellationToken);
    Task<Hall?> GetHallByIdAsync(int id, CancellationToken cancellationToken);
    Task AddHallAsync(Hall hall, CancellationToken cancellationToken);
    void UpdateHallAsync(Hall hall);
    void DeleteHallAsync(Hall hall);
}
