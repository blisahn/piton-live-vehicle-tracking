namespace VehicleTracking.Infrastructure.Persistence;

/// <summary>
/// The app must start and keep collecting even when the database is down, so nothing blocks
/// startup on it; code that needs the database waits on this instead.
/// </summary>
internal sealed class DatabaseReadiness
{
    private readonly TaskCompletionSource _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public bool IsReady => _tcs.Task.IsCompletedSuccessfully;

    public void MarkReady() => _tcs.TrySetResult();

    public Task WaitAsync(CancellationToken cancellationToken) => _tcs.Task.WaitAsync(cancellationToken);
}
