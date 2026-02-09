using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using SmartTrafficMonitor.Models;

#nullable disable

namespace SmartTrafficMonitor.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20250808095338_InitialCreate")]
    partial class InitialCreate
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "6.0.21")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("SmartTrafficMonitor.Models.TrafficData", b =>
                {
                    // Id (PrimaryKey)
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .HasColumnName("id");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    //sensor_id
                    b.Property<int>("SensorId")
                        .HasColumnType("integer")
                        .HasColumnName("sensor_id");

                    // timestamp
                    b.Property<DateTime>("Timestamp")
                        .HasColumnType("timestamp without time zone")
                        .HasColumnName("timestamp");

                    //movement_type
                    b.Property<string>("MovementType")
                        .HasColumnType("text")
                        .HasColumnName("movement_type");

                    //direction
                    b.Property<string>("Direction")
                        .HasColumnType("text")
                        .HasColumnName("direction");

                    //season
                    b.Property<string>("Season")
                        .HasColumnType("text")
                        .HasColumnName("season");

                    // counts
                    b.Property<int>("FootTrafficCount")
                        .HasColumnType("integer")
                        .HasColumnName("foot_traffic_count");

                    b.Property<int>("VehicleCount")
                        .HasColumnType("integer")
                        .HasColumnName("vehicle_count");

                    // flags
                    b.Property<bool>("PublicTransportRef")
                        .HasColumnType("boolean")
                        .HasColumnName("public_transport_ref");

                    b.Property<bool>("VuScheduleRef")
                        .HasColumnType("boolean")
                        .HasColumnName("vu_schedule_ref");

                    b.HasKey("Id");

                    // table & schema
                    b.ToTable("traffictable", "public");
                });
#pragma warning restore 612, 618
        }
    }
}
