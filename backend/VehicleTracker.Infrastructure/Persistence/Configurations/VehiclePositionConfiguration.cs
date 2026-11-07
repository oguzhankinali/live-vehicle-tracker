using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VehicleTracker.Domain.Entities;

namespace VehicleTracker.Infrastructure.Persistence.Configurations;

public sealed class VehiclePositionConfiguration : IEntityTypeConfiguration<VehiclePositionRecord>
{
    public void Configure(EntityTypeBuilder<VehiclePositionRecord> builder)
    {
        builder.ToTable("vehicle_positions");

        // TimescaleDB Hypertable Kuralı: Partition kolonu (observed_at) Primary Key içinde yer almak zorundadır.
        // Aynı zamanda (feed_entity_id, observed_at) çifti doğal idempotency anahtarımızdır.
        builder.HasKey(x => new { x.FeedEntityId, x.Timestamp })
            .HasName("pk_vehicle_positions");

        builder.Property(x => x.FeedEntityId)
            .HasColumnName("feed_entity_id")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(x => x.Timestamp)
            .HasColumnName("observed_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(x => x.VehicleId)
            .HasColumnName("vehicle_id")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(x => x.VehicleLabel)
            .HasColumnName("vehicle_label")
            .HasColumnType("text");

        builder.Property(x => x.TripId)
            .HasColumnName("trip_id")
            .HasColumnType("text");

        builder.Property(x => x.RouteId)
            .HasColumnName("route_id")
            .HasColumnType("text");

        builder.Property(x => x.DirectionId)
            .HasColumnName("direction_id")
            .HasColumnType("bigint");

        builder.Property(x => x.Latitude)
            .HasColumnName("latitude")
            .HasColumnType("double precision")
            .IsRequired();

        builder.Property(x => x.Longitude)
            .HasColumnName("longitude")
            .HasColumnType("double precision")
            .IsRequired();

        builder.Property(x => x.Bearing)
            .HasColumnName("bearing")
            .HasColumnType("real");

        builder.Property(x => x.Speed)
            .HasColumnName("speed")
            .HasColumnType("real");

        builder.Property(x => x.StopId)
            .HasColumnName("stop_id")
            .HasColumnType("text");

        builder.Property(x => x.CurrentStopSequence)
            .HasColumnName("current_stop_sequence")
            .HasConversion<long>()
            .HasColumnType("bigint");

        builder.Property(x => x.CurrentStatus)
            .HasColumnName("current_status")
            .HasConversion<short>()
            .HasColumnType("smallint");

        builder.Property(x => x.OccupancyStatus)
            .HasColumnName("occupancy_status")
            .HasConversion<short>()
            .HasColumnType("smallint");

        builder.Property(x => x.ReceivedAtUtc)
            .HasColumnName("received_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        // İndeks 1: Araç geçmişi sorgusu performansı için: WHERE vehicle_id = ? ORDER BY observed_at DESC
        builder.HasIndex(x => new { x.VehicleId, x.Timestamp })
            .IsDescending(false, true)
            .HasDatabaseName("ix_vehicle_positions_vehicle_id_observed_at");

        // İndeks 2: Zaman aralığı ve retention sorguları için
        builder.HasIndex(x => x.Timestamp)
            .HasDatabaseName("ix_vehicle_positions_observed_at");
    }
}