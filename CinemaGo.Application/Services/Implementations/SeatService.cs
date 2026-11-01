using AutoMapper;
using CinemaGo.Application.Services.Interfaces;
using CinemaGo.Domain.DTOs.SeatDTOs;
using CinemaGo.Domain.Entities;
using CinemaGo.Domain.Exceptions;
using CinemaGo.Domain.Intefraces;

namespace CinemaGo.Application.Services.Implementations;

public class SeatService : ISeatService
{
    private readonly ISeatRepository _seatRepository;
    private readonly IHallRepository _hallRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public SeatService(
        ISeatRepository seatRepository,
        IHallRepository hallRepository,
        IUnitOfWork unitOfWork,
        IMapper mapper)
    {
        _seatRepository = seatRepository;
        _hallRepository = hallRepository;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<ICollection<SeatDTO>> GetSeatsAsync(int? hallId, CancellationToken cancellationToken)
    {
        var seats = hallId.HasValue
            ? await _seatRepository.GetSeatsByHallIdAsync(hallId.Value, cancellationToken)
            : await _seatRepository.GetAllSeatsAsync(cancellationToken);

        return seats
            .OrderBy(s => s.HallId).ThenBy(s => s.RowNumber).ThenBy(s => s.SeatNumber)
            .Select(_mapper.Map<SeatDTO>)
            .ToList();
    }

    public async Task<SeatDTO?> GetSeatByIdAsync(int id, CancellationToken cancellationToken)
    {
        var seat = await _seatRepository.GetSeatByIdAsync(id, cancellationToken);
        return seat is null ? null : _mapper.Map<SeatDTO>(seat);
    }

    public async Task<SeatDTO> AddSeatAsync(CreateSeatDTO dto, CancellationToken cancellationToken)
    {
        ValidatePosition(dto.RowNumber, dto.SeatNumber);

        var hall = await _hallRepository.GetHallByIdAsync(dto.HallId, cancellationToken)
            ?? throw new NotFoundException("Hall was not found.");

        var hallSeats = await _seatRepository.GetSeatsByHallIdAsync(hall.Id, cancellationToken);
        EnsurePositionIsFree(hallSeats, dto.RowNumber, dto.SeatNumber, null);

        if (hallSeats.Count >= hall.Capacity)
        {
            throw new ConflictException($"The hall is full (capacity {hall.Capacity}).");
        }

        var seat = new Seat
        {
            HallId = hall.Id,
            RowNumber = dto.RowNumber,
            SeatNumber = dto.SeatNumber,
            IsVip = dto.IsVip
        };

        await _seatRepository.AddSeatAsync(seat, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _mapper.Map<SeatDTO>(seat);
    }

    public async Task<SeatDTO?> UpdateSeatAsync(int id, UpdateSeatDTO dto, CancellationToken cancellationToken)
    {
        var seat = await _seatRepository.GetSeatByIdAsync(id, cancellationToken);
        if (seat is null)
        {
            return null;
        }

        ValidatePosition(dto.RowNumber, dto.SeatNumber);

        var hallSeats = await _seatRepository.GetSeatsByHallIdAsync(seat.HallId, cancellationToken);
        EnsurePositionIsFree(hallSeats, dto.RowNumber, dto.SeatNumber, id);

        seat.RowNumber = dto.RowNumber;
        seat.SeatNumber = dto.SeatNumber;
        seat.IsVip = dto.IsVip;

        _seatRepository.UpdateSeatAsync(seat);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _mapper.Map<SeatDTO>(seat);
    }

    public async Task<bool> DeleteSeatAsync(int id, CancellationToken cancellationToken)
    {
        var seat = await _seatRepository.GetSeatByIdAsync(id, cancellationToken);
        if (seat is null)
        {
            return false;
        }

        if (seat.Bookings.Count > 0)
        {
            throw new ConflictException("A seat with bookings cannot be deleted.");
        }

        _seatRepository.DeleteSeatAsync(seat);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static void ValidatePosition(int rowNumber, int seatNumber)
    {
        if (rowNumber < 1 || seatNumber < 1)
        {
            throw new ValidationException("Row number and seat number must be greater than zero.");
        }
    }

    private static void EnsurePositionIsFree(ICollection<Seat> hallSeats, int row, int number, int? excludedId)
    {
        if (hallSeats.Any(s => s.Id != excludedId && s.RowNumber == row && s.SeatNumber == number))
        {
            throw new ConflictException($"Seat {number} in row {row} already exists in this hall.");
        }
    }
}
