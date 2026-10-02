using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase16AddendumPushSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PushSubscriptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Endpoint = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    EndpointHash = table.Column<string>(type: "nvarchar(44)", maxLength: 44, nullable: false),
                    P256dh = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Auth = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastDeliveredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PushSubscriptions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PushSubscriptions_EndpointHash",
                table: "PushSubscriptions",
                column: "EndpointHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PushSubscriptions_UserId",
                table: "PushSubscriptions",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PushSubscriptions");
        }
    }
}
