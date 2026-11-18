using VehicleTracking.Application.Auth;

namespace VehicleTracking.Application.Abstractions;

public interface IAccessTokenIssuer
{
    AccessToken Issue(User user);
}
