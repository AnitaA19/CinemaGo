using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CinemaGo.Domain.DTOs.MovieDTOs;
using CinemaGo.Application.Services.Interfaces;

namespace CinemaGo.Web.Controllers;

[ApiController]
[Route("api/movies")]
public class MoviesController : ControllerBase
{
    private readonly IMovieService _movieService;

    public MoviesController(IMovieService movieService)
    {
        _movieService = movieService;
    }

    [HttpGet]
    public async Task<ActionResult<ICollection<MovieDTO>>> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await _movieService.GetAllMoviesAsync(cancellationToken));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<MovieDTO>> GetById(int id, CancellationToken cancellationToken)
    {
        var movie = await _movieService.GetMovieByIdAsync(id, cancellationToken);
        return movie == null ? NotFound() : Ok(movie);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<MovieDTO>> Create(CreateMovieDTO createMovieDto, CancellationToken cancellationToken)
    {
        var movie = await _movieService.AddMovieAsync(createMovieDto, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = movie.Id }, movie);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<MovieDTO>> Update(int id, UpdateMovieDTO updateMovieDto, CancellationToken cancellationToken)
    {
        var movie = await _movieService.UpdateMovieAsync(id, updateMovieDto, cancellationToken);
        return movie == null ? NotFound() : Ok(movie);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await _movieService.DeleteMovieAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}