using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VehicleTracking.Application.Abstractions;
using VehicleTracking.Domain.Vehicles;

namespace VehicleTracking.Infrastructure.Spool;

/// <summary>
/// Durable queue on disk, one batch per file. One writer (the polling worker) and one reader
/// (the writer worker); they never touch the same file because a file becomes visible only
/// through the atomic rename from .tmp to .batch.
/// </summary>
internal sealed class FilePositionSpool : IPositionSpool
{
    private readonly string _directory;
    private readonly ILogger<FilePositionSpool> _logger;
    private long _fileCounter;
    private long _pending;

    public FilePositionSpool(IOptions<SpoolOptions> options, ILogger<FilePositionSpool> logger)
    {
        _logger = logger;
        _directory = options.Value.Directory;
        Directory.CreateDirectory(_directory);

        // A .tmp file is a write that was interrupted; its batch was never acknowledged, so it is safe to drop.
        foreach (var leftover in Directory.EnumerateFiles(_directory, "*.tmp"))
            File.Delete(leftover);

        _pending = Directory.EnumerateFiles(_directory, "*.batch").Count();
    }

    public int PendingBatches => (int)Interlocked.Read(ref _pending);

    public async Task AppendAsync(IReadOnlyCollection<VehiclePosition> batch, CancellationToken cancellationToken)
    {
        // Ticks first, then the counter: ordinal file name order equals write order.
        var name = $"{DateTime.UtcNow.Ticks:D19}-{Interlocked.Increment(ref _fileCounter):D6}";
        var tempPath = Path.Combine(_directory, name + ".tmp");
        var finalPath = Path.Combine(_directory, name + ".batch");

        var stream = new FileStream(
            tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize: 64 * 1024, useAsync: true);
        try
        {
            await JsonSerializer.SerializeAsync(stream, batch, cancellationToken: cancellationToken);
            await stream.FlushAsync(cancellationToken);
            // Force the data to the disk itself so it survives a crash or power loss.
            stream.Flush(flushToDisk: true);
        }
        finally
        {
            await stream.DisposeAsync();
        }

        File.Move(tempPath, finalPath);
        Interlocked.Increment(ref _pending);
    }

    public async Task<SpooledBatch?> PeekAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            var path = Directory.EnumerateFiles(_directory, "*.batch").OrderBy(p => p, StringComparer.Ordinal).FirstOrDefault();
            if (path is null) return null;

            try
            {
                await using var stream = new FileStream(
                    path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 64 * 1024, useAsync: true);
                var positions = await JsonSerializer.DeserializeAsync<List<VehiclePosition>>(
                    stream, cancellationToken: cancellationToken);
                if (positions is null) throw new JsonException("The batch file contains null.");

                return new SpooledBatch(path, positions);
            }
            catch (JsonException ex)
            {
                // The atomic rename should make this impossible, but a disk can corrupt a file.
                // Keep it for inspection, but move it out of the queue so it does not block newer batches.
                _logger.LogError(ex, "Spool batch {Path} is unreadable and is set aside as .corrupt", path);
                File.Move(path, Path.ChangeExtension(path, ".corrupt"), overwrite: true);
                Interlocked.Decrement(ref _pending);
            }
        }
    }

    public Task CompleteAsync(SpooledBatch batch, CancellationToken cancellationToken)
    {
        File.Delete(batch.Id);
        Interlocked.Decrement(ref _pending);
        return Task.CompletedTask;
    }
}
