using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CinemaGo.Domain.Constants;
using CinemaGo.Domain.DTOs.MovieDTOs;
using CinemaGo.Application.Services.Interfaces;
using CinemaGo.Web.Uploads;

namespace CinemaGo.Web.Controllers;

[ApiController]
[Route("api/movies")]
public class MoviesController : ControllerBase
{
    private readonly IMovieService _movieService;
    private readonly PosterStorage _posterStorage;

    public MoviesController(IMovieService movieService, PosterStorage posterStorage)
    {
        _movieService = movieService;
        _posterStorage = posterStorage;
    }

    [HttpPost("{id:int}/poster")]
    [Authorize(Roles = "Admin")]
    [RequestSizeLimit(PosterStorage.MaxBytes + 64 * 1024)]
    public async Task<ActionResult<MovieDTO>> UploadPoster(int id, IFormFile file, CancellationToken cancellationToken)
    {
        var movie = await _movieService.GetMovieByIdAsync(id, cancellationToken);
        if (movie == null)
        {
            return NotFound();
        }

        var newUrl = await _posterStorage.SaveAsync(file, cancellationToken);
        try
        {
            var updated = await SetPosterAsync(movie, newUrl, cancellationToken);
            if (updated == null)
            {
                _posterStorage.Delete(newUrl);
                return NotFound();
            }
            _posterStorage.Delete(movie.PosterUrl);
            return Ok(updated);
        }
        catch
        {
            _posterStorage.Delete(newUrl);
            throw;
        }
    }

    [HttpDelete("{id:int}/poster")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<MovieDTO>> RemovePoster(int id, CancellationToken cancellationToken)
    {
        var movie = await _movieService.GetMovieByIdAsync(id, cancellationToken);
        if (movie == null)
        {
            return NotFound();
        }

        var updated = await SetPosterAsync(movie, null, cancellationToken);
        if (updated == null)
        {
            return NotFound();
        }
        _posterStorage.Delete(movie.PosterUrl);
        return Ok(updated);
    }

    private Task<MovieDTO?> SetPosterAsync(MovieDTO movie, string? posterUrl, CancellationToken cancellationToken) =>
        _movieService.UpdateMovieAsync(movie.Id, new UpdateMovieDTO
        {
            Title = movie.Title,
            DurationMinutes = movie.DurationMinutes,
            AgeRating = movie.AgeRating,
            PosterUrl = posterUrl,
            GenreIds = movie.Genres.Select(g => g.Id).ToList()
        }, cancellationToken);

    [HttpGet]
    public async Task<ActionResult<PagedMoviesDTO>> GetAll(
        [FromQuery] string? title,
        [FromQuery] int? genreId,
        CancellationToken cancellationToken,
        [FromQuery] int page = PaginationLimits.DefaultPage,
        [FromQuery] int pageSize = PaginationLimits.DefaultPageSize)
    {
        return Ok(await _movieService.GetMoviesAsync(title, genreId, page, pageSize, cancellationToken));
    }

    [HttpGet("sales")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ICollection<MovieSalesDTO>>> GetSales(CancellationToken cancellationToken)
    {
        return Ok(await _movieService.GetSalesByMovieAsync(cancellationToken));
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