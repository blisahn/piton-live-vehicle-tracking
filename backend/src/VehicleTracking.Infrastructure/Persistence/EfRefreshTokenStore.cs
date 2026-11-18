using Microsoft.EntityFrameworkCore;
using VehicleTracking.Application.Abstractions;
using VehicleTracking.Application.Auth;

namespace VehicleTracking.Infrastructure.Persistence;

internal sealed class EfRefreshTokenStore(TrackingDbContext db) : IRefreshTokenStore
{
    public async Task AddAsync(RefreshToken token, CancellationToken cancellationToken)
    {
        db.RefreshTokens.Add(new RefreshTokenRow
        {
            Id = token.Id,
            UserId = token.UserId,
            FamilyId = token.FamilyId,
            TokenHash = token.TokenHash,
            CreatedAt = token.CreatedAt.ToUniversalTime(),
            ExpiresAt = token.ExpiresAt.ToUniversalTime(),
            RevokedAt = token.RevokedAt?.ToUniversalTime(),
            ReplacedById = token.ReplacedById,
        });
        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();
    }

    public async Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken)
    {
        var row = await db.RefreshTokens.AsNoTracking()
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);
        return row is null ? null : new RefreshToken
        {
            Id = row.Id,
            UserId = row.UserId,
            FamilyId = row.FamilyId,
            TokenHash = row.TokenHash,
            CreatedAt = row.CreatedAt,
            ExpiresAt = row.ExpiresAt,
            RevokedAt = row.RevokedAt,
            ReplacedById = row.ReplacedById,
        };
    }

    public async Task<bool> TryRevokeAsync(
        Guid id, Guid? replacedById, DateTimeOffset at, CancellationToken cancellationToken)
    {
        var atUtc = at.ToUniversalTime();
        var changed = await db.RefreshTokens
            .Where(t => t.Id == id && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.RevokedAt, atUtc)
                .SetProperty(t => t.ReplacedById, replacedById), cancellationToken);
        return changed > 0;
    }

    public async Task RevokeFamilyAsync(Guid familyId, DateTimeOffset at, CancellationToken cancellationToken)
    {
        var atUtc = at.ToUniversalTime();
        await db.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, atUtc), cancellationToken);
    }

    public async Task DeleteExpiredAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var nowUtc = now.ToUniversalTime();
        await db.RefreshTokens
            .Where(t => t.UserId == userId && t.ExpiresAt < nowUtc)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
