using AutoMapper;
using CinemaGo.Application.Services.Interfaces;
using CinemaGo.Domain.DTOs.BookingDTOs;
using CinemaGo.Domain.Entities;
using CinemaGo.Domain.Enums;
using CinemaGo.Domain.Exceptions;
using CinemaGo.Domain.Intefraces;

namespace CinemaGo.Application.Services.Implementations;

public class BookingService : IBookingService
{
    private readonly IBookingRepository _bookingRepository;
    private readonly ISessionRepository _sessionRepository;
    private readonly ISeatRepository _seatRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public BookingService(
        IBookingRepository bookingRepository,
        ISessionRepository sessionRepository,
        ISeatRepository seatRepository,
        IUnitOfWork unitOfWork,
        IMapper mapper)
    {
        _bookingRepository = bookingRepository;
        _sessionRepository = sessionRepository;
        _seatRepository = seatRepository;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<BookingDTO> BookTicketAsync(
        CreateBookingDTO dto,
        int userId,
        CancellationToken cancellationToken)
    {
        // 1. Find the session.
        var session = await _sessionRepository.GetSessionByIdAsync(
            dto.SessionId,
            cancellationToken);

        if (session is null)
        {
            throw new NotFoundException(
                $"Session with ID {dto.SessionId} not found.");
        }

        // 2. Find the selected seat.
        var seat = await _seatRepository.GetSeatByIdAsync(
            dto.SeatId,
            cancellationToken);

        if (seat is null)
        {
            throw new NotFoundException(
                $"Seat with ID {dto.SeatId} not found.");
        }

        // 3. Check that the seat belongs to the session's hall.
        if (seat.HallId != session.HallId)
        {
            throw new ValidationException(
                "The selected seat does not belong to the session's hall.");
        }

        // 4. Only scheduled sessions that have not started can be booked.
        if (session.Status != SessionStatus.Scheduled)
        {
            throw new ValidationException(
                "You cannot book a ticket for a session that is not scheduled.");
        }

        if (session.StartsAt <= DateTime.UtcNow)
        {
            throw new ValidationException(
                "You cannot book a ticket for a session that has already started.");
        }

        // 5. Check whether the seat is already booked.
        var isBooked = await _bookingRepository.HasActiveBookingAsync(
            session.Id,
            seat.Id,
            cancellationToken);

        if (isBooked)
        {
            throw new ConflictException(
                "This seat has already been booked for this session.");
        }

        // 6. Calculate the ticket price on the server.
        var finalPrice = seat.IsVip
            ? session.VipPrice
            : session.StandardPrice;

        // 7. Create the booking.
        var booking = new Booking
        {
            UserId = userId,
            SessionId = session.Id,
            SeatId = seat.Id,
            Status = BookingStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        // 8. Create a ticket linked to this booking.
        var ticket = new Ticket
        {
            Booking = booking,
            TicketCode = Guid.NewGuid().ToString("N"),
            FinalPrice = finalPrice
        };

        booking.Ticket = ticket;

        // 9. Add the booking and save both entities together.
        await _bookingRepository.AddBookingAsync(
            booking,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // 10. Return the booking details, including the new ticket.
        var result = _mapper.Map<BookingDTO>(booking);

        result.MovieTitle = session.Movie.Title;
        result.SessionStartsAt = session.StartsAt;
        result.RowNumber = seat.RowNumber;
        result.SeatNumber = seat.SeatNumber;
        result.IsVip = seat.IsVip;
        result.TicketCode = ticket.TicketCode;
        result.FinalPrice = ticket.FinalPrice;

        return result;
    }

    public async Task<BookingDTO?> GetBookingByIdAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var booking = await _bookingRepository.GetBookingByIdAsync(
            id,
            cancellationToken);

        return booking is null
            ? null
            : _mapper.Map<BookingDTO>(booking);
    }

    public async Task<BookingDTO?> GetUserBookingByIdAsync(
        int id,
        int userId,
        CancellationToken cancellationToken)
    {
        var booking = await _bookingRepository.GetBookingByIdAsync(
            id,
            cancellationToken);

        // Another user's booking is reported as missing so its existence is not leaked.
        return booking is null || booking.UserId != userId
            ? null
            : _mapper.Map<BookingDTO>(booking);
    }

    public async Task<ICollection<BookingDTO>> GetUserBookingsAsync(
        int userId,
        CancellationToken cancellationToken)
    {
        var bookings = await _bookingRepository.GetBookingsByUserAsync(
            userId,
            cancellationToken);

        return bookings
            .Select(booking => _mapper.Map<BookingDTO>(booking))
            .ToList();
    }

    public async Task<ICollection<BookingDTO>> GetAllBookingsAsync(
        CancellationToken cancellationToken)
    {
        var bookings = await _bookingRepository.GetAllBookingsAsync(
            cancellationToken);

        return bookings
            .Select(booking => _mapper.Map<BookingDTO>(booking))
            .ToList();
    }

    public async Task CancelBookingAsync(
        int id,
        int userId,
        bool isAdmin,
        CancellationToken cancellationToken)
    {
        var booking = await _bookingRepository.GetBookingByIdAsync(
            id,
            cancellationToken);

        if (booking is null)
        {
            throw new NotFoundException(
                $"Booking with ID {id} not found.");
        }

        if (!isAdmin && booking.UserId != userId)
        {
            throw new ForbiddenException(
                "You cannot cancel another user's booking.");
        }

        if (booking.Status != BookingStatus.Active)
        {
            throw new ValidationException(
                "This booking is already cancelled.");
        }

        booking.Status = BookingStatus.Cancelled;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}