using AutoMapper;
using CinemaGo.Application.Profiles;
using CinemaGo.Application.Services.Implementations;
using CinemaGo.Domain.DTOs.SessionDTOs;
using CinemaGo.Domain.Entities;
using CinemaGo.Domain.Enums;
using CinemaGo.Domain.Exceptions;
using CinemaGo.Domain.Intefraces;

namespace CinemaGo.Tests;

public class SessionServiceTests
{
    private static readonly DateTime Start = new(2030, 1, 1, 18, 0, 0, DateTimeKind.Utc);

    private readonly FakeSessionRepository _sessions = new();
    private readonly FakeMovieRepository _movies = new();
    private readonly FakeHallRepository _halls = new();
    private readonly FakeSeatRepository _seats = new();
    private readonly FakeUnitOfWork _uow = new();
    private readonly SessionService _service;

    public SessionServiceTests()
    {
        var mapper = new MapperConfiguration(c => c.AddProfile<SessionProfile>(), Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance).CreateMapper();
        _service = new SessionService(_sessions, _movies, _halls, _seats, _uow, mapper);

        _movies.Items[1] = new Movie { Id = 1, Title = "Dune", DurationMinutes = 120 };
        _sessions.Movies = _movies.Items;
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
    public async Task AddSession_ConcurrentOverlappingRequests_OnlyOneSucceeds()
    {
        var tasks = Enumerable.Range(0, 5)
            .Select(_ => Task.Run(async () =>
            {
                try
                {
                    await _service.AddSessionAsync(NewCreateDto(), CancellationToken.None);
                    return true;
                }
                catch (ConflictException)
                {
                    return false;
                }
            }))
            .ToList();

        var results = await Task.WhenAll(tasks);

        Assert.Equal(1, results.Count(r => r));
        Assert.Single(_sessions.Items);
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
    public async Task AddSession_NonExistentMovie_ThrowsNotFound()
    {
        var dto = NewCreateDto();
        dto.MovieId = 42;

        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.AddSessionAsync(dto, CancellationToken.None));
    }

    [Fact]
    public async Task AddSession_NegativePrice_ThrowsValidation()
    {
        var dto = NewCreateDto();
        dto.StandardPrice = -1;

        await Assert.ThrowsAsync<ValidationException>(
            () => _service.AddSessionAsync(dto, CancellationToken.None));
    }

    [Fact]
    public async Task AddSession_OverlappingInSameHall_ThrowsConflict()
    {
        AddExisting(1, Start);

        await Assert.ThrowsAsync<ConflictException>(
            () => _service.AddSessionAsync(NewCreateDto(Start.AddMinutes(60)), CancellationToken.None));
    }

    [Fact]
    public async Task AddSession_StartsExactlyWhenPreviousEnds_Succeeds()
    {
        AddExisting(1, Start);

        var result = await _service.AddSessionAsync(
            NewCreateDto(Start.AddMinutes(120)), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(2, _sessions.Items.Count);
    }

    [Fact]
    public async Task AddSession_EndsIntoNextSession_ThrowsConflict()
    {
        AddExisting(1, Start.AddMinutes(60));

        await Assert.ThrowsAsync<ConflictException>(
            () => _service.AddSessionAsync(NewCreateDto(Start), CancellationToken.None));
    }

    [Fact]
    public async Task AddSession_OverlapsCancelledSession_Succeeds()
    {
        AddExisting(1, Start).Status = SessionStatus.Cancelled;

        await _service.AddSessionAsync(NewCreateDto(), CancellationToken.None);

        Assert.Equal(2, _sessions.Items.Count);
    }

    [Fact]
    public async Task AddSession_SameTimeInOtherHall_Succeeds()
    {
        _halls.Items[2] = new Hall { Id = 2, Name = "Hall B" };
        AddExisting(1, Start);
        var dto = NewCreateDto();
        dto.HallId = 2;

        await _service.AddSessionAsync(dto, CancellationToken.None);

        Assert.Equal(2, _sessions.Items.Count);
    }

    [Fact]
    public async Task AddSession_VipPriceLowerThanStandard_ThrowsValidation()
    {
        var dto = NewCreateDto();
        dto.StandardPrice = 20;
        dto.VipPrice = 10;

        await Assert.ThrowsAsync<ValidationException>(
            () => _service.AddSessionAsync(dto, CancellationToken.None));
    }

    [Fact]
    public async Task AddSession_DefaultStartTime_ThrowsValidation()
    {
        var dto = NewCreateDto();
        dto.StartsAt = default;

        await Assert.ThrowsAsync<ValidationException>(
            () => _service.AddSessionAsync(dto, CancellationToken.None));
    }

    [Fact]
    public async Task AddSession_NonExistentHall_ThrowsNotFound()
    {
        var dto = NewCreateDto();
        dto.HallId = 99;

        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.AddSessionAsync(dto, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateSession_OverlappingAnotherSession_ThrowsConflict()
    {
        AddExisting(1, Start);
        AddExisting(2, Start.AddDays(1));
        var dto = new UpdateSessionDTO { MovieId = 1, HallId = 1, StartsAt = Start.AddMinutes(30), StandardPrice = 10, VipPrice = 20 };

        await Assert.ThrowsAsync<ConflictException>(
            () => _service.UpdateSessionAsync(2, dto, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateSession_OverlappingItself_Succeeds()
    {
        AddExisting(1, Start);
        var dto = new UpdateSessionDTO { MovieId = 1, HallId = 1, StartsAt = Start.AddMinutes(30), StandardPrice = 10, VipPrice = 20 };

        var result = await _service.UpdateSessionAsync(1, dto, CancellationToken.None);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task UpdateSession_InvalidPrices_ThrowsValidation()
    {
        AddExisting(1, Start);
        var dto = new UpdateSessionDTO { MovieId = 1, HallId = 1, StartsAt = Start, StandardPrice = 20, VipPrice = 10 };

        await Assert.ThrowsAsync<ValidationException>(
            () => _service.UpdateSessionAsync(1, dto, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateSession_Unknown_ReturnsNull()
    {
        var dto = new UpdateSessionDTO { MovieId = 1, HallId = 1, StartsAt = Start, StandardPrice = 10, VipPrice = 20 };

        Assert.Null(await _service.UpdateSessionAsync(99, dto, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateSession_WithActiveBookings_ThrowsConflict()
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

        await Assert.ThrowsAsync<ConflictException>(
            () => _service.UpdateSessionAsync(1, dto, CancellationToken.None));
    }

    [Fact]
    public async Task AddSession_InThePast_ThrowsValidation()
    {
        await Assert.ThrowsAsync<ValidationException>(
            () => _service.AddSessionAsync(
                NewCreateDto(new DateTime(2000, 1, 1, 18, 0, 0, DateTimeKind.Utc)), CancellationToken.None));
    }

    [Fact]
    public async Task AddSession_UnspecifiedKind_ThrowsValidation()
    {
        await Assert.ThrowsAsync<ValidationException>(
            () => _service.AddSessionAsync(
                NewCreateDto(new DateTime(2030, 1, 1, 18, 0, 0)), CancellationToken.None));
    }

    [Fact]
    public async Task DeleteSession_WithNoBookings_ReturnsTrueAndSaves()
    {
        AddExisting(1, Start);

        var result = await _service.DeleteSessionAsync(1, CancellationToken.None);

        Assert.True(result);
        Assert.Equal(1, _uow.SaveCount);
    }

    [Fact]
    public async Task GetSessionSeats_NonExistentSession_ReturnsNull()
    {
        Assert.Null(await _service.GetSessionSeatsAsync(99, CancellationToken.None));
    }

    [Fact]
    public async Task GetSessionSeats_NoBookings_AllSeatsAvailable()
    {
        AddExisting(1, Start);
        _seats.Items.Add(new Seat { Id = 1, HallId = 1, RowNumber = 1, SeatNumber = 1 });
        _seats.Items.Add(new Seat { Id = 2, HallId = 1, RowNumber = 1, SeatNumber = 2, IsVip = true });
        _seats.Items.Add(new Seat { Id = 3, HallId = 2, RowNumber = 1, SeatNumber = 1 });

        var result = await _service.GetSessionSeatsAsync(1, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(2, result!.Count);
        Assert.All(result, s => Assert.True(s.IsAvailable));
    }

    [Fact]
    public async Task GetSessionSeats_ActiveBooking_SeatIsOccupied()
    {
        var session = AddExisting(1, Start);
        _seats.Items.Add(new Seat { Id = 1, HallId = 1, RowNumber = 1, SeatNumber = 1 });
        _seats.Items.Add(new Seat { Id = 2, HallId = 1, RowNumber = 1, SeatNumber = 2 });
        session.Bookings.Add(new Booking { SessionId = 1, SeatId = 1, Status = BookingStatus.Active });

        var result = await _service.GetSessionSeatsAsync(1, CancellationToken.None);

        Assert.False(result!.Single(s => s.SeatId == 1).IsAvailable);
        Assert.True(result!.Single(s => s.SeatId == 2).IsAvailable);
    }

    [Fact]
    public async Task GetSessionSeats_CancelledBooking_SeatIsAvailable()
    {
        var session = AddExisting(1, Start);
        _seats.Items.Add(new Seat { Id = 1, HallId = 1, RowNumber = 1, SeatNumber = 1 });
        session.Bookings.Add(new Booking { SessionId = 1, SeatId = 1, Status = BookingStatus.Cancelled });

        var result = await _service.GetSessionSeatsAsync(1, CancellationToken.None);

        Assert.True(result!.Single().IsAvailable);
    }

    private sealed class FakeSeatRepository : ISeatRepository
    {
        public List<Seat> Items { get; } = new();

        public Task<ICollection<Seat>> GetAllSeatsAsync(CancellationToken ct) =>
            Task.FromResult<ICollection<Seat>>(Items.ToList());

        public Task<ICollection<Seat>> GetSeatsByHallIdAsync(int hallId, CancellationToken ct) =>
            Task.FromResult<ICollection<Seat>>(Items.Where(s => s.HallId == hallId).ToList());

        public Task<Seat?> GetSeatByIdAsync(int id, CancellationToken ct) =>
            Task.FromResult(Items.FirstOrDefault(s => s.Id == id));

        public Task AddSeatAsync(Seat seat, CancellationToken ct) => Task.CompletedTask;
        public void UpdateSeatAsync(Seat seat) { }
        public void DeleteSeatAsync(Seat seat) { }
    }

    private sealed class FakeSessionRepository : ISessionRepository
    {
        public List<Session> Items { get; } = new();

        public Dictionary<int, Movie>? Movies { get; set; }

        public Task<ICollection<Session>> GetAllSessionsAsync(CancellationToken ct) =>
            Task.FromResult<ICollection<Session>>(Items.ToList());

        public Task<(ICollection<Session> Sessions, int TotalCount)> GetSessionsPagedAsync(int? movieId, DateTime? dateFrom, DateTime? dateTo, int page, int pageSize, CancellationToken ct)
        {
            var query = Items
                .Where(s => !movieId.HasValue || s.MovieId == movieId)
                .Where(s => !dateFrom.HasValue || s.StartsAt >= dateFrom)
                .Where(s => !dateTo.HasValue || s.StartsAt <= dateTo)
                .OrderBy(s => s.StartsAt).ThenBy(s => s.Id)
                .ToList();
            return Task.FromResult<(ICollection<Session>, int)>(
                (query.Skip((page - 1) * pageSize).Take(pageSize).ToList(), query.Count));
        }

        public Task<Session?> GetSessionByIdAsync(int id, CancellationToken ct) =>
            Task.FromResult(Items.FirstOrDefault(s => s.Id == id));

        public async Task<ICollection<Session>> GetSessionsByHallAsync(int hallId, CancellationToken ct)
        {
            var snapshot = Items.Where(s => s.HallId == hallId).ToList();
            // Widen the check-then-write window so unprotected callers would interleave.
            await Task.Delay(50, ct);
            return snapshot;
        }

        public Task AddSessionAsync(Session session, CancellationToken ct)
        {
            session.Movie ??= Movies?.GetValueOrDefault(session.MovieId)!;
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

        public Task<(ICollection<Movie> Movies, int TotalCount)> GetMoviesPagedAsync(string? title, int? genreId, int page, int pageSize, CancellationToken ct) =>
            Task.FromResult<(ICollection<Movie>, int)>((Items.Values.ToList(), Items.Count));

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

        // Simulates the database lock a Serializable transaction holds until commit.
        private readonly SemaphoreSlim _gate = new(1, 1);

        public async Task<T> ExecuteInSerializableTransactionAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default)
        {
            await _gate.WaitAsync(cancellationToken);
            try
            {
                return await action();
            }
            finally
            {
                _gate.Release();
            }
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return Task.FromResult(1);
        }
    }
}
