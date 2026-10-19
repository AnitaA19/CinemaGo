namespace CinemaGo.Domain.Entities;

public class Movie
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public int DurationMinutes { get; set; }

    public int AgeRating { get; set; }

    public ICollection<MovieGenre> MovieGenres { get; set; } = new List<MovieGenre>();

    public ICollection<Session> Sessions { get; set; } = new List<Session>();
}