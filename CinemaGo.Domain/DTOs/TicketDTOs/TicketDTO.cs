namespace CinemaGo.Domain.DTOs.TicketDTOs;

public class TicketDTO
{
    public int Id { get; set; }
    public int BookingId { get; set; }
    public string TicketCode { get; set; } = string.Empty;
    public decimal FinalPrice { get; set; }
    public string MovieTitle { get; set; } = string.Empty;
    public DateTime SessionStartsAt { get; set; }
    public string HallName { get; set; } = string.Empty;
    public int RowNumber { get; set; }
    public int SeatNumber { get; set; }
    public bool IsVip { get; set; }
}
