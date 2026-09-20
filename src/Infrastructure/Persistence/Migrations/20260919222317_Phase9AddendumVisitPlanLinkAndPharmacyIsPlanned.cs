using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase9AddendumVisitPlanLinkAndPharmacyIsPlanned : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPlanned",
                table: "PharmacyVisits",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "VisitPlanItemId",
                table: "PharmacyVisits",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VisitPlanItemId",
                table: "DoctorVisits",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PharmacyVisits_VisitPlanItemId",
                table: "PharmacyVisits",
                column: "VisitPlanItemId");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorVisits_VisitPlanItemId",
                table: "DoctorVisits",
                column: "VisitPlanItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_DoctorVisits_VisitPlanItems_VisitPlanItemId",
                table: "DoctorVisits",
                column: "VisitPlanItemId",
                principalTable: "VisitPlanItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PharmacyVisits_VisitPlanItems_VisitPlanItemId",
                table: "PharmacyVisits",
                column: "VisitPlanItemId",
                principalTable: "VisitPlanItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DoctorVisits_VisitPlanItems_VisitPlanItemId",
                table: "DoctorVisits");

            migrationBuilder.DropForeignKey(
                name: "FK_PharmacyVisits_VisitPlanItems_VisitPlanItemId",
                table: "PharmacyVisits");

            migrationBuilder.DropIndex(
                name: "IX_PharmacyVisits_VisitPlanItemId",
                table: "PharmacyVisits");

            migrationBuilder.DropIndex(
                name: "IX_DoctorVisits_VisitPlanItemId",
                table: "DoctorVisits");

            migrationBuilder.DropColumn(
                name: "IsPlanned",
                table: "PharmacyVisits");

            migrationBuilder.DropColumn(
                name: "VisitPlanItemId",
                table: "PharmacyVisits");

            migrationBuilder.DropColumn(
                name: "VisitPlanItemId",
                table: "DoctorVisits");
        }
    }
}
