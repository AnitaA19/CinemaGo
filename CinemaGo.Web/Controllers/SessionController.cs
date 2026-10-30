using CinemaGo.Application.Services.Interfaces;
using CinemaGo.Domain.Constants;
using CinemaGo.Domain.DTOs.SessionDTOs;
using Microsoft.AspNetCore.Authorization;
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
    public async Task<ActionResult<PagedSessionsDTO>> GetAll(
        [FromQuery] int? movieId,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        CancellationToken cancellationToken,
        [FromQuery] int page = PaginationLimits.DefaultPage,
        [FromQuery] int pageSize = PaginationLimits.DefaultPageSize)
    {
        return Ok(await _sessionService.GetSessionsAsync(
            movieId, dateFrom, dateTo, page, pageSize, cancellationToken));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<SessionDTO>> GetById(int id, CancellationToken cancellationToken)
    {
        var session = await _sessionService.GetSessionByIdAsync(id, cancellationToken);
        return session is null ? NotFound() : Ok(session);
    }

    [HttpGet("{id:int}/seats")]
    public async Task<ActionResult<ICollection<SeatAvailabilityDTO>>> GetSeats(int id, CancellationToken cancellationToken)
    {
        var seats = await _sessionService.GetSessionSeatsAsync(id, cancellationToken);
        return seats is null ? NotFound() : Ok(seats);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<SessionDTO>> Create(CreateSessionDTO dto, CancellationToken cancellationToken)
    {
        var session = await _sessionService.AddSessionAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = session.Id }, session);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<SessionDTO>> Update(int id, UpdateSessionDTO dto, CancellationToken cancellationToken)
    {
        var session = await _sessionService.UpdateSessionAsync(id, dto, cancellationToken);
        return session is null ? NotFound() : Ok(session);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await _sessionService.DeleteSessionAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}