using CinemaGo.Domain.DTOs.SessionDTOs;

namespace CinemaGo.Application.Services.Interfaces;

public interface ISessionService
{
    Task<ICollection<SessionDTO>> GetAllSessionsAsync(
        CancellationToken cancellationToken);

    Task<SessionDTO?> GetSessionByIdAsync(
        int id,
        CancellationToken cancellationToken);

    Task<ICollection<SeatAvailabilityDTO>?> GetSessionSeatsAsync(
        int id,
        CancellationToken cancellationToken);

    Task<SessionDTO> AddSessionAsync(
        CreateSessionDTO dto,
        CancellationToken cancellationToken);

    Task<SessionDTO?> UpdateSessionAsync(
        int id,
        UpdateSessionDTO dto,
        CancellationToken cancellationToken);

    Task<bool> DeleteSessionAsync(
        int id,
        CancellationToken cancellationToken);
}