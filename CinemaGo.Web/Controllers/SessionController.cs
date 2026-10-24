using CinemaGo.Application.Services.Interfaces;
using CinemaGo.Domain.DTOs.SessionDTOs;
using Microsoft.AspNetCore.Mvc;

namespace CinemaGo.Web.Controllers;

[ApiController]
[Route("api/sessions")]
public class SessionsController : ControllerBase
{
    private readonly ISessionService _sessionService;

    public SessionsController(ISessionService sessionService)
    {
        _sessionService = sessionService;
    }

    [HttpGet]
    public async Task<ActionResult<ICollection<SessionDTO>>> GetAll(
        CancellationToken cancellationToken)
    {
        var sessions = await _sessionService
            .GetAllSessionsAsync(cancellationToken);

        return Ok(sessions);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<SessionDTO>> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var session = await _sessionService
            .GetSessionByIdAsync(id, cancellationToken);

        if (session is null)
        {
            return NotFound();
        }

        return Ok(session);
    }

    [HttpPost]
    public async Task<ActionResult<SessionDTO>> Create(
        CreateSessionDTO dto,
        CancellationToken cancellationToken)
    {
        try
        {
            var session = await _sessionService
                .AddSessionAsync(dto, cancellationToken);

            return CreatedAtAction(
                nameof(GetById),
                new { id = session.Id },
                session);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<SessionDTO>> Update(
        int id,
        UpdateSessionDTO dto,
        CancellationToken cancellationToken)
    {
        try
        {
            var session = await _sessionService
                .UpdateSessionAsync(id, dto, cancellationToken);

            if (session is null)
            {
                return NotFound();
            }

            return Ok(session);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(
        int id,
        CancellationToken cancellationToken)
    {
        try
        {
            var deleted = await _sessionService
                .DeleteSessionAsync(id, cancellationToken);

            if (!deleted)
            {
                return NotFound();
            }

            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}