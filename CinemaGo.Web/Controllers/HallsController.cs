using CinemaGo.Application.Services.Interfaces;
using CinemaGo.Domain.DTOs.HallDTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CinemaGo.Web.Controllers;

[ApiController]
[Route("api/halls")]
public class HallsController : ControllerBase
{
    private readonly IHallService _hallService;

    public HallsController(IHallService hallService)
    {
        _hallService = hallService;
    }

    [HttpGet]
    public async Task<ActionResult<ICollection<HallDTO>>> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await _hallService.GetAllHallsAsync(cancellationToken));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<HallDTO>> GetById(int id, CancellationToken cancellationToken)
    {
        var hall = await _hallService.GetHallByIdAsync(id, cancellationToken);
        return hall is null ? NotFound() : Ok(hall);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<HallDTO>> Create(CreateHallDTO dto, CancellationToken cancellationToken)
    {
        var hall = await _hallService.AddHallAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = hall.Id }, hall);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<HallDTO>> Update(int id, UpdateHallDTO dto, CancellationToken cancellationToken)
    {
        var hall = await _hallService.UpdateHallAsync(id, dto, cancellationToken);
        return hall is null ? NotFound() : Ok(hall);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        return await _hallService.DeleteHallAsync(id, cancellationToken) ? NoContent() : NotFound();
    }
}
