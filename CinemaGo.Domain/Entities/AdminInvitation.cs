namespace CinemaGo.Domain.Entities;

public class AdminInvitation
{
    public int Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public string CodeHash { get; set; } = string.Empty;

    public int CreatedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    public int FailedAttempts { get; set; }

    public DateTime? AcceptedAtUtc { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    public User CreatedByUser { get; set; } = null!;
}
