namespace CinemaGo.Domain.DTOs.SessionDTOs;

public class SeatAvailabilityDTO
{
    public int SeatId { get; set; }
    public int RowNumber { get; set; }
    public int SeatNumber { get; set; }
    public bool IsVip { get; set; }
    public bool IsAvailable { get; set; }
}
