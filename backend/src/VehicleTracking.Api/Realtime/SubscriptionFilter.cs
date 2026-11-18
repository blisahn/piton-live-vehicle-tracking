using VehicleTracking.Domain.Vehicles;

namespace VehicleTracking.Api.Realtime;

/// <summary>
/// Narrows what one client is sent to its subscription. It remembers which vehicles the client
/// currently has, so a vehicle that leaves the subscribed area is reported as removed and a
/// removal of a vehicle the client never had is not sent. It never touches sequence numbers:
/// callers must still send a delta whose filtered content is empty.
/// One instance per client, used only from that client's send loop.
/// </summary>
internal sealed class SubscriptionFilter
{
    private readonly HashSet<string> _sent = new();

    public int SentCount => _sent.Count;

    public List<VehiclePosition> FilterSnapshot(IReadOnlyList<VehiclePosition> vehicles, VehicleSubscription subscription)
    {
        _sent.Clear();
        var result = new List<VehiclePosition>();
        foreach (var vehicle in vehicles)
        {
            if (!subscription.Matches(vehicle)) continue;
            result.Add(vehicle);
            _sent.Add(vehicle.VehicleKey);
        }
        return result;
    }

    public (List<VehiclePosition> Upserted, List<string> Removed) FilterDelta(
        IReadOnlyList<VehiclePosition> upserted, IReadOnlyList<string> removed, VehicleSubscription subscription)
    {
        var upserts = new List<VehiclePosition>();
        var removals = new List<string>();

        foreach (var vehicle in upserted)
        {
            if (subscription.Matches(vehicle))
            {
                upserts.Add(vehicle);
                _sent.Add(vehicle.VehicleKey);
            }
            else if (_sent.Remove(vehicle.VehicleKey))
            {
                removals.Add(vehicle.VehicleKey);   // it left the subscribed area
            }
        }
        foreach (var key in removed)
        {
            if (_sent.Remove(key)) removals.Add(key);
        }
        return (upserts, removals);
    }
}
