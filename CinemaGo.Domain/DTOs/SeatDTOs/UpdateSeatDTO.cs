namespace CinemaGo.Domain.DTOs.SeatDTOs;

public class UpdateSeatDTO
{
    public int RowNumber { get; set; }

    public int SeatNumber { get; set; }

    public bool IsVip { get; set; }
}
