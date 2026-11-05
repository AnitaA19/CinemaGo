namespace CinemaGo.Domain.Intefraces;

public interface IAdminInvitationEmailSender
{
    Task SendAsync(
        string recipient,
        string code,
        string acceptLink,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken);
}
