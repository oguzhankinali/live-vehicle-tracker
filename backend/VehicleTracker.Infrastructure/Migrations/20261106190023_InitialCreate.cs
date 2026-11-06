using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

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
                name: "VehiclePositions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    VehicleId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    VehicleLabel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    TripId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    RouteId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    DirectionId = table.Column<long>(type: "bigint", nullable: true),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false),
                    Bearing = table.Column<float>(type: "real", nullable: true),
                    Speed = table.Column<float>(type: "real", nullable: true),
                    StopId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CurrentStopSequence = table.Column<long>(type: "bigint", nullable: true),
                    CurrentStatus = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    OccupancyStatus = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehiclePositions", x => new { x.Id, x.Timestamp });
                });

            migrationBuilder.CreateIndex(
                name: "IX_VehiclePositions_Timestamp",
                table: "VehiclePositions",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_VehiclePositions_VehicleId_Timestamp",
                table: "VehiclePositions",
                columns: new[] { "VehicleId", "Timestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VehiclePositions");
        }
    }
}
