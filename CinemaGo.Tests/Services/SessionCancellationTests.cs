using CinemaGo.Application.Services.Implementations;
using CinemaGo.Domain.Entities;
using CinemaGo.Domain.Enums;
using CinemaGo.Domain.Exceptions;
using CinemaGo.Domain.Intefraces;
using CinemaGo.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using CinemaGo.Tests.TestInfrastructure;

namespace CinemaGo.Tests.Services;

public sealed class SessionCancellationTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private readonly TestDb _db = new();
    private readonly SessionService _service;
    private readonly Session _session;
    private readonly RecordingSessionCancellationEmailSender _emailSender = new();

    public SessionCancellationTests()
    {
        var ctx = _db.Context;
        _service = new SessionService(new SessionRepository(ctx), new MovieRepository(ctx), new HallRepository(ctx), new SeatRepository(ctx), _db.UnitOfWork, _db.Mapper, NullLogger<SessionService>.Instance, _emailSender);

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
        Assert.Equal(new[] { "u@x.com" }, _emailSender.Recipients);
        Assert.Equal(_session.Id, _emailSender.SessionId);
        Assert.Equal("M", _emailSender.MovieTitle);
        _db.Context.ChangeTracker.Clear();
        Assert.Equal(SessionStatus.Cancelled, (await _db.Context.Sessions.SingleAsync()).Status);
        Assert.All(await _db.Context.Bookings.ToListAsync(), b => Assert.Equal(BookingStatus.Cancelled, b.Status));
    }

    private sealed class RecordingSessionCancellationEmailSender : ISessionCancellationEmailSender
    {
        public List<string> Recipients { get; private set; } = [];
        public int SessionId { get; private set; }
        public string MovieTitle { get; private set; } = string.Empty;

        public Task SendAsync(
            IEnumerable<string> recipients,
            int sessionId,
            string movieTitle,
            DateTime startsAt,
            CancellationToken cancellationToken)
        {
            Recipients = recipients.ToList();
            SessionId = sessionId;
            MovieTitle = movieTitle;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Cancel_FreesSeatsInAvailability()
    {
        await _service.CancelSessionAsync(_session.Id, Ct);

        var seats = await _service.GetSessionSeatsAsync(_session.Id, Ct);
        Assert.All(seats!, s => Assert.True(s.IsAvailable));
    }

    [Fact]
    public async Task Cancel_WithoutActiveBookings_DoesNotSendEmail()
    {
        _db.Context.Bookings.Where(booking => booking.Status == BookingStatus.Active)
            .ToList()
            .ForEach(booking => booking.Status = BookingStatus.Cancelled);
        await _db.Context.SaveChangesAsync();
        _emailSender.Recipients.Clear();

        await _service.CancelSessionAsync(_session.Id, Ct);

        Assert.Empty(_emailSender.Recipients);
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
