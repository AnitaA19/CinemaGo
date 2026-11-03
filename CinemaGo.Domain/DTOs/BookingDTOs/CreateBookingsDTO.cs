namespace CinemaGo.Domain.DTOs.BookingDTOs;

public class CreateBookingsDTO
{
    public int SessionId { get; set; }

    public List<int> SeatIds { get; set; } = [];
}
