using System.ComponentModel.DataAnnotations;

namespace CinemaGo.Domain.DTOs.AuthDTOs;

public class LoginDTO
{
    [Required]
    [StringLength(256)]
    public string UsernameOrEmail { get; set; } = string.Empty;

    [Required]
    [StringLength(128)]
    public string Password { get; set; } = string.Empty;
}