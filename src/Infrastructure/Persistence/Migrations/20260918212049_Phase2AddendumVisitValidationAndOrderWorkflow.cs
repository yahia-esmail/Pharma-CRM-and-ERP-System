using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase2AddendumVisitValidationAndOrderWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DurationMinutes",
                table: "PharmacyVisits",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DurationTooShort",
                table: "PharmacyVisits",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "LocationMismatch",
                table: "PharmacyVisits",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "OutsideTerritory",
                table: "PharmacyVisits",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "Orders",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BonusQuantity",
                table: "OrderLines",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "DurationTooShort",
                table: "DoctorVisits",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsPlanned",
                table: "DoctorVisits",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "LocationMismatch",
                table: "DoctorVisits",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "OutsideTerritory",
                table: "DoctorVisits",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DurationMinutes",
                table: "PharmacyVisits");

            migrationBuilder.DropColumn(
                name: "DurationTooShort",
                table: "PharmacyVisits");

            migrationBuilder.DropColumn(
                name: "LocationMismatch",
                table: "PharmacyVisits");

            migrationBuilder.DropColumn(
                name: "OutsideTerritory",
                table: "PharmacyVisits");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "BonusQuantity",
                table: "OrderLines");

            migrationBuilder.DropColumn(
                name: "DurationTooShort",
                table: "DoctorVisits");

            migrationBuilder.DropColumn(
                name: "IsPlanned",
                table: "DoctorVisits");

            migrationBuilder.DropColumn(
                name: "LocationMismatch",
                table: "DoctorVisits");

            migrationBuilder.DropColumn(
                name: "OutsideTerritory",
                table: "DoctorVisits");
        }
    }
}
