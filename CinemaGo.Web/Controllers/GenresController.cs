using CinemaGo.Application.Services.Interfaces;
using CinemaGo.Domain.DTOs.GenreDTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CinemaGo.Web.Controllers;

[ApiController]
[Route("api/genres")]
public class GenresController : ControllerBase
{
    private readonly IGenreService _genreService;

    public GenresController(IGenreService genreService)
    {
        _genreService = genreService;
    }

    [HttpGet]
    public async Task<ActionResult<ICollection<GenreDTO>>> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await _genreService.GetAllGenresAsync(cancellationToken));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<GenreDTO>> GetById(int id, CancellationToken cancellationToken)
    {
        var genre = await _genreService.GetGenreByIdAsync(id, cancellationToken);
        return genre is null ? NotFound() : Ok(genre);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<GenreDTO>> Create(CreateGenreDTO dto, CancellationToken cancellationToken)
    {
        var genre = await _genreService.AddGenreAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = genre.Id }, genre);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<GenreDTO>> Update(int id, UpdateGenreDTO dto, CancellationToken cancellationToken)
    {
        var genre = await _genreService.UpdateGenreAsync(id, dto, cancellationToken);
        return genre is null ? NotFound() : Ok(genre);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        return await _genreService.DeleteGenreAsync(id, cancellationToken) ? NoContent() : NotFound();
    }
}
