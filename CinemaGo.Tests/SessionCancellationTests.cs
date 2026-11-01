using CinemaGo.Application.Services.Implementations;
using CinemaGo.Domain.Entities;
using CinemaGo.Domain.Enums;
using CinemaGo.Domain.Exceptions;
using CinemaGo.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace CinemaGo.Tests;

public sealed class SessionCancellationTests : IDisposable
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private readonly TestDb _db = new();
    private readonly SessionService _service;
    private readonly Session _session;

    public SessionCancellationTests()
    {
        var ctx = _db.Context;
        _service = new SessionService(new SessionRepository(ctx), new MovieRepository(ctx), new HallRepository(ctx), new SeatRepository(ctx), _db.UnitOfWork, _db.Mapper);

        var hall = new Hall { Name = "H", Capacity = 3 };
        for (var i = 1; i <= 3; i++)
        {
            hall.Seats.Add(new Seat { RowNumber = 1, SeatNumber = i });
        }
        var user = new User { Username = "u", Email = "u@x.com", PasswordHash = "h" };
        _session = new Session { Movie = new Movie { Title = "M", DurationMinutes = 90 }, Hall = hall, StartsAt = DateTime.UtcNow.AddDays(1), StandardPrice = 1, VipPrice = 2 };
        var seats = hall.Seats.ToList();
        _session.Bookings.Add(new Booking { User = user, Seat = seats[0], Status = BookingStatus.Active });
        _session.Bookings.Add(new Booking { User = user, Seat = seats[1], Status = BookingStatus.Active });
        _session.Bookings.Add(new Booking { User = user, Seat = seats[2], Status = BookingStatus.Cancelled });
        ctx.Sessions.Add(_session);
        ctx.SaveChanges();
        ctx.ChangeTracker.Clear();
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Cancel_SetsStatus_AndCancelsAllActiveBookings()
    {
        var result = await _service.CancelSessionAsync(_session.Id, Ct);

        Assert.Equal(SessionStatus.Cancelled, result!.Status);
        _db.Context.ChangeTracker.Clear();
        Assert.Equal(SessionStatus.Cancelled, (await _db.Context.Sessions.SingleAsync()).Status);
        Assert.All(await _db.Context.Bookings.ToListAsync(), b => Assert.Equal(BookingStatus.Cancelled, b.Status));
    }

    [Fact]
    public async Task Cancel_FreesSeatsInAvailability()
    {
        await _service.CancelSessionAsync(_session.Id, Ct);

        var seats = await _service.GetSessionSeatsAsync(_session.Id, Ct);
        Assert.All(seats!, s => Assert.True(s.IsAvailable));
    }

    [Fact]
    public async Task Cancel_AlreadyCancelled_ThrowsConflict()
    {
        await _service.CancelSessionAsync(_session.Id, Ct);
        await Assert.ThrowsAsync<ConflictException>(() => _service.CancelSessionAsync(_session.Id, Ct));
    }

    [Fact]
    public async Task Cancel_Completed_ThrowsValidation()
    {
        _db.Context.Sessions.Single().Status = SessionStatus.Completed;
        await _db.Context.SaveChangesAsync();

        await Assert.ThrowsAsync<ValidationException>(() => _service.CancelSessionAsync(_session.Id, Ct));
    }

    [Fact]
    public async Task Cancel_UnknownSession_ReturnsNull()
    {
        Assert.Null(await _service.CancelSessionAsync(999, Ct));
    }
}
