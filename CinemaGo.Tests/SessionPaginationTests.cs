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

// Runs the real repository against SQLite so filtering, sorting and paging are translated to SQL.
public sealed class SessionPaginationTests : IDisposable
{
    private static readonly DateTime Base = new(2030, 1, 1, 10, 0, 0, DateTimeKind.Utc);

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly CinemaDbContext _context;
    private readonly SessionService _service;

    public SessionPaginationTests()
    {
        _connection.Open();
        _context = new CinemaDbContext(new DbContextOptionsBuilder<CinemaDbContext>().UseSqlite(_connection).Options);
        _context.Database.EnsureCreated();

        _context.Movies.AddRange(
            new Movie { Id = 1, Title = "Dune", DurationMinutes = 60, AgeRating = 12 },
            new Movie { Id = 2, Title = "Alien", DurationMinutes = 60, AgeRating = 16 },
            new Movie { Id = 3, Title = "Empty", DurationMinutes = 60, AgeRating = 0 });
        _context.Halls.Add(new Hall { Id = 1, Name = "Hall A", Capacity = 10 });

        // Movie 1: 12 sessions, one per day, inserted in reverse order to prove sorting.
        for (var i = 12; i >= 1; i--)
        {
            _context.Sessions.Add(new Session
            {
                MovieId = 1,
                HallId = 1,
                StartsAt = Base.AddDays(i),
                StandardPrice = 10,
                VipPrice = 20
            });
        }

        // Movie 2: 3 sessions.
        for (var i = 1; i <= 3; i++)
        {
            _context.Sessions.Add(new Session
            {
                MovieId = 2,
                HallId = 1,
                StartsAt = Base.AddDays(i).AddHours(5),
                StandardPrice = 10,
                VipPrice = 20
            });
        }

        _context.SaveChanges();

        var mapper = new MapperConfiguration(c => c.AddProfile<SessionProfile>(), NullLoggerFactory.Instance).CreateMapper();
        _service = new SessionService(
            new SessionRepository(_context),
            new MovieRepository(_context),
            new HallRepository(_context),
            new SeatRepository(_context),
            new UnitOfWork(_context),
            mapper);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task NoFilters_ReturnsAllWithCorrectTotals()
    {
        var result = await _service.GetSessionsAsync(null, null, null, 1, 10, CancellationToken.None);

        Assert.Equal(10, result.Sessions.Count);
        Assert.Equal(15, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
        Assert.Equal(1, result.Page);
        Assert.Equal(10, result.PageSize);
    }

    [Fact]
    public async Task MovieIdFilter_ReturnsOnlyThatMovie()
    {
        var result = await _service.GetSessionsAsync(2, null, null, 1, 10, CancellationToken.None);

        Assert.Equal(3, result.TotalCount);
        Assert.Equal(1, result.TotalPages);
        Assert.All(result.Sessions, s => Assert.Equal(2, s.MovieId));
    }

    [Fact]
    public async Task Sessions_AreSortedByStartTimeAscending()
    {
        var result = await _service.GetSessionsAsync(1, null, null, 1, 12, CancellationToken.None);

        var starts = result.Sessions.Select(s => s.StartsAt).ToList();
        Assert.Equal(12, starts.Count);
        Assert.Equal(starts.OrderBy(s => s).ToList(), starts);
        Assert.Equal(Base.AddDays(1), starts.First());
    }

    [Fact]
    public async Task Paging_SecondPageContinuesTheOrderWithoutOverlap()
    {
        var first = await _service.GetSessionsAsync(1, null, null, 1, 5, CancellationToken.None);
        var second = await _service.GetSessionsAsync(1, null, null, 2, 5, CancellationToken.None);
        var third = await _service.GetSessionsAsync(1, null, null, 3, 5, CancellationToken.None);

        Assert.Equal(5, first.Sessions.Count);
        Assert.Equal(5, second.Sessions.Count);
        Assert.Equal(2, third.Sessions.Count);
        Assert.Equal(3, third.TotalPages);
        Assert.Equal(12, third.TotalCount);

        var all = first.Sessions.Concat(second.Sessions).Concat(third.Sessions).Select(s => s.Id).ToList();
        Assert.Equal(12, all.Distinct().Count());
        Assert.True(first.Sessions.Last().StartsAt < second.Sessions.First().StartsAt);
    }

    [Fact]
    public async Task DateFrom_IsInclusive()
    {
        var result = await _service.GetSessionsAsync(1, Base.AddDays(10), null, 1, 10, CancellationToken.None);

        Assert.Equal(3, result.TotalCount);
        Assert.Equal(Base.AddDays(10), result.Sessions.First().StartsAt);
    }

    [Fact]
    public async Task DateTo_IsInclusive()
    {
        var result = await _service.GetSessionsAsync(1, null, Base.AddDays(2), 1, 10, CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(Base.AddDays(2), result.Sessions.Last().StartsAt);
    }

    [Fact]
    public async Task DateRangeAndMovie_AreCombined()
    {
        var result = await _service.GetSessionsAsync(
            2, Base.AddDays(2), Base.AddDays(3), 1, 10, CancellationToken.None);

        // Movie 2 sessions are at day+5h, so days 2 and 3 are both inside the range.
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(Base.AddDays(2).AddHours(5), result.Sessions.Single().StartsAt);
    }

    [Fact]
    public async Task NonUtcDateFilter_IsConvertedToUtc()
    {
        var localInstant = Base.AddDays(10).ToLocalTime();

        var result = await _service.GetSessionsAsync(
            1, localInstant, null, 1, 20, CancellationToken.None);

        Assert.Equal(3, result.TotalCount);
        Assert.Equal(Base.AddDays(10), result.Sessions.First().StartsAt);
    }

    [Fact]
    public async Task MovieWithoutSessions_ReturnsEmptyResult()
    {
        var result = await _service.GetSessionsAsync(3, null, null, 1, 10, CancellationToken.None);

        Assert.Empty(result.Sessions);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
    }

    [Fact]
    public async Task UnknownMovie_ReturnsEmptyResult()
    {
        var result = await _service.GetSessionsAsync(999, null, null, 1, 10, CancellationToken.None);

        Assert.Empty(result.Sessions);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task DateRangeWithNoSessions_ReturnsEmptyResult()
    {
        var result = await _service.GetSessionsAsync(
            null, Base.AddYears(5), Base.AddYears(6), 1, 10, CancellationToken.None);

        Assert.Empty(result.Sessions);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
    }

    [Fact]
    public async Task PageBeyondEnd_ReturnsEmptyWithTotals()
    {
        var result = await _service.GetSessionsAsync(null, null, null, 99, 10, CancellationToken.None);

        Assert.Empty(result.Sessions);
        Assert.Equal(15, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
    }

    [Fact]
    public async Task MaxPageSize_IsAccepted()
    {
        var result = await _service.GetSessionsAsync(null, null, null, 1, 100, CancellationToken.None);

        Assert.Equal(15, result.Sessions.Count);
        Assert.Equal(1, result.TotalPages);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(-1, 10)]
    [InlineData(1, 0)]
    [InlineData(1, -5)]
    [InlineData(1, 101)]
    public async Task InvalidPaging_ThrowsValidationException(int page, int pageSize)
    {
        await Assert.ThrowsAsync<ValidationException>(
            () => _service.GetSessionsAsync(null, null, null, page, pageSize, CancellationToken.None));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task InvalidMovieId_ThrowsValidationException(int movieId)
    {
        await Assert.ThrowsAsync<ValidationException>(
            () => _service.GetSessionsAsync(movieId, null, null, 1, 10, CancellationToken.None));
    }

    [Fact]
    public async Task DateFromAfterDateTo_ThrowsValidationException()
    {
        await Assert.ThrowsAsync<ValidationException>(
            () => _service.GetSessionsAsync(null, Base.AddDays(5), Base.AddDays(1), 1, 10, CancellationToken.None));
    }
}
