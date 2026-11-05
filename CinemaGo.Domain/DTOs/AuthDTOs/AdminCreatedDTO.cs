namespace CinemaGo.Domain.DTOs.AuthDTOs;

public class AdminCreatedDTO
{
    public int UserId { get; set; }

    public string Username { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;
}
