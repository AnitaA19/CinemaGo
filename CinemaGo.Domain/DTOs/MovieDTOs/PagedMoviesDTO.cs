namespace CinemaGo.Domain.DTOs.MovieDTOs;

public class PagedMoviesDTO
{
    public ICollection<MovieDTO> Movies { get; set; } = new List<MovieDTO>();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
}
