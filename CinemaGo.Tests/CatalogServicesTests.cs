using CinemaGo.Application.Services.Implementations;
using CinemaGo.Domain.DTOs.GenreDTOs;
using CinemaGo.Domain.DTOs.HallDTOs;
using CinemaGo.Domain.DTOs.MovieDTOs;
using CinemaGo.Domain.DTOs.SeatDTOs;
using CinemaGo.Domain.Entities;
using CinemaGo.Domain.Enums;
using CinemaGo.Domain.Exceptions;
using CinemaGo.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace CinemaGo.Tests;

public sealed class CatalogServicesTests : IDisposable
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private readonly TestDb _db = new();
    private readonly GenreService _genres;
    private readonly HallService _halls;
    private readonly SeatService _seats;
    private readonly MovieService _movies;

    public CatalogServicesTests()
    {
        var ctx = _db.Context;
        _genres = new GenreService(new GenreRepository(ctx), _db.UnitOfWork, _db.Mapper);
        _halls = new HallService(new HallRepository(ctx), new SeatRepository(ctx), new SessionRepository(ctx), _db.UnitOfWork, _db.Mapper);
        _seats = new SeatService(new SeatRepository(ctx), new HallRepository(ctx), _db.UnitOfWork, _db.Mapper);
        _movies = new MovieService(new MovieRepository(ctx), new GenreRepository(ctx), _db.UnitOfWork, _db.Mapper);
    }

    public void Dispose() => _db.Dispose();

    // ---- Genres ----

    [Fact]
    public async Task Genre_Crud_RoundTrip()
    {
        var created = await _genres.AddGenreAsync(new CreateGenreDTO { Name = "  Drama " }, Ct);
        Assert.Equal("Drama", created.Name);

        var updated = await _genres.UpdateGenreAsync(created.Id, new UpdateGenreDTO { Name = "Comedy" }, Ct);
        Assert.Equal("Comedy", updated!.Name);
        Assert.Equal("Comedy", (await _genres.GetGenreByIdAsync(created.Id, Ct))!.Name);
        Assert.Single(await _genres.GetAllGenresAsync(Ct));

        Assert.True(await _genres.DeleteGenreAsync(created.Id, Ct));
        Assert.Null(await _genres.GetGenreByIdAsync(created.Id, Ct));
    }

    [Fact]
    public async Task Genre_DuplicateName_IgnoringCase_ThrowsConflict()
    {
        await _genres.AddGenreAsync(new CreateGenreDTO { Name = "Drama" }, Ct);
        await Assert.ThrowsAsync<ConflictException>(() => _genres.AddGenreAsync(new CreateGenreDTO { Name = "drama" }, Ct));
    }

    [Fact]
    public async Task Genre_UpdateToOtherGenresName_ThrowsConflict_ButSameNameIsAllowed()
    {
        var a = await _genres.AddGenreAsync(new CreateGenreDTO { Name = "A" }, Ct);
        await _genres.AddGenreAsync(new CreateGenreDTO { Name = "B" }, Ct);

        await Assert.ThrowsAsync<ConflictException>(() => _genres.UpdateGenreAsync(a.Id, new UpdateGenreDTO { Name = "b" }, Ct));
        Assert.NotNull(await _genres.UpdateGenreAsync(a.Id, new UpdateGenreDTO { Name = "A" }, Ct));
    }

    [Fact]
    public async Task Genre_EmptyName_ThrowsValidation()
    {
        await Assert.ThrowsAsync<ValidationException>(() => _genres.AddGenreAsync(new CreateGenreDTO { Name = "  " }, Ct));
    }

    [Fact]
    public async Task Genre_Missing_UpdateReturnsNull_DeleteReturnsFalse()
    {
        Assert.Null(await _genres.UpdateGenreAsync(99, new UpdateGenreDTO { Name = "X" }, Ct));
        Assert.False(await _genres.DeleteGenreAsync(99, Ct));
    }

    [Fact]
    public async Task Genre_AssignedToMovie_CannotBeDeleted()
    {
        var genre = await _genres.AddGenreAsync(new CreateGenreDTO { Name = "Drama" }, Ct);
        await _movies.AddMovieAsync(new CreateMovieDTO { Title = "M", DurationMinutes = 90, AgeRating = 12, GenreIds = [genre.Id] }, Ct);

        await Assert.ThrowsAsync<ConflictException>(() => _genres.DeleteGenreAsync(genre.Id, Ct));
    }

    // ---- Halls ----

    [Fact]
    public async Task Hall_Crud_RoundTrip()
    {
        var created = await _halls.AddHallAsync(new CreateHallDTO { Name = " Hall A ", Capacity = 10 }, Ct);
        Assert.Equal("Hall A", created.Name);

        var updated = await _halls.UpdateHallAsync(created.Id, new UpdateHallDTO { Name = "Hall B", Capacity = 20 }, Ct);
        Assert.Equal(20, updated!.Capacity);
        Assert.Equal("Hall B", (await _halls.GetHallByIdAsync(created.Id, Ct))!.Name);
        Assert.Single(await _halls.GetAllHallsAsync(Ct));

        Assert.True(await _halls.DeleteHallAsync(created.Id, Ct));
        Assert.Null(await _halls.GetHallByIdAsync(created.Id, Ct));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Hall_InvalidCapacity_ThrowsValidation(int capacity)
    {
        await Assert.ThrowsAsync<ValidationException>(() => _halls.AddHallAsync(new CreateHallDTO { Name = "H", Capacity = capacity }, Ct));
    }

    [Fact]
    public async Task Hall_DuplicateName_ThrowsConflict()
    {
        await _halls.AddHallAsync(new CreateHallDTO { Name = "H", Capacity = 5 }, Ct);
        await Assert.ThrowsAsync<ConflictException>(() => _halls.AddHallAsync(new CreateHallDTO { Name = "h", Capacity = 5 }, Ct));
    }

    [Fact]
    public async Task Hall_CapacityBelowSeatCount_ThrowsConflict()
    {
        var hall = await _halls.AddHallAsync(new CreateHallDTO { Name = "H", Capacity = 5 }, Ct);
        for (var i = 1; i <= 3; i++)
        {
            await _seats.AddSeatAsync(new CreateSeatDTO { HallId = hall.Id, RowNumber = 1, SeatNumber = i }, Ct);
        }

        await Assert.ThrowsAsync<ConflictException>(() => _halls.UpdateHallAsync(hall.Id, new UpdateHallDTO { Name = "H", Capacity = 2 }, Ct));
        Assert.NotNull(await _halls.UpdateHallAsync(hall.Id, new UpdateHallDTO { Name = "H", Capacity = 3 }, Ct));
    }

    [Fact]
    public async Task Hall_WithSeats_CannotBeDeleted()
    {
        var hall = await _halls.AddHallAsync(new CreateHallDTO { Name = "H", Capacity = 5 }, Ct);
        await _seats.AddSeatAsync(new CreateSeatDTO { HallId = hall.Id, RowNumber = 1, SeatNumber = 1 }, Ct);

        await Assert.ThrowsAsync<ConflictException>(() => _halls.DeleteHallAsync(hall.Id, Ct));
    }

    [Fact]
    public async Task Hall_WithSessions_CannotBeDeleted()
    {
        var hall = await _halls.AddHallAsync(new CreateHallDTO { Name = "H", Capacity = 5 }, Ct);
        var movie = new Movie { Title = "M", DurationMinutes = 90, AgeRating = 0 };
        _db.Context.Sessions.Add(new Session { Movie = movie, HallId = hall.Id, StartsAt = DateTime.UtcNow.AddDays(1), StandardPrice = 1, VipPrice = 2 });
        await _db.Context.SaveChangesAsync();

        await Assert.ThrowsAsync<ConflictException>(() => _halls.DeleteHallAsync(hall.Id, Ct));
    }

    [Fact]
    public async Task Hall_Missing_UpdateReturnsNull_DeleteReturnsFalse()
    {
        Assert.Null(await _halls.UpdateHallAsync(99, new UpdateHallDTO { Name = "X", Capacity = 1 }, Ct));
        Assert.False(await _halls.DeleteHallAsync(99, Ct));
    }

    // ---- Seats ----

    private async Task<int> NewHallAsync(int capacity = 10) =>
        (await _halls.AddHallAsync(new CreateHallDTO { Name = $"Hall {Guid.NewGuid():N}", Capacity = capacity }, Ct)).Id;

    [Fact]
    public async Task Seat_Crud_RoundTrip()
    {
        var hallId = await NewHallAsync();
        var created = await _seats.AddSeatAsync(new CreateSeatDTO { HallId = hallId, RowNumber = 2, SeatNumber = 3, IsVip = true }, Ct);
        Assert.Equal(hallId, created.HallId);
        Assert.True(created.IsVip);

        var updated = await _seats.UpdateSeatAsync(created.Id, new UpdateSeatDTO { RowNumber = 4, SeatNumber = 5, IsVip = false }, Ct);
        Assert.Equal((4, 5, false), (updated!.RowNumber, updated.SeatNumber, updated.IsVip));
        Assert.Equal(4, (await _seats.GetSeatByIdAsync(created.Id, Ct))!.RowNumber);

        Assert.True(await _seats.DeleteSeatAsync(created.Id, Ct));
        Assert.Null(await _seats.GetSeatByIdAsync(created.Id, Ct));
    }

    [Fact]
    public async Task Seat_UnknownHall_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _seats.AddSeatAsync(new CreateSeatDTO { HallId = 99, RowNumber = 1, SeatNumber = 1 }, Ct));
    }

    [Fact]
    public async Task Seat_DuplicatePositionInHall_ThrowsConflict_ButOtherHallIsFine()
    {
        var hall1 = await NewHallAsync();
        var hall2 = await NewHallAsync();
        await _seats.AddSeatAsync(new CreateSeatDTO { HallId = hall1, RowNumber = 1, SeatNumber = 1 }, Ct);

        await Assert.ThrowsAsync<ConflictException>(() => _seats.AddSeatAsync(new CreateSeatDTO { HallId = hall1, RowNumber = 1, SeatNumber = 1 }, Ct));
        Assert.NotNull(await _seats.AddSeatAsync(new CreateSeatDTO { HallId = hall2, RowNumber = 1, SeatNumber = 1 }, Ct));
    }

    [Fact]
    public async Task Seat_HallAtCapacity_ThrowsConflict()
    {
        var hallId = await NewHallAsync(capacity: 1);
        await _seats.AddSeatAsync(new CreateSeatDTO { HallId = hallId, RowNumber = 1, SeatNumber = 1 }, Ct);

        await Assert.ThrowsAsync<ConflictException>(() => _seats.AddSeatAsync(new CreateSeatDTO { HallId = hallId, RowNumber = 1, SeatNumber = 2 }, Ct));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    public async Task Seat_InvalidPosition_ThrowsValidation(int row, int number)
    {
        var hallId = await NewHallAsync();
        await Assert.ThrowsAsync<ValidationException>(() => _seats.AddSeatAsync(new CreateSeatDTO { HallId = hallId, RowNumber = row, SeatNumber = number }, Ct));
    }

    [Fact]
    public async Task Seat_UpdateToTakenPosition_ThrowsConflict()
    {
        var hallId = await NewHallAsync();
        await _seats.AddSeatAsync(new CreateSeatDTO { HallId = hallId, RowNumber = 1, SeatNumber = 1 }, Ct);
        var second = await _seats.AddSeatAsync(new CreateSeatDTO { HallId = hallId, RowNumber = 1, SeatNumber = 2 }, Ct);

        await Assert.ThrowsAsync<ConflictException>(() => _seats.UpdateSeatAsync(second.Id, new UpdateSeatDTO { RowNumber = 1, SeatNumber = 1 }, Ct));
    }

    [Fact]
    public async Task Seat_GetByHall_FiltersAndOrders()
    {
        var hall1 = await NewHallAsync();
        var hall2 = await NewHallAsync();
        await _seats.AddSeatAsync(new CreateSeatDTO { HallId = hall1, RowNumber = 2, SeatNumber = 1 }, Ct);
        await _seats.AddSeatAsync(new CreateSeatDTO { HallId = hall1, RowNumber = 1, SeatNumber = 2 }, Ct);
        await _seats.AddSeatAsync(new CreateSeatDTO { HallId = hall2, RowNumber = 1, SeatNumber = 1 }, Ct);

        var result = (await _seats.GetSeatsAsync(hall1, Ct)).ToList();

        Assert.Equal([(1, 2), (2, 1)], result.Select(s => (s.RowNumber, s.SeatNumber)));
        Assert.Equal(3, (await _seats.GetSeatsAsync(null, Ct)).Count);
    }

    [Fact]
    public async Task Seat_WithBookings_CannotBeDeleted()
    {
        var hallId = await NewHallAsync();
        var seat = await _seats.AddSeatAsync(new CreateSeatDTO { HallId = hallId, RowNumber = 1, SeatNumber = 1 }, Ct);
        var user = new User { Username = "u", Email = "u@x.com", PasswordHash = "h" };
        var session = new Session { Movie = new Movie { Title = "M", DurationMinutes = 90 }, HallId = hallId, StartsAt = DateTime.UtcNow.AddDays(1), StandardPrice = 1, VipPrice = 2 };
        _db.Context.Bookings.Add(new Booking { User = user, Session = session, SeatId = seat.Id, Status = BookingStatus.Cancelled });
        await _db.Context.SaveChangesAsync();

        await Assert.ThrowsAsync<ConflictException>(() => _seats.DeleteSeatAsync(seat.Id, Ct));
    }

    [Fact]
    public async Task Seat_Missing_UpdateReturnsNull_DeleteReturnsFalse()
    {
        Assert.Null(await _seats.UpdateSeatAsync(99, new UpdateSeatDTO { RowNumber = 1, SeatNumber = 1 }, Ct));
        Assert.False(await _seats.DeleteSeatAsync(99, Ct));
    }

    // ---- Movie <-> Genre ----

    private async Task<(int Drama, int Comedy, int Horror)> NewGenresAsync() => (
        (await _genres.AddGenreAsync(new CreateGenreDTO { Name = "Drama" }, Ct)).Id,
        (await _genres.AddGenreAsync(new CreateGenreDTO { Name = "Comedy" }, Ct)).Id,
        (await _genres.AddGenreAsync(new CreateGenreDTO { Name = "Horror" }, Ct)).Id);

    [Fact]
    public async Task Movie_Create_AssignsGenres_AndReturnsThemOrderedByName()
    {
        var (drama, comedy, _) = await NewGenresAsync();

        var created = await _movies.AddMovieAsync(new CreateMovieDTO { Title = "M", DurationMinutes = 90, AgeRating = 12, GenreIds = [drama, comedy, drama] }, Ct);

        Assert.Equal(["Comedy", "Drama"], created.Genres.Select(g => g.Name));
        var loaded = await _movies.GetMovieByIdAsync(created.Id, Ct);
        Assert.Equal(2, loaded!.Genres.Count);
    }

    [Fact]
    public async Task Movie_Create_UnknownGenre_ThrowsValidation_AndSavesNothing()
    {
        await Assert.ThrowsAsync<ValidationException>(() => _movies.AddMovieAsync(new CreateMovieDTO { Title = "M", DurationMinutes = 90, AgeRating = 12, GenreIds = [999] }, Ct));
        Assert.Empty(_db.Context.Movies);
    }

    [Fact]
    public async Task Movie_Create_WithoutGenres_Works()
    {
        var created = await _movies.AddMovieAsync(new CreateMovieDTO { Title = "M", DurationMinutes = 90, AgeRating = 12 }, Ct);
        Assert.Empty(created.Genres);
    }

    [Fact]
    public async Task Movie_Update_ReplacesGenreSet()
    {
        var (drama, comedy, horror) = await NewGenresAsync();
        var movie = await _movies.AddMovieAsync(new CreateMovieDTO { Title = "M", DurationMinutes = 90, AgeRating = 12, GenreIds = [drama, comedy] }, Ct);

        var updated = await _movies.UpdateMovieAsync(movie.Id, new UpdateMovieDTO { Title = "M2", DurationMinutes = 100, AgeRating = 16, GenreIds = [comedy, horror] }, Ct);

        Assert.Equal(["Comedy", "Horror"], updated!.Genres.Select(g => g.Name));
        _db.Context.ChangeTracker.Clear();
        var persisted = await _db.Context.MovieGenres.Where(mg => mg.MovieId == movie.Id).Select(mg => mg.GenreId).ToListAsync();
        Assert.Equal(new[] { comedy, horror }.Order(), persisted.Order());
    }

    [Fact]
    public async Task Movie_Update_EmptyGenreList_RemovesAllGenres()
    {
        var (drama, _, _) = await NewGenresAsync();
        var movie = await _movies.AddMovieAsync(new CreateMovieDTO { Title = "M", DurationMinutes = 90, AgeRating = 12, GenreIds = [drama] }, Ct);

        var updated = await _movies.UpdateMovieAsync(movie.Id, new UpdateMovieDTO { Title = "M", DurationMinutes = 90, AgeRating = 12 }, Ct);

        Assert.Empty(updated!.Genres);
        Assert.Empty(_db.Context.MovieGenres);
    }

    [Fact]
    public async Task Movie_Update_UnknownGenre_ThrowsValidation()
    {
        var movie = await _movies.AddMovieAsync(new CreateMovieDTO { Title = "M", DurationMinutes = 90, AgeRating = 12 }, Ct);
        await Assert.ThrowsAsync<ValidationException>(() => _movies.UpdateMovieAsync(movie.Id, new UpdateMovieDTO { Title = "M", DurationMinutes = 90, AgeRating = 12, GenreIds = [999] }, Ct));
    }

    [Fact]
    public async Task Movie_List_IncludesGenres_AndGenreFilterStillWorks()
    {
        var (drama, comedy, _) = await NewGenresAsync();
        await _movies.AddMovieAsync(new CreateMovieDTO { Title = "A", DurationMinutes = 90, AgeRating = 12, GenreIds = [drama] }, Ct);
        await _movies.AddMovieAsync(new CreateMovieDTO { Title = "B", DurationMinutes = 90, AgeRating = 12, GenreIds = [comedy] }, Ct);

        var page = await _movies.GetMoviesAsync(null, comedy, 1, 10, Ct);

        var movie = Assert.Single(page.Movies);
        Assert.Equal("B", movie.Title);
        Assert.Equal("Comedy", Assert.Single(movie.Genres).Name);
    }
}




