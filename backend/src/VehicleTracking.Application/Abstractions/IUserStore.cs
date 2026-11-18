using VehicleTracking.Application.Auth;

namespace VehicleTracking.Application.Abstractions;

public interface IUserStore
{
    /// <param name="email">Already lower-cased.</param>
    Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken);

    Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>False when the email is already taken.</summary>
    Task<bool> TryAddAsync(User user, CancellationToken cancellationToken);
}
