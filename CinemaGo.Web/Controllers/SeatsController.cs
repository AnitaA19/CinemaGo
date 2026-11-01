using CinemaGo.Application.Services.Interfaces;
using CinemaGo.Domain.DTOs.SeatDTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CinemaGo.Web.Controllers;

[ApiController]
[Route("api/seats")]
public class SeatsController : ControllerBase
{
    private readonly ISeatService _seatService;

    public SeatsController(ISeatService seatService)
    {
        _seatService = seatService;
    }

    [HttpGet]
    public async Task<ActionResult<ICollection<SeatDTO>>> GetAll(
        [FromQuery] int? hallId,
        CancellationToken cancellationToken)
    {
        return Ok(await _seatService.GetSeatsAsync(hallId, cancellationToken));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<SeatDTO>> GetById(int id, CancellationToken cancellationToken)
    {
        var seat = await _seatService.GetSeatByIdAsync(id, cancellationToken);
        return seat is null ? NotFound() : Ok(seat);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<SeatDTO>> Create(CreateSeatDTO dto, CancellationToken cancellationToken)
    {
        var seat = await _seatService.AddSeatAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = seat.Id }, seat);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<SeatDTO>> Update(int id, UpdateSeatDTO dto, CancellationToken cancellationToken)
    {
        var seat = await _seatService.UpdateSeatAsync(id, dto, cancellationToken);
        return seat is null ? NotFound() : Ok(seat);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        return await _seatService.DeleteSeatAsync(id, cancellationToken) ? NoContent() : NotFound();
    }
}
