using CinemaGo.Domain.DTOs.HallDTOs;

namespace CinemaGo.Application.Services.Interfaces;

public interface IHallService
{
    Task<ICollection<HallDTO>> GetAllHallsAsync(CancellationToken cancellationToken);
    Task<HallDTO?> GetHallByIdAsync(int id, CancellationToken cancellationToken);
    Task<HallDTO> AddHallAsync(CreateHallDTO dto, CancellationToken cancellationToken);
    Task<HallDTO?> UpdateHallAsync(int id, UpdateHallDTO dto, CancellationToken cancellationToken);
    Task<bool> DeleteHallAsync(int id, CancellationToken cancellationToken);
}
