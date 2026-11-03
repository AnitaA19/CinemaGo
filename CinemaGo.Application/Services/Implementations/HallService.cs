using AutoMapper;
using CinemaGo.Application.Services.Interfaces;
using CinemaGo.Domain.DTOs.HallDTOs;
using CinemaGo.Domain.Entities;
using CinemaGo.Domain.Exceptions;
using CinemaGo.Domain.Intefraces;

namespace CinemaGo.Application.Services.Implementations;

public class HallService : IHallService
{
    private readonly IHallRepository _hallRepository;
    private readonly ISeatRepository _seatRepository;
    private readonly ISessionRepository _sessionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public HallService(
        IHallRepository hallRepository,
        ISeatRepository seatRepository,
        ISessionRepository sessionRepository,
        IUnitOfWork unitOfWork,
        IMapper mapper)
    {
        _hallRepository = hallRepository;
        _seatRepository = seatRepository;
        _sessionRepository = sessionRepository;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<ICollection<HallDTO>> GetAllHallsAsync(CancellationToken cancellationToken)
    {
        var halls = await _hallRepository.GetAllHallsAsync(cancellationToken);
        return halls.OrderBy(h => h.Name).Select(_mapper.Map<HallDTO>).ToList();
    }

    public async Task<HallDTO?> GetHallByIdAsync(int id, CancellationToken cancellationToken)
    {
        var hall = await _hallRepository.GetHallByIdAsync(id, cancellationToken);
        return hall is null ? null : _mapper.Map<HallDTO>(hall);
    }

    public async Task<HallDTO> AddHallAsync(CreateHallDTO dto, CancellationToken cancellationToken)
    {
        var name = NormalizeName(dto.Name);
        ValidateCapacity(dto.Capacity);
        await EnsureNameIsUniqueAsync(name, null, cancellationToken);

        var hall = new Hall { Name = name, Capacity = dto.Capacity };
        await _hallRepository.AddHallAsync(hall, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _mapper.Map<HallDTO>(hall);
    }

    public async Task<HallDTO?> UpdateHallAsync(int id, UpdateHallDTO dto, CancellationToken cancellationToken)
    {
        return await _unitOfWork.ExecuteInSerializableTransactionAsync(async () =>
        {
            var hall = await _hallRepository.GetHallByIdAsync(id, cancellationToken);
            if (hall is null)
            {
                return null;
            }

            var name = NormalizeName(dto.Name);
            ValidateCapacity(dto.Capacity);
            await EnsureNameIsUniqueAsync(name, id, cancellationToken);

            var seats = await _seatRepository.GetSeatsByHallIdAsync(id, cancellationToken);
            if (dto.Capacity < seats.Count)
            {
                throw new ConflictException(
                    $"Capacity cannot be lower than the number of existing seats ({seats.Count}).");
            }

            hall.Name = name;
            hall.Capacity = dto.Capacity;
            _hallRepository.UpdateHallAsync(hall);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return _mapper.Map<HallDTO>(hall);
        }, cancellationToken);
    }

    public async Task<bool> DeleteHallAsync(int id, CancellationToken cancellationToken)
    {
        var hall = await _hallRepository.GetHallByIdAsync(id, cancellationToken);
        if (hall is null)
        {
            return false;
        }

        var sessions = await _sessionRepository.GetSessionsByHallAsync(id, cancellationToken);
        if (sessions.Count > 0)
        {
            throw new ConflictException("A hall with sessions cannot be deleted.");
        }

        var seats = await _seatRepository.GetSeatsByHallIdAsync(id, cancellationToken);
        if (seats.Count > 0)
        {
            throw new ConflictException("A hall with seats cannot be deleted. Delete its seats first.");
        }

        _hallRepository.DeleteHallAsync(hall);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static string NormalizeName(string? name)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new ValidationException("Hall name cannot be empty.");
        }

        if (trimmed.Length > 100)
        {
            throw new ValidationException("Hall name cannot be longer than 100 characters.");
        }

        return trimmed;
    }

    private static void ValidateCapacity(int capacity)
    {
        if (capacity < 1 || capacity > 10000)
        {
            throw new ValidationException("Hall capacity must be between 1 and 10000.");
        }
    }

    private async Task EnsureNameIsUniqueAsync(string name, int? excludedId, CancellationToken cancellationToken)
    {
        var halls = await _hallRepository.GetAllHallsAsync(cancellationToken);
        if (halls.Any(h => h.Id != excludedId && string.Equals(h.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ConflictException($"A hall named '{name}' already exists.");
        }
    }
}
