namespace CinemaGo.Domain.Intefraces;

public interface ISessionCancellationEmailSender
{
    Task SendAsync(
        IEnumerable<string> recipients,
        int sessionId,
        string movieTitle,
        DateTime startsAt,
        CancellationToken cancellationToken);
}
