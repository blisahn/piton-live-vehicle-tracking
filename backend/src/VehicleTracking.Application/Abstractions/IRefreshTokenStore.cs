using VehicleTracking.Application.Auth;

namespace VehicleTracking.Application.Abstractions;

public interface IRefreshTokenStore
{
    Task AddAsync(RefreshToken token, CancellationToken cancellationToken);

    Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken);

    /// <summary>Revokes a token that is still active. False when it was already revoked (or is gone).</summary>
    Task<bool> TryRevokeAsync(Guid id, Guid? replacedById, DateTimeOffset at, CancellationToken cancellationToken);

    Task RevokeFamilyAsync(Guid familyId, DateTimeOffset at, CancellationToken cancellationToken);

    Task DeleteExpiredAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken);
}
