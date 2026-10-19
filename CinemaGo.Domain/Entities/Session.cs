using CinemaGo.Domain.Enums;

namespace CinemaGo.Domain.Entities;

public class Session
{
    public int Id { get; set; }

    public int MovieId { get; set; }
    public Movie Movie { get; set; } = null!;

    public int HallId { get; set; }
    public Hall Hall { get; set; } = null!;

    public DateTime StartsAt { get; set; }

    public decimal StandardPrice { get; set; }

    public decimal VipPrice { get; set; }

    public SessionStatus Status { get; set; } = SessionStatus.Scheduled;

    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}