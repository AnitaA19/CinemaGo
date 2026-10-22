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
        var movies = await _movieService.GetAllMoviesAsync(cancellationToken);
        return Ok(movies);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<MovieDTO>> GetById(int id, CancellationToken cancellationToken)
    {
        var movie = await _movieService.GetMovieByIdAsync(id, cancellationToken);
        if (movie == null)
        {
            return NotFound();
        }
        return Ok(movie);
    }

    [HttpPost]
    public async Task<ActionResult<MovieDTO>> Create(CreateMovieDTO createMovieDto, CancellationToken cancellationToken)
    {
        try
        {
            var movie = await _movieService.AddMovieAsync(createMovieDto, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = movie.Id }, movie);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<MovieDTO>> Update(int id, UpdateMovieDTO updateMovieDto, CancellationToken cancellationToken)
    {
        try
        {
            var movie = await _movieService.UpdateMovieAsync(id, updateMovieDto, cancellationToken);
            if (movie == null)
            {
                return NotFound();
            }
            return Ok(movie);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await _movieService.DeleteMovieAsync(id, cancellationToken);
        if (!deleted)
        {
            return NotFound();
        }
        return NoContent();
    }
}