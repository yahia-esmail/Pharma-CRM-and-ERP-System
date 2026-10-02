using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase13AddendumOrderSubmitLocationAndVisit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PharmacyVisitId",
                table: "Orders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "SubmitAccuracyMeters",
                table: "Orders",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "SubmitLatitude",
                table: "Orders",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "SubmitLongitude",
                table: "Orders",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedAtUtc",
                table: "Orders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_PharmacyVisitId",
                table: "Orders",
                column: "PharmacyVisitId");

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_PharmacyVisits_PharmacyVisitId",
                table: "Orders",
                column: "PharmacyVisitId",
                principalTable: "PharmacyVisits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_PharmacyVisits_PharmacyVisitId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_PharmacyVisitId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PharmacyVisitId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "SubmitAccuracyMeters",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "SubmitLatitude",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "SubmitLongitude",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "SubmittedAtUtc",
                table: "Orders");
        }
    }
}
