using CinemaGo.Application.Services.Interfaces;
using CinemaGo.Domain.DTOs.AuthDTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CinemaGo.Web.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register(
        [FromBody] RegisterDTO dto,
        CancellationToken cancellationToken)
    {
        return Ok(await _authService.RegisterAsync(dto, cancellationToken));
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromBody] LoginDTO dto,
        CancellationToken cancellationToken)
    {
        return Ok(await _authService.LoginAsync(dto, cancellationToken));
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshTokenRequestDTO dto,
        CancellationToken cancellationToken)
    {
        return Ok(await _authService.RefreshAsync(dto.RefreshToken, cancellationToken));
    }

    [AllowAnonymous]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(
        [FromBody] RefreshTokenRequestDTO dto,
        CancellationToken cancellationToken)
    {
        await _authService.RevokeRefreshTokenAsync(dto.RefreshToken, cancellationToken);
        return NoContent();
    }

    [Authorize(Roles = "SuperAdmin")]
    [HttpPost("admins")]
    public async Task<IActionResult> CreateAdmin(
        [FromBody] RegisterDTO dto,
        CancellationToken cancellationToken)
    {
        var admin = await _authService.CreateAdminAsync(dto, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, admin);
    }
}