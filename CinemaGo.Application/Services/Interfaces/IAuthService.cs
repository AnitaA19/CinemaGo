using CinemaGo.Domain.DTOs.AuthDTOs;

namespace CinemaGo.Application.Services.Interfaces;

public interface IAuthService
{
    Task<AuthResponseDTO> RegisterAsync(
        RegisterDTO dto,
        CancellationToken cancellationToken);

    Task<AuthResponseDTO> LoginAsync(
        LoginDTO dto,
        CancellationToken cancellationToken);
}