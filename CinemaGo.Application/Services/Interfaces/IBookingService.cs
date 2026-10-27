using CinemaGo.Domain.DTOs.BookingDTOs;

namespace CinemaGo.Application.Services.Interfaces;

public interface IBookingService
{
    Task<BookingDTO> BookTicketAsync(
        CreateBookingDTO dto,
        int userId,
        CancellationToken cancellationToken);

    Task<BookingDTO?> GetBookingByIdAsync(
        int id,
        CancellationToken cancellationToken);

    Task<BookingDTO?> GetUserBookingByIdAsync(
        int id,
        int userId,
        CancellationToken cancellationToken);

    Task<ICollection<BookingDTO>> GetUserBookingsAsync(
        int userId,
        CancellationToken cancellationToken);

    Task<ICollection<BookingDTO>> GetAllBookingsAsync(
        CancellationToken cancellationToken);

    Task CancelBookingAsync(
        int id,
        int userId,
        bool isAdmin,
        CancellationToken cancellationToken);
}