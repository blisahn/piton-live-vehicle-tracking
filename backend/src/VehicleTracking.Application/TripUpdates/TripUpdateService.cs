namespace VehicleTracking.Application.TripUpdates;

/// <summary>
/// The latest trip updates, by trip id.
///
/// One writer (the polling worker) calls <see cref="Apply"/>; request threads read. Each read
/// of the feed replaces the whole dictionary in one step, so readers take no lock and never see
/// a half-applied read. Trips that left the feed disappear with the swap.
/// </summary>
public sealed class TripUpdateService(TimeProvider time)
{
    private IReadOnlyDictionary<string, TripProgressRecord> _trips = new Dictionary<string, TripProgressRecord>();

    // Only the writer touches the status.
    private TripUpdateStatus _status = new(null, null, 0, 0, 0, 0);

    public TripUpdateStatus Status => Volatile.Read(ref _status);

    public TripProgressRecord? TryGet(string tripId) => Volatile.Read(ref _trips).GetValueOrDefault(tripId);

    public void Apply(TripUpdateSnapshot snapshot)
    {
        var trips = new Dictionary<string, TripProgressRecord>(snapshot.Trips.Count, StringComparer.Ordinal);
        // Last one wins when the feed repeats a trip.
        foreach (var trip in snapshot.Trips) trips[trip.TripId] = trip;
        Volatile.Write(ref _trips, trips);

        var current = _status;
        Volatile.Write(ref _status, current with
        {
            LastSuccessfulReadAt = time.GetUtcNow(),
            LastError = null,
            SuccessfulReads = current.SuccessfulReads + 1,
            TripCount = trips.Count,
            DroppedDelays = current.DroppedDelays + snapshot.DroppedDelays,
        });
    }

    public void RecordFailure(string error)
    {
        var current = _status;
        Volatile.Write(ref _status, current with { LastError = error, FailedReads = current.FailedReads + 1 });
    }
}
