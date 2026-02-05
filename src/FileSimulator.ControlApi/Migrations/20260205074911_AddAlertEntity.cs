using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FileSimulator.ControlApi.Migrations
{
    /// <inheritdoc />
    public partial class AddAlertEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "alerts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    type = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    severity = table.Column<int>(type: "INTEGER", nullable: false),
                    title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    message = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    source = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    triggered_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    resolved_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    is_resolved = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alerts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "health_hourly",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    hour_start = table.Column<DateTime>(type: "TEXT", nullable: false),
                    server_id = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    server_type = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    sample_count = table.Column<int>(type: "INTEGER", nullable: false),
                    healthy_count = table.Column<int>(type: "INTEGER", nullable: false),
                    avg_latency_ms = table.Column<double>(type: "REAL", nullable: true),
                    min_latency_ms = table.Column<double>(type: "REAL", nullable: true),
                    max_latency_ms = table.Column<double>(type: "REAL", nullable: true),
                    p95_latency_ms = table.Column<double>(type: "REAL", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_health_hourly", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "health_samples",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    server_id = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    server_type = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    is_healthy = table.Column<bool>(type: "INTEGER", nullable: false),
                    latency_ms = table.Column<double>(type: "REAL", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_health_samples", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_alerts_triggered_at",
                table: "alerts",
                column: "triggered_at");

            migrationBuilder.CreateIndex(
                name: "ix_alerts_type_source_resolved",
                table: "alerts",
                columns: new[] { "type", "source", "is_resolved" });

            migrationBuilder.CreateIndex(
                name: "ix_health_hourly_server_hour",
                table: "health_hourly",
                columns: new[] { "server_id", "hour_start" });

            migrationBuilder.CreateIndex(
                name: "ix_health_samples_server_timestamp",
                table: "health_samples",
                columns: new[] { "server_id", "timestamp" });

            migrationBuilder.CreateIndex(
                name: "ix_health_samples_timestamp",
                table: "health_samples",
                column: "timestamp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "alerts");

            migrationBuilder.DropTable(
                name: "health_hourly");

            migrationBuilder.DropTable(
                name: "health_samples");
        }
    }
}
