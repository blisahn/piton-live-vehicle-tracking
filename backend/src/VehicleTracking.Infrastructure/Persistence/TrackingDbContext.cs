using Microsoft.EntityFrameworkCore;

namespace VehicleTracking.Infrastructure.Persistence;

/// <summary>EF Core context used for read queries and for applying migrations.</summary>
public sealed class TrackingDbContext(DbContextOptions<TrackingDbContext> options) : DbContext(options)
{
    public DbSet<PositionRow> Positions => Set<PositionRow>();

    public DbSet<IngestStatRow> IngestStats => Set<IngestStatRow>();

    public DbSet<TransitLineRow> TransitLines => Set<TransitLineRow>();

    public DbSet<TransitStopRow> TransitStops => Set<TransitStopRow>();

    public DbSet<UserRow> Users => Set<UserRow>();

    public DbSet<RefreshTokenRow> RefreshTokens => Set<RefreshTokenRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PositionRow>(e =>
        {
            e.ToTable("vehicle_positions");
            e.HasKey(p => new { p.VehicleKey, p.RecordedAt });
            e.Property(p => p.VehicleKey).HasColumnName("vehicle_key");
            e.Property(p => p.RecordedAt).HasColumnName("recorded_at");
            e.Property(p => p.Latitude).HasColumnName("latitude");
            e.Property(p => p.Longitude).HasColumnName("longitude");
            e.Property(p => p.Bearing).HasColumnName("bearing");
            e.Property(p => p.Speed).HasColumnName("speed");
            e.Property(p => p.RouteId).HasColumnName("route_id");
            e.Property(p => p.TripId).HasColumnName("trip_id");
            e.Property(p => p.StopId).HasColumnName("stop_id");
            e.Property(p => p.StopStatus).HasColumnName("stop_status");
            e.Property(p => p.Occupancy).HasColumnName("occupancy");
        });

        modelBuilder.Entity<IngestStatRow>(e =>
        {
            e.ToTable("ingest_stats");
            e.HasKey(s => s.Minute);
            e.Property(s => s.Minute).HasColumnName("minute");
            e.Property(s => s.RowsWritten).HasColumnName("rows_written");
        });

        modelBuilder.Entity<TransitLineRow>(e =>
        {
            e.ToTable("transit_lines");
            e.HasKey(l => l.Id);
            e.Property(l => l.Id).HasColumnName("id");
            e.Property(l => l.PublicCode).HasColumnName("public_code");
            e.Property(l => l.Name).HasColumnName("name");
            e.Property(l => l.TransportMode).HasColumnName("transport_mode");
            e.Property(l => l.FetchedAt).HasColumnName("fetched_at");
        });

        modelBuilder.Entity<TransitStopRow>(e =>
        {
            e.ToTable("transit_stops");
            e.HasKey(s => s.Id);
            e.Property(s => s.Id).HasColumnName("id");
            e.Property(s => s.Name).HasColumnName("name");
            e.Property(s => s.Latitude).HasColumnName("latitude");
            e.Property(s => s.Longitude).HasColumnName("longitude");
            e.Property(s => s.FetchedAt).HasColumnName("fetched_at");
        });

        modelBuilder.Entity<UserRow>(e =>
        {
            e.ToTable("users");
            e.HasKey(u => u.Id);
            e.Property(u => u.Id).HasColumnName("id");
            e.Property(u => u.Email).HasColumnName("email");
            e.Property(u => u.PasswordHash).HasColumnName("password_hash");
            e.Property(u => u.Role).HasColumnName("role");
            e.Property(u => u.CreatedAt).HasColumnName("created_at");
        });

        modelBuilder.Entity<RefreshTokenRow>(e =>
        {
            e.ToTable("refresh_tokens");
            e.HasKey(t => t.Id);
            e.Property(t => t.Id).HasColumnName("id");
            e.Property(t => t.UserId).HasColumnName("user_id");
            e.Property(t => t.FamilyId).HasColumnName("family_id");
            e.Property(t => t.TokenHash).HasColumnName("token_hash");
            e.Property(t => t.CreatedAt).HasColumnName("created_at");
            e.Property(t => t.ExpiresAt).HasColumnName("expires_at");
            e.Property(t => t.RevokedAt).HasColumnName("revoked_at");
            e.Property(t => t.ReplacedById).HasColumnName("replaced_by_id");
        });
    }
}
