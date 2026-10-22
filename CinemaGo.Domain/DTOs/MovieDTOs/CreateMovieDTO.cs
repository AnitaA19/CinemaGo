namespace CinemaGo.Domain.DTOs.MovieDTOs;

public class CreateMovieDTO
{
    public string Title { get; set; }
    public int DurationMinutes { get; set; }
    public int AgeRating { get; set; }
}
