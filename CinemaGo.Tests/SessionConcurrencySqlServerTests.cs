using AutoMapper;
using CinemaGo.Application.Profiles;
using CinemaGo.Application.Services.Implementations;
using CinemaGo.Domain.DTOs.SessionDTOs;
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
