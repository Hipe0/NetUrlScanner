using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NetUrlScanner.Migrations
{
    /// <inheritdoc />
    public partial class AddUserIdToUrlScan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "UrlScans",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UrlScans_UserId",
                table: "UrlScans",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_UrlScans_AppUsers_UserId",
                table: "UrlScans",
                column: "UserId",
                principalTable: "AppUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UrlScans_AppUsers_UserId",
                table: "UrlScans");

            migrationBuilder.DropIndex(
                name: "IX_UrlScans_UserId",
                table: "UrlScans");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "UrlScans");
        }
    }
}
