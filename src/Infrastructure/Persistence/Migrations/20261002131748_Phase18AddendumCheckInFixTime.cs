using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase18AddendumCheckInFixTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CheckInFixElapsedMs",
                table: "PharmacyVisits",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CheckInFixElapsedMs",
                table: "DoctorVisits",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CheckInFixElapsedMs",
                table: "PharmacyVisits");

            migrationBuilder.DropColumn(
                name: "CheckInFixElapsedMs",
                table: "DoctorVisits");
        }
    }
}
