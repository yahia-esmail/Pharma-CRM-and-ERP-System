using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase11AddendumLocationPingAccuracy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "AccuracyMeters",
                table: "LocationPings",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "AltitudeMeters",
                table: "LocationPings",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ClientId",
                table: "LocationPings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Heading",
                table: "LocationPings",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAnomaly",
                table: "LocationPings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReceivedAtUtc",
                table: "LocationPings",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "LocationPings",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Track");

            migrationBuilder.AddColumn<double>(
                name: "SpeedMps",
                table: "LocationPings",
                type: "float",
                nullable: true);

            // Pings recorded before this migration were stored on receipt, so their fix time is the best
            // available receipt time (rather than the 0001-01-01 column default).
            migrationBuilder.Sql("UPDATE LocationPings SET ReceivedAtUtc = TimestampUtc");

            migrationBuilder.CreateIndex(
                name: "IX_LocationPings_RepresentativeId_ClientId",
                table: "LocationPings",
                columns: new[] { "RepresentativeId", "ClientId" },
                unique: true,
                filter: "[ClientId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LocationPings_RepresentativeId_ClientId",
                table: "LocationPings");

            migrationBuilder.DropColumn(
                name: "AccuracyMeters",
                table: "LocationPings");

            migrationBuilder.DropColumn(
                name: "AltitudeMeters",
                table: "LocationPings");

            migrationBuilder.DropColumn(
                name: "ClientId",
                table: "LocationPings");

            migrationBuilder.DropColumn(
                name: "Heading",
                table: "LocationPings");

            migrationBuilder.DropColumn(
                name: "IsAnomaly",
                table: "LocationPings");

            migrationBuilder.DropColumn(
                name: "ReceivedAtUtc",
                table: "LocationPings");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "LocationPings");

            migrationBuilder.DropColumn(
                name: "SpeedMps",
                table: "LocationPings");
        }
    }
}
