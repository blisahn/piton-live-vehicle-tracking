namespace VehicleTracking.Domain.Vehicles;

public static class VehicleKey
{
    /// <summary>
    /// Builds the identity of a vehicle across the whole feed. The feed merges many operators and
    /// their vehicle ids collide, so the operator's codespace (e.g. "RUT", "ATB") is part of the key.
    /// </summary>
    /// Some operators already put their codespace in the vehicle id ("MOR:Vehicle:238_3520");
    /// prefixing it again would give "MOR:MOR:Vehicle:238_3520".
    public static string Create(string codespace, string vehicleId)
    {
        if (codespace.Length == 0) return vehicleId;
        return vehicleId.StartsWith(codespace + ":", StringComparison.Ordinal) ? vehicleId : $"{codespace}:{vehicleId}";
    }
}
