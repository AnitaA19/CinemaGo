namespace CinemaGo.Domain.DTOs.MovieDTOs;

public class CreateMovieDTO
{
    public string Title { get; set; } = string.Empty;

    public int DurationMinutes { get; set; }

    public int AgeRating { get; set; }

    public string? PosterUrl { get; set; }

    public List<int> GenreIds { get; set; } = new();
}
