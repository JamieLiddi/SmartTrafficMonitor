﻿using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SmartTrafficMonitor.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create the table & schema 
            migrationBuilder.CreateTable(
                name: "traffictable",
                schema: "public",
                columns: table => new
                {
                    // Row PK
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),

                    // Sensor location id (repeats across rows)
                    sensor_id = table.Column<int>(type: "integer", nullable: false),

                    // Timestamp of record (match [Column("timestamp")] and Timestamp property)
                    timestamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),

                    // Type + context columns
                    movement_type = table.Column<string>(type: "text", nullable: true),
                    direction = table.Column<string>(type: "text", nullable: true),
                    season = table.Column<string>(type: "text", nullable: true),

                    // Counts
                    foot_traffic_count = table.Column<int>(type: "integer", nullable: false),
                    vehicle_count = table.Column<int>(type: "integer", nullable: false),

                    // Flags
                    public_transport_ref = table.Column<bool>(type: "boolean", nullable: false),
                    vu_schedule_ref = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_traffictable", x => x.id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop table + schema
            migrationBuilder.DropTable(
                name: "traffictable",
                schema: "public");
        }
    }
}
