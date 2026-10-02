using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase12AddendumVisitCheckInOutAndLocationProposals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "CheckInAccuracyMeters",
                table: "PharmacyVisits",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CheckInClockOffsetSeconds",
                table: "PharmacyVisits",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CheckInDeviceTimeUtc",
                table: "PharmacyVisits",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CheckInReceivedAtUtc",
                table: "PharmacyVisits",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CheckOutAccuracyMeters",
                table: "PharmacyVisits",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CheckOutClockOffsetSeconds",
                table: "PharmacyVisits",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CheckOutDeviceTimeUtc",
                table: "PharmacyVisits",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CheckOutLatitude",
                table: "PharmacyVisits",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CheckOutLongitude",
                table: "PharmacyVisits",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CheckOutReceivedAtUtc",
                table: "PharmacyVisits",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DeviceClockSuspect",
                table: "PharmacyVisits",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "OutsideGeofenceReason",
                table: "PharmacyVisits",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SessionStatus",
                table: "PharmacyVisits",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "CheckInAccuracyMeters",
                table: "DoctorVisits",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CheckInClockOffsetSeconds",
                table: "DoctorVisits",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CheckInDeviceTimeUtc",
                table: "DoctorVisits",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CheckInReceivedAtUtc",
                table: "DoctorVisits",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CheckOutAccuracyMeters",
                table: "DoctorVisits",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CheckOutClockOffsetSeconds",
                table: "DoctorVisits",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CheckOutDeviceTimeUtc",
                table: "DoctorVisits",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CheckOutLatitude",
                table: "DoctorVisits",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CheckOutLongitude",
                table: "DoctorVisits",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CheckOutReceivedAtUtc",
                table: "DoctorVisits",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DeviceClockSuspect",
                table: "DoctorVisits",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "InterestLevel",
                table: "DoctorVisits",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OutsideGeofenceReason",
                table: "DoctorVisits",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SessionStatus",
                table: "DoctorVisits",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "CustomerLocationProposals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DoctorId = table.Column<int>(type: "int", nullable: true),
                    PharmacyId = table.Column<int>(type: "int", nullable: true),
                    RepresentativeId = table.Column<int>(type: "int", nullable: false),
                    Latitude = table.Column<double>(type: "float", nullable: false),
                    Longitude = table.Column<double>(type: "float", nullable: false),
                    AccuracyMeters = table.Column<double>(type: "float", nullable: false),
                    CapturedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PreviousLatitude = table.Column<double>(type: "float", nullable: true),
                    PreviousLongitude = table.Column<double>(type: "float", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerLocationProposals", x => x.Id);
                    table.CheckConstraint("CK_CustomerLocationProposals_OneCustomer", "([DoctorId] IS NOT NULL AND [PharmacyId] IS NULL) OR ([DoctorId] IS NULL AND [PharmacyId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_CustomerLocationProposals_Doctors_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerLocationProposals_Pharmacies_PharmacyId",
                        column: x => x.PharmacyId,
                        principalTable: "Pharmacies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerLocationProposals_Representatives_RepresentativeId",
                        column: x => x.RepresentativeId,
                        principalTable: "Representatives",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerLocationProposals_DoctorId",
                table: "CustomerLocationProposals",
                column: "DoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerLocationProposals_PharmacyId",
                table: "CustomerLocationProposals",
                column: "PharmacyId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerLocationProposals_RepresentativeId",
                table: "CustomerLocationProposals",
                column: "RepresentativeId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerLocationProposals_Status",
                table: "CustomerLocationProposals",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerLocationProposals");

            migrationBuilder.DropColumn(
                name: "CheckInAccuracyMeters",
                table: "PharmacyVisits");

            migrationBuilder.DropColumn(
                name: "CheckInClockOffsetSeconds",
                table: "PharmacyVisits");

            migrationBuilder.DropColumn(
                name: "CheckInDeviceTimeUtc",
                table: "PharmacyVisits");

            migrationBuilder.DropColumn(
                name: "CheckInReceivedAtUtc",
                table: "PharmacyVisits");

            migrationBuilder.DropColumn(
                name: "CheckOutAccuracyMeters",
                table: "PharmacyVisits");

            migrationBuilder.DropColumn(
                name: "CheckOutClockOffsetSeconds",
                table: "PharmacyVisits");

            migrationBuilder.DropColumn(
                name: "CheckOutDeviceTimeUtc",
                table: "PharmacyVisits");

            migrationBuilder.DropColumn(
                name: "CheckOutLatitude",
                table: "PharmacyVisits");

            migrationBuilder.DropColumn(
                name: "CheckOutLongitude",
                table: "PharmacyVisits");

            migrationBuilder.DropColumn(
                name: "CheckOutReceivedAtUtc",
                table: "PharmacyVisits");

            migrationBuilder.DropColumn(
                name: "DeviceClockSuspect",
                table: "PharmacyVisits");

            migrationBuilder.DropColumn(
                name: "OutsideGeofenceReason",
                table: "PharmacyVisits");

            migrationBuilder.DropColumn(
                name: "SessionStatus",
                table: "PharmacyVisits");

            migrationBuilder.DropColumn(
                name: "CheckInAccuracyMeters",
                table: "DoctorVisits");

            migrationBuilder.DropColumn(
                name: "CheckInClockOffsetSeconds",
                table: "DoctorVisits");

            migrationBuilder.DropColumn(
                name: "CheckInDeviceTimeUtc",
                table: "DoctorVisits");

            migrationBuilder.DropColumn(
                name: "CheckInReceivedAtUtc",
                table: "DoctorVisits");

            migrationBuilder.DropColumn(
                name: "CheckOutAccuracyMeters",
                table: "DoctorVisits");

            migrationBuilder.DropColumn(
                name: "CheckOutClockOffsetSeconds",
                table: "DoctorVisits");

            migrationBuilder.DropColumn(
                name: "CheckOutDeviceTimeUtc",
                table: "DoctorVisits");

            migrationBuilder.DropColumn(
                name: "CheckOutLatitude",
                table: "DoctorVisits");

            migrationBuilder.DropColumn(
                name: "CheckOutLongitude",
                table: "DoctorVisits");

            migrationBuilder.DropColumn(
                name: "CheckOutReceivedAtUtc",
                table: "DoctorVisits");

            migrationBuilder.DropColumn(
                name: "DeviceClockSuspect",
                table: "DoctorVisits");

            migrationBuilder.DropColumn(
                name: "InterestLevel",
                table: "DoctorVisits");

            migrationBuilder.DropColumn(
                name: "OutsideGeofenceReason",
                table: "DoctorVisits");

            migrationBuilder.DropColumn(
                name: "SessionStatus",
                table: "DoctorVisits");
        }
    }
}
