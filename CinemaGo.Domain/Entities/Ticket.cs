namespace CinemaGo.Domain.Entities;

public class Ticket
{
    public int Id { get; set; }
    public int BookingId { get; set; }
    public Booking Booking { get; set; } = null!;
    public string TicketCode { get; set; } = string.Empty;
    public decimal FinalPrice { get; set; }

}
