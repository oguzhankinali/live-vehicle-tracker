using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VehicleTracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "vehicle_positions",
                columns: table => new
                {
                    feed_entity_id = table.Column<string>(type: "text", nullable: false),
                    observed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    vehicle_id = table.Column<string>(type: "text", nullable: false),
                    vehicle_label = table.Column<string>(type: "text", nullable: true),
                    trip_id = table.Column<string>(type: "text", nullable: true),
                    route_id = table.Column<string>(type: "text", nullable: true),
                    direction_id = table.Column<long>(type: "bigint", nullable: true),
                    latitude = table.Column<double>(type: "double precision", nullable: false),
                    longitude = table.Column<double>(type: "double precision", nullable: false),
                    bearing = table.Column<float>(type: "real", nullable: true),
                    speed = table.Column<float>(type: "real", nullable: true),
                    stop_id = table.Column<string>(type: "text", nullable: true),
                    current_stop_sequence = table.Column<long>(type: "bigint", nullable: true),
                    current_status = table.Column<short>(type: "smallint", nullable: true),
                    occupancy_status = table.Column<short>(type: "smallint", nullable: true),
                    received_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vehicle_positions", x => new { x.feed_entity_id, x.observed_at });
                });

            migrationBuilder.CreateIndex(
                name: "ix_vehicle_positions_observed_at",
                table: "vehicle_positions",
                column: "observed_at");

            migrationBuilder.CreateIndex(
                name: "ix_vehicle_positions_vehicle_id_observed_at",
                table: "vehicle_positions",
                columns: new[] { "vehicle_id", "observed_at" },
                descending: new[] { false, true });
            
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS timescaledb;");
            migrationBuilder.Sql("SELECT create_hypertable('vehicle_positions', 'observed_at', chunk_time_interval => INTERVAL '30 minutes', if_not_exists => TRUE);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "vehicle_positions");
        }
    }
}
