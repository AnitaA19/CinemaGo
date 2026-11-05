using CinemaGo.Domain.Entities;
using CinemaGo.Domain.Intefraces;
using CinemaGo.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CinemaGo.Infrastructure.Repositories;

public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly CinemaDbContext _context;

    public RefreshTokenRepository(CinemaDbContext context)
    {
        _context = context;
    }

    public async Task<RefreshToken?> GetByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken) =>
        await _context.RefreshTokens
            .Include(token => token.User)
            .SingleOrDefaultAsync(
                token => token.TokenHash == tokenHash,
                cancellationToken);

    public async Task AddAsync(
        RefreshToken refreshToken,
        CancellationToken cancellationToken)
    {
        await _context.RefreshTokens.AddAsync(refreshToken, cancellationToken);
    }

    public async Task<bool> TryRotateAsync(
        RefreshToken currentToken,
        RefreshToken replacementToken,
        CancellationToken cancellationToken)
    {
        if (!currentToken.RevokedAtUtc.HasValue)
        {
            return false;
        }

        await _context.RefreshTokens.AddAsync(replacementToken, cancellationToken);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }
}
