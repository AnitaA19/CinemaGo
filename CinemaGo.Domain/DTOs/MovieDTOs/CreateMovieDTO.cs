using System.ComponentModel.DataAnnotations;

namespace CinemaGo.Domain.DTOs.MovieDTOs;

public class CreateMovieDTO
{
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Title { get; set; } = string.Empty;

    [Range(1, 1000)]
    public int DurationMinutes { get; set; }

    [Range(0, 21)]
    public int AgeRating { get; set; }
}