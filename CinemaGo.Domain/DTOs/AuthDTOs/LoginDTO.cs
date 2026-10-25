namespace CinemaGo.Domain.DTOs.AuthDTOs;

public class LoginDTO
{
    public string UsernameOrEmail { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}