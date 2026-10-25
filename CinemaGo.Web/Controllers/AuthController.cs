using CinemaGo.Application.Services.Interfaces;
using CinemaGo.Domain.DTOs.AuthDTOs;
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

    [HttpPost("register")]
    public async Task<IActionResult> Register(
        [FromBody] RegisterDTO dto,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _authService.RegisterAsync(
                dto, cancellationToken);

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromBody] LoginDTO dto,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _authService.LoginAsync(
                dto, cancellationToken);

            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new
            {
                message = "Invalid username/email or password."
            });
        }
    }
}