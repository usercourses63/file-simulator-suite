using Microsoft.EntityFrameworkCore;
using FileSimulator.ControlApi.Data.Entities;

namespace FileSimulator.ControlApi.Data;

/// <summary>
/// Entity Framework Core DbContext for metrics persistence.
/// Uses SQLite for embedded database storage.
/// </summary>
public class MetricsDbContext : DbContext
{
    public MetricsDbContext(DbContextOptions<MetricsDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Raw health check samples at 5-second intervals.
    /// </summary>
    public DbSet<HealthSample> HealthSamples => Set<HealthSample>();

    /// <summary>
    /// Hourly aggregated health metrics rollups.
    /// </summary>
    public DbSet<HealthHourly> HealthHourly => Set<HealthHourly>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // HealthSample entity configuration
        modelBuilder.Entity<HealthSample>(entity =>
        {
            entity.ToTable("health_samples");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Timestamp)
                .IsRequired()
                .HasColumnName("timestamp");

            entity.Property(e => e.ServerId)
                .IsRequired()
                .HasMaxLength(50)
                .HasColumnName("server_id");

            entity.Property(e => e.ServerType)
                .IsRequired()
                .HasMaxLength(10)
                .HasColumnName("server_type");

            entity.Property(e => e.IsHealthy)
                .IsRequired()
                .HasColumnName("is_healthy");

            entity.Property(e => e.LatencyMs)
                .HasColumnName("latency_ms");

            // Composite index for time-range queries per server (most common query pattern)
            entity.HasIndex(e => new { e.ServerId, e.Timestamp })
                .HasDatabaseName("ix_health_samples_server_timestamp");

            // Simple index for retention cleanup queries (delete by timestamp)
            entity.HasIndex(e => e.Timestamp)
                .HasDatabaseName("ix_health_samples_timestamp");
        });

        // HealthHourly entity configuration
        modelBuilder.Entity<HealthHourly>(entity =>
        {
            entity.ToTable("health_hourly");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.HourStart)
                .IsRequired()
                .HasColumnName("hour_start");

            entity.Property(e => e.ServerId)
                .IsRequired()
                .HasMaxLength(50)
                .HasColumnName("server_id");

            entity.Property(e => e.ServerType)
                .IsRequired()
                .HasMaxLength(10)
                .HasColumnName("server_type");

            entity.Property(e => e.SampleCount)
                .IsRequired()
                .HasColumnName("sample_count");

            entity.Property(e => e.HealthyCount)
                .IsRequired()
                .HasColumnName("healthy_count");

            entity.Property(e => e.AvgLatencyMs)
                .HasColumnName("avg_latency_ms");

            entity.Property(e => e.MinLatencyMs)
                .HasColumnName("min_latency_ms");

            entity.Property(e => e.MaxLatencyMs)
                .HasColumnName("max_latency_ms");

            entity.Property(e => e.P95LatencyMs)
                .HasColumnName("p95_latency_ms");

            // Composite index for time-range queries per server
            entity.HasIndex(e => new { e.ServerId, e.HourStart })
                .HasDatabaseName("ix_health_hourly_server_hour");
        });
    }
}
