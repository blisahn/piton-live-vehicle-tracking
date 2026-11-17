using VehicleTracking.Infrastructure.Persistence;

namespace VehicleTracking.UnitTests;

public class RetentionScheduleTests
{
    [Theory]
    [InlineData(180, 15)]  // 3 h -> 15 min
    [InlineData(5, 1)]     // 5 min / 12 is below the floor
    public void Chunk_interval_is_retention_over_twelve_with_a_one_minute_floor(int retentionMinutes, int expectedMinutes) =>
        Assert.Equal(
            TimeSpan.FromMinutes(expectedMinutes),
            RetentionSchedule.ChunkInterval(TimeSpan.FromMinutes(retentionMinutes)));

    [Fact]
    public void Sweep_interval_is_half_the_chunk_interval()
    {
        // 1 h -> chunk 5 min -> sweep 2.5 min.
        Assert.Equal(TimeSpan.FromSeconds(150), RetentionSchedule.SweepInterval(TimeSpan.FromHours(1)));
    }

    [Fact]
    public void Sweep_interval_for_a_very_short_retention_is_half_the_one_minute_chunk_floor()
    {
        // The chunk floor of 1 min makes 30 s the smallest sweep interval.
        Assert.Equal(TimeSpan.FromSeconds(30), RetentionSchedule.SweepInterval(TimeSpan.FromMinutes(5)));
    }

    [Fact]
    public void Sweep_interval_is_at_most_five_minutes() =>
        Assert.Equal(TimeSpan.FromMinutes(5), RetentionSchedule.SweepInterval(TimeSpan.FromHours(3)));
}
