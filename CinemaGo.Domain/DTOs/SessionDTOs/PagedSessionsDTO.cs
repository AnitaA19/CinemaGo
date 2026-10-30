namespace CinemaGo.Domain.DTOs.SessionDTOs;

public class PagedSessionsDTO
{
    public ICollection<SessionDTO> Sessions { get; set; } = new List<SessionDTO>();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
}
