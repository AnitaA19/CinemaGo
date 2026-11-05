namespace CinemaGo.Domain.DTOs.AuthDTOs;

public class AcceptAdminInvitationDTO
{
    public string Email { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;
}
