using System.ComponentModel.DataAnnotations;

namespace CinemaGo.Domain.DTOs.AuthDTOs;

public class RegisterDTO
{
    [Required]
    [StringLength(100, MinimumLength = 3)]
    [RegularExpression(@"^[A-Za-z0-9_.-]+$",
        ErrorMessage = "Username may contain only letters, digits, '_', '.' and '-'.")]
    public string Username { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(128, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;
}