using AutoMapper;
using CinemaGo.Application.Profiles;
using CinemaGo.Application.Services.Implementations;
using CinemaGo.Domain.DTOs.BookingDTOs;
using CinemaGo.Domain.Entities;
using CinemaGo.Domain.Enums;
using CinemaGo.Domain.Exceptions;
using CinemaGo.Infrastructure.Data;
using CinemaGo.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CinemaGo.Tests;

// Runs the real repositories and mapping against SQLite, so the unique index is actually enforced.
public sealed class BookingPersistenceTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly IMapper _mapper = new MapperConfiguration(
        configuration => configuration.AddProfile<BookingProfile>(),
        NullLoggerFactory.Instance).CreateMapper();

    public BookingPersistenceTests()
    {
        _connection.Open();

        using var context = CreateContext();
        context.Database.EnsureCreated();

        context.Users.AddRange(
            new User { Id = 1, Username = "a", Email = "a@x.com", PasswordHash = "h" },
            new User { Id = 2, Username = "b", Email = "b@x.com", PasswordHash = "h" });
        context.Movies.Add(new Movie { Id = 1, Title = "Dune", DurationMinutes = 120, AgeRating = 12 });
        context.Halls.Add(new Hall { Id = 1, Name = "Hall", Capacity = 10 });
        context.Seats.AddRange(
            new Seat { Id = 1, HallId = 1, RowNumber = 1, SeatNumber = 1 },
            new Seat { Id = 2, HallId = 1, RowNumber = 1, SeatNumber = 2 });
        context.Sessions.Add(new Session
        {
            Id = 1,
            MovieId = 1,
            HallId = 1,
            StartsAt = DateTime.UtcNow.AddDays(1),
            StandardPrice = 10,
            VipPrice = 20
        });
        context.SaveChanges();
    }

    private CinemaDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<CinemaDbContext>().UseSqlite(_connection).Options);

    private BookingService CreateService(CinemaDbContext context) => new(
        new BookingRepository(context),
        new SessionRepository(context),
        new SeatRepository(context),
        new CinemaGo.Infrastructure.UnitOfWork.UnitOfWork(context),
        _mapper);

    private static CreateBookingDTO Dto => new() { SessionId = 1, SeatId = 1 };

    [Fact]
    public async Task BookBatch_PersistsBookingsAndTicketsTogether()
    {
        using var context = CreateContext();
        var service = CreateService(context);

        var bookings = await service.BookTicketsAsync(
            new CreateBookingsDTO { SessionId = 1, SeatIds = [1, 2] },
            1,
            CancellationToken.None);

        Assert.Equal(2, bookings.Count);
        Assert.Equal(2, await context.Bookings.CountAsync());
        Assert.Equal(2, await context.Tickets.CountAsync());
        Assert.Equal(2, bookings.Select(booking => booking.TicketCode).Distinct().Count());
    }

    [Fact]
    public async Task BookBatch_OneUnavailableSeatPersistsNoNewBookings()
    {
        using (var setup = CreateContext())
        {
            setup.Bookings.Add(new Booking { UserId = 2, SessionId = 1, SeatId = 2 });
            await setup.SaveChangesAsync();
        }

        using var context = CreateContext();
        var service = CreateService(context);

        await Assert.ThrowsAsync<ConflictException>(() => service.BookTicketsAsync(
            new CreateBookingsDTO { SessionId = 1, SeatIds = [1, 2] },
            1,
            CancellationToken.None));

        Assert.Equal(1, await context.Bookings.CountAsync());
        Assert.False(await context.Bookings.AnyAsync(booking => booking.SeatId == 1));
    }

    [Fact]
    public async Task Book_SameSeatTwice_SecondIsRejectedByService()
    {
        using var context = CreateContext();
        var service = CreateService(context);

        await service.BookTicketAsync(Dto, 1, CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(
            () => service.BookTicketAsync(Dto, 2, CancellationToken.None));
    }

    [Fact]
    public async Task Book_ConcurrentRequests_UniqueIndexRejectsSecondBooking()
    {
        // Both contexts pass the "is the seat free" check before either one saves.
        using var first = CreateContext();
        using var second = CreateContext();

        first.Bookings.Add(new Booking { UserId = 1, SessionId = 1, SeatId = 1 });
        second.Bookings.Add(new Booking { UserId = 2, SessionId = 1, SeatId = 1 });

        await first.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync());

        using var verify = CreateContext();
        Assert.Equal(1, await verify.Bookings.CountAsync());
    }

    [Fact]
    public async Task Cancel_ThenBookAgain_Succeeds()
    {
        using var context = CreateContext();
        var service = CreateService(context);

        var booking = await service.BookTicketAsync(Dto, 1, CancellationToken.None);
        await service.CancelBookingAsync(booking.Id, 1, false, CancellationToken.None);

        var rebooked = await service.BookTicketAsync(Dto, 2, CancellationToken.None);

        Assert.NotEqual(booking.Id, rebooked.Id);

        using var verify = CreateContext();
        Assert.Equal(2, await verify.Bookings.CountAsync());
        Assert.Equal(1, await verify.Bookings.CountAsync(b => b.Status == BookingStatus.Active));
    }

    [Fact]
    public async Task Book_TicketSaveFails_NeitherBookingNorTicketIsPersisted()
    {
        using var context = CreateContext();
        var booking = new Booking { UserId = 1, SessionId = 1, SeatId = 1 };
        // A negative price violates CK_Tickets_FinalPrice.
        booking.Ticket = new Ticket { Booking = booking, TicketCode = "code", FinalPrice = -1 };
        context.Bookings.Add(booking);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

        using var verify = CreateContext();
        Assert.Equal(0, await verify.Bookings.CountAsync());
        Assert.Equal(0, await verify.Tickets.CountAsync());
    }

    public void Dispose() => _connection.Dispose();
}
