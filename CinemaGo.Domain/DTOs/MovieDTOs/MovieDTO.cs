using CinemaGo.Domain.DTOs.GenreDTOs;

namespace CinemaGo.Domain.DTOs.MovieDTOs
{
    public class MovieDTO
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public int DurationMinutes { get; set; }
        public int AgeRating { get; set; }
        public List<GenreDTO> Genres { get; set; } = new();
    }
}
