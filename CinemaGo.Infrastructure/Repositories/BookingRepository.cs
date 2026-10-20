using CinemaGo.Domain.Entities;
using CinemaGo.Domain.Intefraces;
using CinemaGo.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CinemaGo.Infrastructure.Repositories;

public class BookingRepository : IBookingRepository
{
    private readonly CinemaDbContext _context;
    public BookingRepository(CinemaDbContext context)
    {
        _context = context;
    }
    public async Task<ICollection<Booking>> GetAllBookingsAsync(CancellationToken cancellationToken)
    {
        return await _context.Bookings.ToListAsync(cancellationToken);
    }

    public async Task<ICollection<Booking>> GetBookingByIdAsync(int id, CancellationToken cancellationToken)
    {
        return await _context.Bookings.Where(b => b.Id == id).ToListAsync(cancellationToken);
    }

    public async Task AddBookingAsync(Booking booking, CancellationToken cancellationToken)
    {
        await _context.Bookings.AddAsync(booking, cancellationToken);
    }

    public void UpdateBookingAsync(Booking booking)
    {
        _context.Bookings.Update(booking);
    }

    public void DeleteBookingAsync(Booking booking)
    {
        _context.Bookings.Remove(booking);
    }
}
