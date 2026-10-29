using CinemaGo.Domain.Entities;

namespace CinemaGo.Domain.Intefraces;

public interface ISeatRepository
{
    Task<ICollection<Seat>> GetAllSeatsAsync(CancellationToken cancellationToken);
    Task<ICollection<Seat>> GetSeatsByHallIdAsync(int hallId, CancellationToken cancellationToken);
    Task<Seat?> GetSeatByIdAsync(int id, CancellationToken cancellationToken);
    Task AddSeatAsync(Seat seat, CancellationToken cancellationToken);
    void UpdateSeatAsync(Seat seat);
    void DeleteSeatAsync(Seat seat);
}
