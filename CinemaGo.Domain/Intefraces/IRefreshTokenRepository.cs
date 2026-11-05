using CinemaGo.Domain.Entities;

namespace CinemaGo.Domain.Intefraces;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken);

    Task AddAsync(RefreshToken refreshToken, CancellationToken cancellationToken);

    Task<bool> TryRotateAsync(
        RefreshToken currentToken,
        RefreshToken replacementToken,
        CancellationToken cancellationToken);
}
