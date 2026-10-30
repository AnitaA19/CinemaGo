using CinemaGo.Domain.DTOs.SessionDTOs;
using CinemaGo.Domain.Entities;

namespace CinemaGo.Domain.Intefraces;

public interface ISessionRepository
{
    Task<ICollection<Session>> GetAllSessionsAsync(
        CancellationToken cancellationToken);

    Task<(ICollection<Session> Sessions, int TotalCount)> GetSessionsPagedAsync(
        int? movieId,
        DateTime? dateFrom,
        DateTime? dateTo,
        int page,
        int pageSize,
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