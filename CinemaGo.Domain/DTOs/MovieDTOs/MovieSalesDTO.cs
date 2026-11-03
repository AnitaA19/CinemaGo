namespace CinemaGo.Domain.DTOs.MovieDTOs;

public class MovieSalesDTO
{
    public int MovieId { get; set; }

    public string Title { get; set; } = string.Empty;

    public int TicketCount { get; set; }

    public decimal Revenue { get; set; }
}
