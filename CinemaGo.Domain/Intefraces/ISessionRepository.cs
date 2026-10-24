using CinemaGo.Domain.DTOs.SessionDTOs;
using CinemaGo.Domain.Entities;

namespace CinemaGo.Domain.Intefraces;

public interface ISessionRepository
{
    Task<ICollection<Session>> GetAllSessionsAsync(
        CancellationToken cancellationToken);

    Task<Session?> GetSessionByIdAsync(
        int id,
        CancellationToken cancellationToken);

    Task<ICollection<Session>> GetSessionsByHallAsync(
        int hallId,
        CancellationToken cancellationToken);

    Task AddSessionAsync(
        Session session,
        CancellationToken cancellationToken);

    void UpdateSessionAsync(Session session);

    void DeleteSessionAsync(Session session);
}