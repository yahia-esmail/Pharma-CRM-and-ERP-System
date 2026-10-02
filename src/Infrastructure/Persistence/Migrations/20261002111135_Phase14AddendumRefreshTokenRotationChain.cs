using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase14AddendumRefreshTokenRotationChain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReplacedByTokenHash",
                table: "RefreshTokens",
                type: "nvarchar(44)",
                maxLength: 44,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReplacedByTokenHash",
                table: "RefreshTokens");
        }
    }
}
