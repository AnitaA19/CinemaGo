using AutoMapper;
using CinemaGo.Application.Profiles;
using CinemaGo.Application.Services.Implementations;
using CinemaGo.Domain.DTOs.BookingDTOs;
using CinemaGo.Domain.DTOs.SessionDTOs;
using CinemaGo.Domain.Enums;
using CinemaGo.Domain.Entities;
using CinemaGo.Domain.Exceptions;
using CinemaGo.Infrastructure.Data;
using CinemaGo.Infrastructure.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CinemaGo.Tests;

// Skipped when no SQL Server is reachable. Override the server with the CINEMAGO_TEST_SQL environment variable.
public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        try
        {
            using var connection = new SqlConnection(SessionConcurrencySqlServerTests.MasterConnectionString);
            connection.Open();
        }
        catch (Exception ex)
        {
            Skip = "SQL Server is not available: " + ex.Message;
        }
    }
}

// Runs real parallel requests against SQL Server so the Serializable transaction is genuinely exercised.
public sealed class SessionConcurrencySqlServerTests : IDisposable
{
    internal static readonly string MasterConnectionString =
        Environment.GetEnvironmentVariable("CINEMAGO_TEST_SQL")
        ?? "Server=(localdb)\\MSSQLLocalDB;Database=master;Integrated Security=true;TrustServerCertificate=true;Connect Timeout=5";

    private static readonly DateTime Start = new(2030, 1, 1, 18, 0, 0, DateTimeKind.Utc);

    private readonly string _databaseName = "CinemaGoTests_" + Guid.NewGuid().ToString("N");
    private readonly IMapper _mapper = new MapperConfiguration(
        c => c.AddProfile<SessionProfile>(),
        NullLoggerFactory.Instance).CreateMapper();

    private string DatabaseConnectionString =>
        new SqlConnectionStringBuilder(MasterConnectionString) { InitialCatalog = _databaseName }.ConnectionString;

    private CinemaDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<CinemaDbContext>().UseSqlServer(DatabaseConnectionString).Options);

    private SessionService CreateService(CinemaDbContext context) => new(
        new SessionRepository(context),
        new MovieRepository(context),
        new HallRepository(context),
        new SeatRepository(context),
        new CinemaGo.Infrastructure.UnitOfWork.UnitOfWork(context),
        _mapper);

    private void Seed()
    {
        using var context = CreateContext();
        context.Database.EnsureCreated();
        context.Movies.Add(new Movie { Title = "Dune", DurationMinutes = 120, AgeRating = 12 });
        context.Halls.Add(new Hall { Name = "Hall A", Capacity = 10 });
        context.Halls.Add(new Hall { Name = "Hall B", Capacity = 10 });
        context.SaveChanges();
    }

    [SqlServerFact]
    public async Task AddSession_ParallelOverlappingRequests_OnlyOneIsCreated()
    {
        Seed();

        const int parallelRequests = 8;
        var startGate = new TaskCompletionSource();

        var tasks = Enumerable.Range(0, parallelRequests).Select(i => Task.Run(async () =>
        {
            await using var context = CreateContext();
            var service = CreateService(context);
            var dto = new CreateSessionDTO
            {
                MovieId = 1,
                HallId = 1,
                // Every request overlaps every other one.
                StartsAt = Start.AddMinutes(i * 5),
                StandardPrice = 10,
                VipPrice = 20
            };

            await startGate.Task;

            try
            {
                await service.AddSessionAsync(dto, CancellationToken.None);
                return true;
            }
            catch (ConflictException)
            {
                // Overlap detected, or this request was the deadlock victim of the Serializable transaction.
                return false;
            }
        })).ToList();

        startGate.SetResult();
        var results = await Task.WhenAll(tasks);

        Assert.Equal(1, results.Count(created => created));

        await using var verify = CreateContext();
        Assert.Equal(1, await verify.Sessions.CountAsync(s => s.HallId == 1));
    }

    [SqlServerFact]
    public async Task AddSession_ParallelRequestsInDifferentHalls_BothSucceed()
    {
        Seed();

        var startGate = new TaskCompletionSource();

        var tasks = new[] { 1, 2 }.Select(hallId => Task.Run(async () =>
        {
            await using var context = CreateContext();
            var service = CreateService(context);
            await startGate.Task;

            return await service.AddSessionAsync(new CreateSessionDTO
            {
                MovieId = 1,
                HallId = hallId,
                StartsAt = Start,
                StandardPrice = 10,
                VipPrice = 20
            }, CancellationToken.None);
        })).ToList();

        startGate.SetResult();
        await Task.WhenAll(tasks);

        await using var verify = CreateContext();
        Assert.Equal(2, await verify.Sessions.CountAsync());
    }

    [SqlServerFact]
    public async Task BookTicket_ParallelRequestsForSameSeat_OnlyOneSucceeds()
    {
        using (var context = CreateContext())
        {
            context.Database.EnsureCreated();
            context.Movies.Add(new Movie { Title = "Dune", DurationMinutes = 120, AgeRating = 12 });
            context.Halls.Add(new Hall { Name = "Hall A", Capacity = 10 });
            context.SaveChanges();
            context.Seats.Add(new Seat { HallId = 1, RowNumber = 1, SeatNumber = 1 });
            const int users = 8;
            for (var i = 1; i <= users; i++)
            {
                context.Users.Add(new User { Username = "user" + i, Email = $"user{i}@x.com", PasswordHash = "h" });
            }

            context.Sessions.Add(new Session
            {
                MovieId = 1, HallId = 1, StartsAt = DateTime.UtcNow.AddDays(1), StandardPrice = 10, VipPrice = 20
            });
            context.SaveChanges();
        }

        var bookingMapper = new MapperConfiguration(
            c => c.AddProfile<BookingProfile>(), NullLoggerFactory.Instance).CreateMapper();
        var startGate = new TaskCompletionSource();

        var tasks = Enumerable.Range(1, 8).Select(userId => Task.Run(async () =>
        {
            await using var context = CreateContext();
            var service = new BookingService(
                new BookingRepository(context),
                new SessionRepository(context),
                new SeatRepository(context),
                new CinemaGo.Infrastructure.UnitOfWork.UnitOfWork(context),
                bookingMapper);

            await startGate.Task;

            try
            {
                await service.BookTicketAsync(new CreateBookingDTO { SessionId = 1, SeatId = 1 }, userId, CancellationToken.None);
                return true;
            }
            catch (ConflictException)
            {
                return false;
            }
        })).ToList();

        startGate.SetResult();
        var results = await Task.WhenAll(tasks);

        Assert.Equal(1, results.Count(booked => booked));

        await using var verify = CreateContext();
        Assert.Equal(1, await verify.Bookings.CountAsync(b => b.Status == BookingStatus.Active));
        Assert.Equal(1, await verify.Tickets.CountAsync());
    }

    public void Dispose()
    {
        SqlConnection.ClearAllPools();
        using var connection = new SqlConnection(MasterConnectionString);
        try
        {
            connection.Open();
        }
        catch
        {
            return;
        }

        using var command = connection.CreateCommand();
        command.CommandText =
            $"IF DB_ID('{_databaseName}') IS NOT NULL BEGIN " +
            $"ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
            $"DROP DATABASE [{_databaseName}]; END";
        command.ExecuteNonQuery();
    }
}
