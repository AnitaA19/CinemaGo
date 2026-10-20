using CinemaGo.Domain.Intefraces;
using CinemaGo.Infrastructure.Data;

namespace CinemaGo.Infrastructure.UnitOfWork;

public class UnitOfWork : IUnitOfWork
{
    private readonly CinemaDbContext _context;

    public UnitOfWork(CinemaDbContext context)
    {
        _context = context;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }
}
