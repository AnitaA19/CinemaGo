namespace CinemaGo.Domain.DTOs.SeatDTOs;

public class SeatDTO
{
    public int Id { get; set; }

    public int HallId { get; set; }

    public int RowNumber { get; set; }

    public int SeatNumber { get; set; }

    public bool IsVip { get; set; }
}
