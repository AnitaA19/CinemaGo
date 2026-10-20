using CinemaGo.Domain.Entities;
using CinemaGo.Domain.Intefraces;
using CinemaGo.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CinemaGo.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly CinemaDbContext _context;
    public UserRepository(CinemaDbContext context)
    {
        _context = context;
    }

    public async Task<ICollection<User>> GetAllUsersAsync(CancellationToken cancellationToken)
    {
        return await _context.Users.ToListAsync(cancellationToken);
    }

    public async Task<ICollection<User>> GetUserByIdAsync(int id, CancellationToken cancellationToken)
    {
        return await _context.Users.Where(u => u.Id == id).ToListAsync(cancellationToken);
    }

    public async Task AddUserAsync(User user, CancellationToken cancellationToken)
    {
        await _context.Users.AddAsync(user, cancellationToken);
    }

    public void UpdateUserAsync(User user)
    {
        _context.Users.Update(user);
    }

    public void DeleteUserAsync(User user)
    {
        _context.Users.Remove(user);
    }
}
