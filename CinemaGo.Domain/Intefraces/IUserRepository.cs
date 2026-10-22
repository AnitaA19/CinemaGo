using CinemaGo.Domain.Entities;

namespace CinemaGo.Domain.Intefraces;

public interface IUserRepository
{
    Task<ICollection<User>> GetAllUsersAsync(CancellationToken cancellationToken);
    Task<User?> GetUserByIdAsync(int id, CancellationToken cancellationToken);
    Task AddUserAsync(User user, CancellationToken cancellationToken);
    void UpdateUserAsync(User user);
    void DeleteUserAsync(User user);
}
