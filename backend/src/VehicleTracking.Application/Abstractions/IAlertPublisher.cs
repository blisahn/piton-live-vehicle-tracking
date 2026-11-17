namespace VehicleTracking.Application.Abstractions;

/// <summary>Told when the set of alerts changed. Must return quickly and never block on a client.</summary>
public interface IAlertPublisher
{
    /// <param name="version">Grows by one on every change; clients compare it to know whether they are up to date.</param>
    void PublishAlertsVersion(long version);
}

/// <summary>Lets a late joiner learn the current version. Kept apart from the publisher so the WebSocket hub need not know the service.</summary>
public interface IAlertVersionReader
{
    long Version { get; }
}
