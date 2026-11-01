using AutoMapper;
using CinemaGo.Application.Profiles;
using CinemaGo.Infrastructure.Data;
using CinemaGo.Infrastructure.UnitOfWork;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CinemaGo.Tests;

// Real EF Core context on in-memory SQLite so repositories, constraints and cascades behave like production.
public sealed class TestDb : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public CinemaDbContext Context { get; }
    public UnitOfWork UnitOfWork { get; }
    public IMapper Mapper { get; }

    public TestDb()
    {
        _connection.Open();
        Context = new CinemaDbContext(new DbContextOptionsBuilder<CinemaDbContext>().UseSqlite(_connection).Options);
        Context.Database.EnsureCreated();
        UnitOfWork = new UnitOfWork(Context);
        Mapper = new MapperConfiguration(c =>
        {
            c.AddProfile<CatalogProfile>();
            c.AddProfile<MovieMapper>();
            c.AddProfile<SessionProfile>();
        }, NullLoggerFactory.Instance).CreateMapper();
    }

    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
    }
}
