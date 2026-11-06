
using Microsoft.EntityFrameworkCore;
using VehicleTracker.Domain.Entities;

namespace VehicleTracker.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<VehiclePositionRecord> VehiclePositions => Set<VehiclePositionRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<VehiclePositionRecord>(builder =>
        {
            // TimescaleDB hypertable kullanımı için zaman alanı
            // composite primary key'in bir parçası olmalıdır.
            builder.HasKey(x => new { x.Id, x.Timestamp });

            builder.Property(x => x.Id)
                .ValueGeneratedOnAdd();

            builder.Property(x => x.VehicleId)
                .HasMaxLength(128)
                .IsRequired();

            builder.Property(x => x.VehicleLabel)
                .HasMaxLength(128);

            builder.Property(x => x.TripId)
                .HasMaxLength(128);

            builder.Property(x => x.RouteId)
                .HasMaxLength(128);

            builder.Property(x => x.DirectionId)
                .HasConversion<long>()
                .HasColumnType("bigint");

            builder.Property(x => x.StopId)
                .HasMaxLength(128);

            builder.Property(x => x.CurrentStopSequence)
                .HasConversion<long>()
                .HasColumnType("bigint");

            // Enum değerlerini veritabanında isimleriyle saklıyoruz.
            builder.Property(x => x.CurrentStatus)
                .HasConversion<string>()
                .HasMaxLength(64);

            builder.Property(x => x.OccupancyStatus)
                .HasConversion<string>()
                .HasMaxLength(64);

            // Araç geçmişi sorguları için indeks.
            builder.HasIndex(x => new { x.VehicleId, x.Timestamp });

            // Belirli zaman aralıklarındaki kayıtları hızlı bulmak için.
            builder.HasIndex(x => x.Timestamp);
        });
    }
}
