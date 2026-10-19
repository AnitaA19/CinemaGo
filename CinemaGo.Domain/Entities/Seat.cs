namespace CinemaGo.Domain.Entities;

public class Seat
{
    public int Id { get; set; }

    public int HallId { get; set; }
    public Hall Hall { get; set; } = null!;

    public int RowNumber { get; set; }

    public int SeatNumber { get; set; }

    public bool IsVip { get; set; }

    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}