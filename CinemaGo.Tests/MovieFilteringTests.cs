using AutoMapper;
using CinemaGo.Application.Profiles;
using CinemaGo.Application.Services.Implementations;
using CinemaGo.Domain.Entities;
using CinemaGo.Domain.Exceptions;
using CinemaGo.Infrastructure.Data;
using CinemaGo.Infrastructure.Repositories;
using CinemaGo.Infrastructure.UnitOfWork;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CinemaGo.Tests;

// Runs the real repository against SQLite so filtering and paging are translated to SQL.
public sealed class MovieFilteringTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly CinemaDbContext _context;
    private readonly MovieService _service;

    public MovieFilteringTests()
    {
        _connection.Open();
        _context = new CinemaDbContext(new DbContextOptionsBuilder<CinemaDbContext>().UseSqlite(_connection).Options);
        _context.Database.EnsureCreated();

        _context.Genres.AddRange(new Genre { Id = 1, Name = "Sci-Fi" }, new Genre { Id = 2, Name = "Drama" });
        for (var i = 1; i <= 25; i++)
        {
            _context.Movies.Add(new Movie
            {
                Id = i,
                Title = i <= 5 ? $"Dune {i:00}" : $"Film {i:00}",
                DurationMinutes = 100,
                AgeRating = 12,
                MovieGenres = { new MovieGenre { GenreId = i % 2 == 0 ? 1 : 2 } }
            });
        }
        _context.SaveChanges();

        var mapper = new MapperConfiguration(c => c.AddProfile<MovieMapper>(), NullLoggerFactory.Instance).CreateMapper();
        _service = new MovieService(new MovieRepository(_context), new UnitOfWork(_context), mapper);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task Defaults_ReturnFirstTenOrderedByTitle()
    {
        var result = await _service.GetMoviesAsync(null, null, 1, 10, CancellationToken.None);

        Assert.Equal(10, result.Movies.Count);
        Assert.Equal(1, result.Page);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(25, result.TotalCount);
        Assert.Equal(3, result.TotalPages);
        Assert.Equal("Dune 01", result.Movies.First().Title);
    }

    [Fact]
    public async Task LastPage_ReturnsRemainingItems()
    {
        var result = await _service.GetMoviesAsync(null, null, 3, 10, CancellationToken.None);

        Assert.Equal(5, result.Movies.Count);
    }

    [Fact]
    public async Task PageBeyondEnd_ReturnsEmptyWithTotals()
    {
        var result = await _service.GetMoviesAsync(null, null, 9, 10, CancellationToken.None);

        Assert.Empty(result.Movies);
        Assert.Equal(25, result.TotalCount);
    }

    [Fact]
    public async Task TitleFilter_IsCaseInsensitivePartialMatch()
    {
        var result = await _service.GetMoviesAsync("dune", null, 1, 10, CancellationToken.None);

        Assert.Equal(5, result.TotalCount);
        Assert.Equal(1, result.TotalPages);
        Assert.All(result.Movies, m => Assert.StartsWith("Dune", m.Title));
    }

    [Fact]
    public async Task GenreFilter_ReturnsOnlyMoviesInGenre()
    {
        var result = await _service.GetMoviesAsync(null, 1, 1, 100, CancellationToken.None);

        Assert.Equal(12, result.TotalCount);
        Assert.Equal(12, result.Movies.Count);
    }

    [Fact]
    public async Task TitleAndGenreFilters_AreCombined()
    {
        var result = await _service.GetMoviesAsync("Dune", 1, 1, 10, CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(-1, 10)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task InvalidPaging_ThrowsValidationException(int page, int pageSize)
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.GetMoviesAsync(null, null, page, pageSize, CancellationToken.None));
    }

    [Fact]
    public async Task InvalidGenreId_ThrowsValidationException()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.GetMoviesAsync(null, 0, 1, 10, CancellationToken.None));
    }
}
