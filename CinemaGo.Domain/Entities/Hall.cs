namespace CinemaGo.Domain.Entities;

public class Hall
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int Capacity { get; set; }

    public ICollection<Seat> Seats { get; set; } = new List<Seat>();

    public ICollection<Session> Sessions { get; set; } = new List<Session>();
}