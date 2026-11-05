using Microsoft.EntityFrameworkCore;
using VehicleTracker.Domain.Entities;

namespace VehicleTracker.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<VehiclePositionRecord> VehiclePositions => Set<VehiclePositionRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<VehiclePositionRecord>(builder =>
        {
            // TimescaleDB hypertable icin zaman damgasi birincil anahtarin parcasi olmalidir
            builder.HasKey(x => new { x.Id, x.Timestamp });

            // Id alaninin otomatik artan (Identity) olmasini sagliyoruz
            builder.Property(x => x.Id)
                   .ValueGeneratedOnAdd();

            builder.Property(x => x.VehicleId)
                   .HasMaxLength(128)
                   .IsRequired();

            builder.Property(x => x.TripId)
                   .HasMaxLength(128);

            builder.Property(x => x.RouteId)
                   .HasMaxLength(128);

            builder.Property(x => x.OccupancyStatus)
                   .HasMaxLength(64);

            // Gecmis konum sorgularini milisaniyelere indirmek icin bilesik indeks
            builder.HasIndex(x => new { x.VehicleId, x.Timestamp });
        });
    }
}