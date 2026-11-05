using CinemaGo.Domain.Entities;
using CinemaGo.Domain.Intefraces;
using CinemaGo.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CinemaGo.Infrastructure.Repositories;

public class AdminInvitationRepository : IAdminInvitationRepository
{
    private readonly CinemaDbContext _context;

    public AdminInvitationRepository(CinemaDbContext context)
    {
        _context = context;
    }

    public async Task<AdminInvitation?> GetActiveByEmailAsync(
        string email,
        CancellationToken cancellationToken) =>
        await _context.AdminInvitations
            .SingleOrDefaultAsync(
                invitation => invitation.Email == email
                    && invitation.AcceptedAtUtc == null
                    && invitation.RevokedAtUtc == null,
                cancellationToken);

    public async Task AddAsync(
        AdminInvitation invitation,
        CancellationToken cancellationToken)
    {
        await _context.AdminInvitations.AddAsync(invitation, cancellationToken);
    }
}
