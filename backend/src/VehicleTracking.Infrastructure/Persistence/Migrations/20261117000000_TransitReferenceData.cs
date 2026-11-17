using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace VehicleTracking.Infrastructure.Persistence.Migrations;

/// <summary>
/// Hand-written like <see cref="Initial"/> (no designer file, no model snapshot).
/// These are reference data, not measurements: retention does not touch them.
/// </summary>
[DbContext(typeof(TrackingDbContext))]
[Migration("20261117000000_TransitReferenceData")]
public sealed class TransitReferenceData : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // fetched_at tells when a name is old enough to be asked for again.
        migrationBuilder.Sql("""
            create table transit_lines (
                id text primary key,
                public_code text,
                name text,
                transport_mode text,
                fetched_at timestamptz not null
            );
            """);

        migrationBuilder.Sql("""
            create table transit_stops (
                id text primary key,
                name text not null,
                latitude double precision not null,
                longitude double precision not null,
                fetched_at timestamptz not null
            );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("drop table if exists transit_stops;");
        migrationBuilder.Sql("drop table if exists transit_lines;");
    }
}
