using System.Net;
using Microsoft.Extensions.Options;
using VehicleTracking.Application.Abstractions;
using VehicleTracking.Application.Configuration;
using VehicleTracking.Infrastructure.Entur;

namespace VehicleTracking.UnitTests;

/// <summary>
/// A connection that sends its headers and then goes silent mid-body (network drop, laptop sleep)
/// must end in a failed read the poll loop can retry, not in a read that never returns.
/// </summary>
public sealed class FeedReadDeadlineTests
{
    private static readonly TimeSpan ShortTimeout = TimeSpan.FromMilliseconds(300);

    [Fact]
    public async Task A_body_that_stalls_ends_in_a_feed_read_exception_within_the_timeout()
    {
        var http = new HttpClient(new StallingBodyHandler()) { Timeout = ShortTimeout };
        var source = new EnturFeedSource(http, Options.Create(new FeedOptions
        {
            VehiclePositionsUrl = "https://example.test/vehicle-positions",
            PollInterval = TimeSpan.FromSeconds(3),
            ClientName = "test",
        }));

        var read = source.ReadAsync(CancellationToken.None);
        var finished = await Task.WhenAny(read, Task.Delay(TimeSpan.FromSeconds(5)));

        Assert.Same(read, finished);
        var error = await Assert.ThrowsAsync<FeedReadException>(() => read);
        Assert.Contains("timed out", error.Message);
    }

    [Fact]
    public async Task Shutting_down_still_cancels_instead_of_reporting_a_timeout()
    {
        var http = new HttpClient(new StallingBodyHandler()) { Timeout = TimeSpan.FromSeconds(30) };
        var source = new EnturFeedSource(http, Options.Create(new FeedOptions
        {
            VehiclePositionsUrl = "https://example.test/vehicle-positions",
            PollInterval = TimeSpan.FromSeconds(3),
            ClientName = "test",
        }));
        using var stopping = new CancellationTokenSource(ShortTimeout);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => source.ReadAsync(stopping.Token));
    }

    /// <summary>Answers 200 with headers at once, then a body that never delivers a byte.</summary>
    private sealed class StallingBodyHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new StallingStream()) });
    }

    private sealed class StallingStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
