using System.Data;
using CinemaGo.Domain.Exceptions;
using CinemaGo.Domain.Intefraces;
using CinemaGo.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CinemaGo.Infrastructure.UnitOfWork;

public class UnitOfWork : IUnitOfWork
{
    private readonly CinemaDbContext _context;

    public UnitOfWork(CinemaDbContext context)
    {
        _context = context;
    }

    public async Task<T> ExecuteInSerializableTransactionAsync<T>(
        Func<Task<T>> action,
        CancellationToken cancellationToken = default)
    {
        if (_context.Database.CurrentTransaction is not null)
        {
            return await action();
        }

        var strategy = _context.Database.CreateExecutionStrategy();

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await strategy.ExecuteAsync(async () =>
                {
                    await using var transaction = await _context.Database
                        .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

                    var result = await action();
                    await transaction.CommitAsync(cancellationToken);
                    return result;
                });
            }
            catch (Exception ex) when (IsDeadlock(ex))
            {
                // Serializable range locks make concurrent requests deadlock; SQL Server picks one victim.
                // Retrying lets the victim re-run its check and see what the winner committed.
                // The aborted attempt's tracked changes must be discarded before it is re-run.
                _context.ChangeTracker.Clear();

                if (attempt >= MaxDeadlockAttempts)
                {
                    throw new ConflictException(
                        "The request conflicted with a concurrent operation. Please retry.");
                }

                await Task.Delay(Random.Shared.Next(10, 50) * attempt, cancellationToken);
            }
        }
    }

    private const int MaxDeadlockAttempts = 5;

    private static bool IsDeadlock(Exception? exception)
    {
        for (; exception is not null; exception = exception.InnerException)
        {
            if (exception is SqlException { Number: 1205 })
            {
                return true;
            }
        }

        return false;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (
            ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // Unique index violation, e.g. two concurrent bookings of the same seat.
            throw new ConflictException(
                "The request conflicts with existing data. The seat may have just been booked by someone else.");
        }
    }
}
