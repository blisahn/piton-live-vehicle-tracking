using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VehicleTracking.Domain.Vehicles;
using VehicleTracking.Infrastructure.Spool;
using static VehicleTracking.UnitTests.TestData;

namespace VehicleTracking.UnitTests;

public sealed class FilePositionSpoolTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "spool-tests-" + Guid.NewGuid().ToString("N"));
    private readonly FilePositionSpool _spool;

    public FilePositionSpoolTests() => _spool = NewSpool();

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    private FilePositionSpool NewSpool() =>
        new(Options.Create(new SpoolOptions { Directory = _directory }), NullLogger<FilePositionSpool>.Instance);

    private static VehiclePosition[] Batch(string key) => new[] { Position(key), Position(key, at: Now.AddSeconds(1)) };

    [Fact]
    public async Task Peek_returns_the_positions_that_were_appended()
    {
        var batch = Batch("A:1");
        await _spool.AppendAsync(batch, CancellationToken.None);

        var peeked = await _spool.PeekAsync(CancellationToken.None);

        Assert.NotNull(peeked);
        Assert.Equal(batch, peeked.Positions);
    }

    [Fact]
    public async Task Complete_removes_the_batch_so_peek_returns_null()
    {
        await _spool.AppendAsync(Batch("A:1"), CancellationToken.None);
        var peeked = await _spool.PeekAsync(CancellationToken.None);

        await _spool.CompleteAsync(peeked!, CancellationToken.None);

        Assert.Null(await _spool.PeekAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Batches_come_back_oldest_first()
    {
        await _spool.AppendAsync(Batch("A:1"), CancellationToken.None);
        await _spool.AppendAsync(Batch("A:2"), CancellationToken.None);

        var first = await _spool.PeekAsync(CancellationToken.None);
        await _spool.CompleteAsync(first!, CancellationToken.None);
        var second = await _spool.PeekAsync(CancellationToken.None);

        Assert.Equal("A:1", first!.Positions[0].VehicleKey);
        Assert.Equal("A:2", second!.Positions[0].VehicleKey);
    }

    [Fact]
    public async Task Pending_batches_follows_append_and_complete()
    {
        Assert.Equal(0, _spool.PendingBatches);

        await _spool.AppendAsync(Batch("A:1"), CancellationToken.None);
        await _spool.AppendAsync(Batch("A:2"), CancellationToken.None);
        Assert.Equal(2, _spool.PendingBatches);

        await _spool.CompleteAsync((await _spool.PeekAsync(CancellationToken.None))!, CancellationToken.None);
        Assert.Equal(1, _spool.PendingBatches);
    }

    [Fact]
    public async Task A_new_spool_over_the_same_directory_sees_batches_left_by_the_previous_one()
    {
        await _spool.AppendAsync(Batch("A:1"), CancellationToken.None);

        var restarted = NewSpool();

        Assert.Equal(1, restarted.PendingBatches);
        var peeked = await restarted.PeekAsync(CancellationToken.None);
        Assert.Equal("A:1", peeked!.Positions[0].VehicleKey);
    }

    [Fact]
    public void A_new_spool_deletes_leftover_tmp_files()
    {
        var leftover = Path.Combine(_directory, "interrupted.tmp");
        File.WriteAllText(leftover, "half a batch");

        NewSpool();

        Assert.False(File.Exists(leftover));
    }

    [Fact]
    public async Task A_corrupt_batch_is_set_aside_and_the_next_good_batch_is_returned()
    {
        // Sorts before any real batch name, so it is the oldest one.
        var corrupt = Path.Combine(_directory, "0000000000000000000-000000.batch");
        File.WriteAllText(corrupt, "{ not json");
        await _spool.AppendAsync(Batch("A:good"), CancellationToken.None);

        var peeked = await NewSpool().PeekAsync(CancellationToken.None);

        Assert.Equal("A:good", peeked!.Positions[0].VehicleKey);
        Assert.False(File.Exists(corrupt));
        Assert.True(File.Exists(Path.ChangeExtension(corrupt, ".corrupt")));
    }
}
