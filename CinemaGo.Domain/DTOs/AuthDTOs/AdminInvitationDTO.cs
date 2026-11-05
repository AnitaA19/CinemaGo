namespace CinemaGo.Domain.DTOs.AuthDTOs;

public class AdminInvitationDTO
{
    public int InvitationId { get; set; }

    public string Email { get; set; } = string.Empty;

    public DateTime ExpiresAtUtc { get; set; }
}
