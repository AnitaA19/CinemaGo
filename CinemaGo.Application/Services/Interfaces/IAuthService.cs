using CinemaGo.Domain.DTOs.AuthDTOs;

namespace CinemaGo.Application.Services.Interfaces;

public interface IAuthService
{
    Task<AuthResponseDTO> RegisterAsync(
        RegisterDTO dto,
        CancellationToken cancellationToken);

    Task<AdminInvitationDTO> InviteAdminAsync(
        InviteAdminDTO dto,
        int invitedByUserId,
        CancellationToken cancellationToken);

    Task<AuthResponseDTO> AcceptAdminInvitationAsync(
        AcceptAdminInvitationDTO dto,
        CancellationToken cancellationToken);

    Task<AuthResponseDTO> LoginAsync(
        LoginDTO dto,
        CancellationToken cancellationToken);

    Task<AuthResponseDTO> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken);

    Task RevokeRefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken);
}