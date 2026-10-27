using CinemaGo.Domain.Entities;
using CinemaGo.Domain.Enums;
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
        return await _context.Bookings
            .Include(booking => booking.Session)
                .ThenInclude(session => session.Movie)
            .Include(booking => booking.Seat)
            .Include(booking => booking.Ticket)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<ICollection<Booking>> GetBookingsByUserAsync(int userId, CancellationToken cancellationToken)
    {
        return await _context.Bookings
            .Where(booking => booking.UserId == userId)
            .Include(booking => booking.Session)
                .ThenInclude(session => session.Movie)
            .Include(booking => booking.Seat)
            .Include(booking => booking.Ticket)
            .AsNoTracking()
            .OrderByDescending(booking => booking.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<Booking?> GetBookingByIdAsync(int id, CancellationToken cancellationToken)
    {
        return await _context.Bookings
            .Include(booking => booking.Session)
                .ThenInclude(session => session.Movie)
            .Include(booking => booking.Seat)
            .Include(booking => booking.Ticket)
            .FirstOrDefaultAsync(booking => booking.Id == id, cancellationToken);
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

    public async Task<bool> HasActiveBookingAsync(
    int sessionId,
    int seatId,
    CancellationToken cancellationToken)
    {
        return await _context.Bookings.AnyAsync(
            booking =>
                booking.SessionId == sessionId &&
                booking.SeatId == seatId &&
                booking.Status == BookingStatus.Active,
            cancellationToken);
    }
}
