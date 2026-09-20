using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase1AddendumListsAndTerritoryHierarchy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ParentTerritoryId",
                table: "Territories",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "Territories",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "WhatsAppNumber",
                table: "Pharmacies",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WhatsAppNumber",
                table: "Doctors",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CustomerLists",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Mode = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    OwnerRepresentativeId = table.Column<int>(type: "int", nullable: true),
                    FilterTerritoryId = table.Column<int>(type: "int", nullable: true),
                    FilterClassificationId = table.Column<int>(type: "int", nullable: true),
                    FilterSegment = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    FilterActiveOnly = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerLists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerLists_DoctorClassifications_FilterClassificationId",
                        column: x => x.FilterClassificationId,
                        principalTable: "DoctorClassifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerLists_Representatives_OwnerRepresentativeId",
                        column: x => x.OwnerRepresentativeId,
                        principalTable: "Representatives",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerLists_Territories_FilterTerritoryId",
                        column: x => x.FilterTerritoryId,
                        principalTable: "Territories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomerTransferLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DoctorId = table.Column<int>(type: "int", nullable: true),
                    PharmacyId = table.Column<int>(type: "int", nullable: true),
                    FromRepresentativeId = table.Column<int>(type: "int", nullable: false),
                    ToRepresentativeId = table.Column<int>(type: "int", nullable: false),
                    TransferDateUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ApprovedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerTransferLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerTransferLogs_Doctors_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerTransferLogs_Pharmacies_PharmacyId",
                        column: x => x.PharmacyId,
                        principalTable: "Pharmacies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerTransferLogs_Representatives_FromRepresentativeId",
                        column: x => x.FromRepresentativeId,
                        principalTable: "Representatives",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerTransferLogs_Representatives_ToRepresentativeId",
                        column: x => x.ToRepresentativeId,
                        principalTable: "Representatives",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomerListItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerListId = table.Column<int>(type: "int", nullable: false),
                    DoctorId = table.Column<int>(type: "int", nullable: true),
                    PharmacyId = table.Column<int>(type: "int", nullable: true),
                    AddedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AddedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerListItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerListItems_CustomerLists_CustomerListId",
                        column: x => x.CustomerListId,
                        principalTable: "CustomerLists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CustomerListItems_Doctors_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerListItems_Pharmacies_PharmacyId",
                        column: x => x.PharmacyId,
                        principalTable: "Pharmacies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Territories_ParentTerritoryId",
                table: "Territories",
                column: "ParentTerritoryId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerListItems_CustomerListId_DoctorId",
                table: "CustomerListItems",
                columns: new[] { "CustomerListId", "DoctorId" },
                unique: true,
                filter: "[DoctorId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerListItems_CustomerListId_PharmacyId",
                table: "CustomerListItems",
                columns: new[] { "CustomerListId", "PharmacyId" },
                unique: true,
                filter: "[PharmacyId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerListItems_DoctorId",
                table: "CustomerListItems",
                column: "DoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerListItems_PharmacyId",
                table: "CustomerListItems",
                column: "PharmacyId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerLists_FilterClassificationId",
                table: "CustomerLists",
                column: "FilterClassificationId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerLists_FilterTerritoryId",
                table: "CustomerLists",
                column: "FilterTerritoryId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerLists_Name",
                table: "CustomerLists",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerLists_OwnerRepresentativeId",
                table: "CustomerLists",
                column: "OwnerRepresentativeId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerTransferLogs_DoctorId",
                table: "CustomerTransferLogs",
                column: "DoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerTransferLogs_FromRepresentativeId",
                table: "CustomerTransferLogs",
                column: "FromRepresentativeId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerTransferLogs_PharmacyId",
                table: "CustomerTransferLogs",
                column: "PharmacyId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerTransferLogs_ToRepresentativeId",
                table: "CustomerTransferLogs",
                column: "ToRepresentativeId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerTransferLogs_TransferDateUtc",
                table: "CustomerTransferLogs",
                column: "TransferDateUtc");

            migrationBuilder.AddForeignKey(
                name: "FK_Territories_Territories_ParentTerritoryId",
                table: "Territories",
                column: "ParentTerritoryId",
                principalTable: "Territories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Territories_Territories_ParentTerritoryId",
                table: "Territories");

            migrationBuilder.DropTable(
                name: "CustomerListItems");

            migrationBuilder.DropTable(
                name: "CustomerTransferLogs");

            migrationBuilder.DropTable(
                name: "CustomerLists");

            migrationBuilder.DropIndex(
                name: "IX_Territories_ParentTerritoryId",
                table: "Territories");

            migrationBuilder.DropColumn(
                name: "ParentTerritoryId",
                table: "Territories");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Territories");

            migrationBuilder.DropColumn(
                name: "WhatsAppNumber",
                table: "Pharmacies");

            migrationBuilder.DropColumn(
                name: "WhatsAppNumber",
                table: "Doctors");
        }
    }
}
