using CinemaGo.Domain.Entities;

namespace CinemaGo.Domain.Intefraces;

public interface IAdminInvitationRepository
{
    Task<AdminInvitation?> GetActiveByEmailAsync(
        string email,
        CancellationToken cancellationToken);

    Task AddAsync(AdminInvitation invitation, CancellationToken cancellationToken);
}
