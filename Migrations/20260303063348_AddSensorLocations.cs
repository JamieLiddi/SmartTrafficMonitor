using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SmartTrafficMonitor.Migrations
{
    public partial class AddSensorLocations : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sensor_locations",
                schema: "public",
                columns: table => new
                {
                    sensor_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sensor_slug = table.Column<string>(type: "text", nullable: false),
                    latitude = table.Column<double>(type: "double precision", nullable: true),
                    longitude = table.Column<double>(type: "double precision", nullable: true),
                    zone = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sensor_locations", x => x.sensor_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_traffictable_MovementType",
                schema: "public",
                table: "traffictable",
                column: "MovementType");

            migrationBuilder.CreateIndex(
                name: "IX_traffictable_Season",
                schema: "public",
                table: "traffictable",
                column: "Season");

            migrationBuilder.CreateIndex(
                name: "IX_traffictable_SensorId",
                schema: "public",
                table: "traffictable",
                column: "SensorId");

            migrationBuilder.CreateIndex(
                name: "IX_traffictable_TimeStamp",
                schema: "public",
                table: "traffictable",
                column: "TimeStamp");

            migrationBuilder.CreateIndex(
                name: "IX_sensor_locations_sensor_slug",
                schema: "public",
                table: "sensor_locations",
                column: "sensor_slug",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sensor_locations",
                schema: "public");

            migrationBuilder.DropIndex(
                name: "IX_traffictable_MovementType",
                schema: "public",
                table: "traffictable");

            migrationBuilder.DropIndex(
                name: "IX_traffictable_Season",
                schema: "public",
                table: "traffictable");

            migrationBuilder.DropIndex(
                name: "IX_traffictable_SensorId",
                schema: "public",
                table: "traffictable");

            migrationBuilder.DropIndex(
                name: "IX_traffictable_TimeStamp",
                schema: "public",
                table: "traffictable");
        }
    }
}
