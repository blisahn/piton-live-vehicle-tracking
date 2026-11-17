using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace VehicleTracking.Infrastructure.Persistence.Migrations;

/// <summary>
/// Hand-written on purpose (no designer file, no model snapshot): the schema needs TimescaleDB
/// calls that EF Core cannot generate.
/// </summary>
[DbContext(typeof(TrackingDbContext))]
[Migration("20261116000000_Initial")]
public sealed class Initial : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("create extension if not exists timescaledb;");

        // The primary key does two jobs: it makes writing the same position twice a no-op (ON CONFLICT),
        // and its index (vehicle first, then time) is exactly what the history query needs.
        // A unique index on a hypertable must include the time column, which this one does.
        migrationBuilder.Sql("""
            create table vehicle_positions (
                vehicle_key text not null,
                recorded_at timestamptz not null,
                latitude double precision not null,
                longitude double precision not null,
                bearing real,
                speed real,
                route_id text,
                trip_id text,
                stop_id text,
                stop_status smallint,
                occupancy smallint,
                primary key (vehicle_key, recorded_at)
            );
            """);

        // The real interval is set at startup from the configured retention (see DatabaseInitializer);
        // 15 minutes fits the default 3 hours.
        migrationBuilder.Sql("""
            select create_hypertable('vehicle_positions', 'recorded_at', chunk_time_interval => interval '15 minutes');
            """);

        // Per-minute write counter so "rows written in the last hour" is a 60-row sum,
        // not a count over a million rows, and survives restarts.
        migrationBuilder.Sql("""
            create table ingest_stats (
                minute timestamptz primary key,
                rows_written bigint not null
            );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("drop table if exists ingest_stats;");
        migrationBuilder.Sql("drop table if exists vehicle_positions;");
    }
}
