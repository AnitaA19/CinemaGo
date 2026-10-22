using CinemaGo.Domain.Entities;

namespace CinemaGo.Domain.Intefraces;

public interface IBookingRepository
{
    Task<ICollection<Booking>> GetAllBookingsAsync(CancellationToken cancellationToken);
    Task<Booking?> GetBookingByIdAsync(int id, CancellationToken cancellationToken);
    Task AddBookingAsync(Booking booking, CancellationToken cancellationToken);
    void UpdateBookingAsync(Booking booking);
    void DeleteBookingAsync(Booking booking);
}
