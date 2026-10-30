using AutoMapper;
using CinemaGo.Application.Services.Interfaces;
using CinemaGo.Domain.Constants;
using CinemaGo.Domain.DTOs.SessionDTOs;
using CinemaGo.Domain.Entities;
using CinemaGo.Domain.Enums;
using CinemaGo.Domain.Exceptions;
using CinemaGo.Domain.Intefraces;

namespace CinemaGo.Application.Services.Implementations;

public class SessionService : ISessionService
{
    private readonly ISessionRepository _sessionRepository;
    private readonly IMovieRepository _movieRepository;
    private readonly IHallRepository _hallRepository;
    private readonly ISeatRepository _seatRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public SessionService(
        ISessionRepository sessionRepository,
        IMovieRepository movieRepository,
        IHallRepository hallRepository,
        ISeatRepository seatRepository,
        IUnitOfWork unitOfWork,
        IMapper mapper)
    {
        _sessionRepository = sessionRepository;
        _movieRepository = movieRepository;
        _hallRepository = hallRepository;
        _seatRepository = seatRepository;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<ICollection<SessionDTO>> GetAllSessionsAsync(
        CancellationToken cancellationToken)
    {
        var sessions = await _sessionRepository
            .GetAllSessionsAsync(cancellationToken);

        return sessions
            .Select(_mapper.Map<SessionDTO>)
            .ToList();
    }

    public async Task<PagedSessionsDTO> GetSessionsAsync(
        int? movieId,
        DateTime? dateFrom,
        DateTime? dateTo,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (page < 1)
        {
            throw new ValidationException("Page must be greater than or equal to 1.");
        }

        if (pageSize < 1 || pageSize > PaginationLimits.MaxPageSize)
        {
            throw new ValidationException(
                $"Page size must be between 1 and {PaginationLimits.MaxPageSize}.");
        }

        if (movieId.HasValue && movieId.Value < 1)
        {
            throw new ValidationException("Movie id must be greater than zero.");
        }

        var from = dateFrom.HasValue ? ToUtc(dateFrom.Value) : (DateTime?)null;
        var to = dateTo.HasValue ? ToUtc(dateTo.Value) : (DateTime?)null;

        if (from.HasValue && to.HasValue && from > to)
        {
            throw new ValidationException("dateFrom cannot be later than dateTo.");
        }

        var (sessions, totalCount) = await _sessionRepository.GetSessionsPagedAsync(
            movieId, from, to, page, pageSize, cancellationToken);

        return new PagedSessionsDTO
        {
            Sessions = sessions.Select(_mapper.Map<SessionDTO>).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        };
    }

    // Unspecified kinds are treated as UTC, matching how start times are stored.
    private static DateTime ToUtc(DateTime value) =>
        value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
            : value.ToUniversalTime();

    public async Task<SessionDTO?> GetSessionByIdAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var session = await _sessionRepository
            .GetSessionByIdAsync(id, cancellationToken);

        return session is null ? null : _mapper.Map<SessionDTO>(session);
    }

    public async Task<ICollection<SeatAvailabilityDTO>?> GetSessionSeatsAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var session = await _sessionRepository
            .GetSessionByIdAsync(id, cancellationToken);

        if (session is null)
        {
            return null;
        }

        var seats = await _seatRepository
            .GetSeatsByHallIdAsync(session.HallId, cancellationToken);

        var bookedSeatIds = session.Bookings
            .Where(b => b.Status == BookingStatus.Active)
            .Select(b => b.SeatId)
            .ToHashSet();

        return seats
            .OrderBy(s => s.RowNumber)
            .ThenBy(s => s.SeatNumber)
            .Select(s => new SeatAvailabilityDTO
            {
                SeatId = s.Id,
                RowNumber = s.RowNumber,
                SeatNumber = s.SeatNumber,
                IsVip = s.IsVip,
                IsAvailable = !bookedSeatIds.Contains(s.Id)
            })
            .ToList();
    }

    public async Task<SessionDTO> AddSessionAsync(
        CreateSessionDTO dto,
        CancellationToken cancellationToken)
    {
        ValidatePrices(dto.StandardPrice, dto.VipPrice);

        var movie = await _movieRepository.GetMovieByIdAsync(
            dto.MovieId, cancellationToken);

        if (movie is null)
        {
            throw new NotFoundException("Movie was not found.");
        }

        var hall = await _hallRepository.GetHallByIdAsync(
            dto.HallId, cancellationToken);

        if (hall is null)
        {
            throw new NotFoundException("Hall was not found.");
        }

        var startsAt = NormalizeStartTime(dto.StartsAt);

        await EnsureNoOverlapAsync(
            dto.HallId,
            startsAt,
            movie.DurationMinutes,
            null,
            cancellationToken);

        var session = new Session
        {
            MovieId = movie.Id,
            HallId = hall.Id,
            StartsAt = startsAt,
            StandardPrice = dto.StandardPrice,
            VipPrice = dto.VipPrice,
            Status = SessionStatus.Scheduled
        };

        await _sessionRepository.AddSessionAsync(
            session, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var createdSession = await _sessionRepository
            .GetSessionByIdAsync(session.Id, cancellationToken);

        if (createdSession is null)
        {
            throw new InvalidOperationException(
                "The session was saved but could not be retrieved.");
        }

        return _mapper.Map<SessionDTO>(createdSession);
    }

    public async Task<SessionDTO?> UpdateSessionAsync(
        int id,
        UpdateSessionDTO dto,
        CancellationToken cancellationToken)
    {
        var session = await _sessionRepository
            .GetSessionByIdAsync(id, cancellationToken);

        if (session is null)
        {
            return null;
        }

        if (session.Bookings.Any(b => b.Status == BookingStatus.Active))
        {
            throw new ConflictException(
                "A session with active bookings cannot be updated.");
        }

        ValidatePrices(dto.StandardPrice, dto.VipPrice);

        var movie = await _movieRepository.GetMovieByIdAsync(
            dto.MovieId, cancellationToken);

        if (movie is null)
        {
            throw new NotFoundException("Movie was not found.");
        }

        var hall = await _hallRepository.GetHallByIdAsync(
            dto.HallId, cancellationToken);

        if (hall is null)
        {
            throw new NotFoundException("Hall was not found.");
        }

        var startsAt = NormalizeStartTime(dto.StartsAt);

        await EnsureNoOverlapAsync(
            dto.HallId,
            startsAt,
            movie.DurationMinutes,
            id,
            cancellationToken);

        session.MovieId = movie.Id;
        session.HallId = hall.Id;
        session.StartsAt = startsAt;
        session.StandardPrice = dto.StandardPrice;
        session.VipPrice = dto.VipPrice;

        _sessionRepository.UpdateSessionAsync(session);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var updatedSession = await _sessionRepository
            .GetSessionByIdAsync(id, cancellationToken);

        return updatedSession is null ? null : _mapper.Map<SessionDTO>(updatedSession);
    }

    public async Task<bool> DeleteSessionAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var session = await _sessionRepository
            .GetSessionByIdAsync(id, cancellationToken);

        if (session is null)
        {
            return false;
        }

        if (session.Bookings.Any())
        {
            throw new ConflictException(
                "A session with bookings cannot be deleted.");
        }

        _sessionRepository.DeleteSessionAsync(session);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    private async Task EnsureNoOverlapAsync(
        int hallId,
        DateTime startsAt,
        int durationMinutes,
        int? excludedSessionId,
        CancellationToken cancellationToken)
    {
        var proposedEnd = startsAt.AddMinutes(durationMinutes);

        var sessions = await _sessionRepository
            .GetSessionsByHallAsync(hallId, cancellationToken);

        foreach (var existing in sessions)
        {
            if (existing.Id == excludedSessionId ||
                existing.Status == SessionStatus.Cancelled)
            {
                continue;
            }

            var existingEnd = existing.StartsAt.AddMinutes(
                existing.Movie.DurationMinutes);

            var overlaps =
                startsAt < existingEnd &&
                proposedEnd > existing.StartsAt;

            if (overlaps)
            {
                throw new ConflictException(
                    "The session overlaps with another session in this hall.");
            }
        }
    }

    private static void ValidatePrices(
        decimal standardPrice,
        decimal vipPrice)
    {
        if (standardPrice < 0)
        {
            throw new ValidationException(
                "Standard price cannot be negative.");
        }

        if (vipPrice < standardPrice)
        {
            throw new ValidationException(
                "VIP price cannot be lower than standard price.");
        }
    }

    // Start times are stored and compared in UTC; unspecified kinds are rejected as ambiguous.
    private static DateTime NormalizeStartTime(DateTime startsAt)
    {
        if (startsAt == default)
        {
            throw new ValidationException("Session start time must be specified.");
        }

        if (startsAt.Kind == DateTimeKind.Unspecified)
        {
            throw new ValidationException(
                "Session start time must include a UTC offset (e.g. 2030-01-01T18:00:00Z).");
        }

        var utc = startsAt.ToUniversalTime();

        if (utc <= DateTime.UtcNow)
        {
            throw new ValidationException("Session start time must be in the future.");
        }

        return utc;
    }
}