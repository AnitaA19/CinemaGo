namespace CinemaGo.Domain.Intefraces;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    // Runs the action in a Serializable transaction so check-then-write logic cannot interleave with other requests.
    Task<T> ExecuteInSerializableTransactionAsync<T>(
        Func<Task<T>> action,
        CancellationToken cancellationToken = default);
}
