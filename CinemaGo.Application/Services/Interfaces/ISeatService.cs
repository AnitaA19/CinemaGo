using CinemaGo.Domain.DTOs.SeatDTOs;

namespace CinemaGo.Application.Services.Interfaces;

public interface ISeatService
{
    Task<ICollection<SeatDTO>> GetSeatsAsync(int? hallId, CancellationToken cancellationToken);
    Task<SeatDTO?> GetSeatByIdAsync(int id, CancellationToken cancellationToken);
    Task<SeatDTO> AddSeatAsync(CreateSeatDTO dto, CancellationToken cancellationToken);
    Task<SeatDTO?> UpdateSeatAsync(int id, UpdateSeatDTO dto, CancellationToken cancellationToken);
    Task<bool> DeleteSeatAsync(int id, CancellationToken cancellationToken);
}
