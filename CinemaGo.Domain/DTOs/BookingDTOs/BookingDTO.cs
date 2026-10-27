namespace CinemaGo.Domain.DTOs.BookingDTOs;

public class BookingDTO
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public int SessionId { get; set; }

    public int SeatId { get; set; }

    public string MovieTitle { get; set; } = string.Empty;

    public DateTime SessionStartsAt { get; set; }

    public int RowNumber { get; set; }

    public int SeatNumber { get; set; }

    public bool IsVip { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public string? TicketCode { get; set; }

    public decimal? FinalPrice { get; set; }
}