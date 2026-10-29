namespace CinemaGo.Domain.DTOs.SessionDTOs;

public class CreateSessionDTO
{
    public int MovieId { get; set; }

    public int HallId { get; set; }

    /// <summary>Start time in UTC (e.g. 2030-01-01T18:00:00Z). Must be in the future.</summary>
    public DateTime StartsAt { get; set; }

    public decimal StandardPrice { get; set; }

    public decimal VipPrice { get; set; }
}
