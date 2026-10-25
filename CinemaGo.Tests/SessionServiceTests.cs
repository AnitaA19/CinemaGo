using AutoMapper;
using CinemaGo.Application.Profiles;
using CinemaGo.Application.Services.Implementations;
using CinemaGo.Domain.DTOs.SessionDTOs;
using CinemaGo.Domain.Entities;
using CinemaGo.Domain.Enums;
using CinemaGo.Domain.Intefraces;

namespace CinemaGo.Tests;

public class SessionServiceTests
{
    private static readonly DateTime Start = new(2030, 1, 1, 18, 0, 0);

    private readonly FakeSessionRepository _sessions = new();
    private readonly FakeMovieRepository _movies = new();
    private readonly FakeHallRepository _halls = new();
    private readonly FakeUnitOfWork _uow = new();
    private readonly SessionService _service;

    public SessionServiceTests()
    {
        var mapper = new MapperConfiguration(c => c.AddProfile<SessionProfile>()).CreateMapper();
        _service = new SessionService(_sessions, _movies, _halls, _uow, mapper);

        _movies.Items[1] = new Movie { Id = 1, Title = "Dune", DurationMinutes = 120 };
        _halls.Items[1] = new Hall { Id = 1, Name = "Hall A" };
    }

    private static CreateSessionDTO NewCreateDto(DateTime? startsAt = null) => new()
    {
        MovieId = 1,
        HallId = 1,
        StartsAt = startsAt ?? Start,
        StandardPrice = 10,
        VipPrice = 20
    };

    private Session AddExisting(int id, DateTime startsAt)
    {
        var session = new Session
        {
            Id = id,
            MovieId = 1,
            Movie = _movies.Items[1],
            HallId = 1,
            Hall = _halls.Items[1],
            StartsAt = startsAt,
            StandardPrice = 10,
            VipPrice = 20
        };
        _sessions.Items.Add(session);
        return session;
    }

    [Fact]
    public async Task GetAllSessions_ReturnsListOfDtos()
    {
        AddExisting(1, Start);
        AddExisting(2, Start.AddDays(1));

        var result = await _service.GetAllSessionsAsync(CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.All(result, s =>
        {
            Assert.Equal("Dune", s.MovieTitle);
            Assert.Equal("Hall A", s.HallName);
        });
    }

    [Fact]
    public async Task GetSessionById_NonExistent_ReturnsNull()
    {
        Assert.Null(await _service.GetSessionByIdAsync(99, CancellationToken.None));
    }

    [Fact]
    public async Task AddSession_NonExistentMovie_ThrowsArgumentException()
    {
        var dto = NewCreateDto();
        dto.MovieId = 42;

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.AddSessionAsync(dto, CancellationToken.None));
    }

    [Fact]
    public async Task AddSession_NegativePrice_ThrowsArgumentException()
    {
        var dto = NewCreateDto();
        dto.StandardPrice = -1;

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.AddSessionAsync(dto, CancellationToken.None));
    }

    [Fact]
    public async Task AddSession_OverlappingInSameHall_ThrowsArgumentException()
    {
        AddExisting(1, Start);

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.AddSessionAsync(NewCreateDto(Start.AddMinutes(60)), CancellationToken.None));
    }

    [Fact]
    public async Task UpdateSession_WithActiveBookings_ThrowsArgumentException()
    {
        var session = AddExisting(1, Start);
        session.Bookings.Add(new Booking { Status = BookingStatus.Active });

        var dto = new UpdateSessionDTO
        {
            MovieId = 1,
            HallId = 1,
            StartsAt = Start,
            StandardPrice = 10,
            VipPrice = 20
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.UpdateSessionAsync(1, dto, CancellationToken.None));
    }

    [Fact]
    public async Task DeleteSession_WithNoBookings_ReturnsTrueAndSaves()
    {
        AddExisting(1, Start);

        var result = await _service.DeleteSessionAsync(1, CancellationToken.None);

        Assert.True(result);
        Assert.Equal(1, _uow.SaveCount);
    }

    private sealed class FakeSessionRepository : ISessionRepository
    {
        public List<Session> Items { get; } = new();

        public Task<ICollection<Session>> GetAllSessionsAsync(CancellationToken ct) =>
            Task.FromResult<ICollection<Session>>(Items.ToList());

        public Task<Session?> GetSessionByIdAsync(int id, CancellationToken ct) =>
            Task.FromResult(Items.FirstOrDefault(s => s.Id == id));

        public Task<ICollection<Session>> GetSessionsByHallAsync(int hallId, CancellationToken ct) =>
            Task.FromResult<ICollection<Session>>(Items.Where(s => s.HallId == hallId).ToList());

        public Task AddSessionAsync(Session session, CancellationToken ct)
        {
            Items.Add(session);
            return Task.CompletedTask;
        }

        public void UpdateSessionAsync(Session session) { }

        public void DeleteSessionAsync(Session session) => Items.Remove(session);
    }

    private sealed class FakeMovieRepository : IMovieRepository
    {
        public Dictionary<int, Movie> Items { get; } = new();

        public Task<ICollection<Movie>> GetAllMoviesAsync(CancellationToken ct) =>
            Task.FromResult<ICollection<Movie>>(Items.Values.ToList());

        public Task<Movie?> GetMovieByIdAsync(int id, CancellationToken ct) =>
            Task.FromResult(Items.GetValueOrDefault(id));

        public Task AddMovieAsync(Movie movie, CancellationToken ct) => Task.CompletedTask;
        public void UpdateMovieAsync(Movie movie) { }
        public void DeleteMovieAsync(Movie movie) { }
    }

    private sealed class FakeHallRepository : IHallRepository
    {
        public Dictionary<int, Hall> Items { get; } = new();

        public Task<ICollection<Hall>> GetAllHallsAsync(CancellationToken ct) =>
            Task.FromResult<ICollection<Hall>>(Items.Values.ToList());

        public Task<Hall?> GetHallByIdAsync(int id, CancellationToken ct) =>
            Task.FromResult(Items.GetValueOrDefault(id));

        public Task AddHallAsync(Hall hall, CancellationToken ct) => Task.CompletedTask;
        public void UpdateHallAsync(Hall hall) { }
        public void DeleteHallAsync(Hall hall) { }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveCount { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return Task.FromResult(1);
        }
    }
}
