using Microsoft.EntityFrameworkCore;
using Npgsql;
using VehicleTracking.Application.Abstractions;
using VehicleTracking.Application.Auth;

namespace VehicleTracking.Infrastructure.Persistence;

internal sealed class EfUserStore(TrackingDbContext db) : IUserStore
{
    public async Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken)
    {
        var row = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
        return row is null ? null : ToUser(row);
    }

    public async Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        return row is null ? null : ToUser(row);
    }

    public async Task<bool> TryAddAsync(User user, CancellationToken cancellationToken)
    {
        db.Users.Add(new UserRow
        {
            Id = user.Id,
            Email = user.Email,
            PasswordHash = user.PasswordHash,
            Role = user.Role,
            CreatedAt = user.CreatedAt.ToUniversalTime(),
        });
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            db.ChangeTracker.Clear();
            return false;
        }
    }

    private static User ToUser(UserRow row) => new()
    {
        Id = row.Id,
        Email = row.Email,
        PasswordHash = row.PasswordHash,
        Role = row.Role,
        CreatedAt = row.CreatedAt,
    };
}
