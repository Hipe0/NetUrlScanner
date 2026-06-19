using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NetUrlScanner.Migrations
{
    /// <inheritdoc />
    public partial class AddPackageBFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NormalizedDomain",
                table: "UrlScans",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SafeBrowsingStatus",
                table: "UrlScans",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SafeBrowsingThreatType",
                table: "UrlScans",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SiteCategory",
                table: "UrlScans",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SiteCategoryTags",
                table: "UrlScans",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DomainVotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    NormalizedDomain = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Vote = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DomainVotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DomainVotes_AppUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DomainVotes_UserId_NormalizedDomain",
                table: "DomainVotes",
                columns: new[] { "UserId", "NormalizedDomain" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DomainVotes");

            migrationBuilder.DropColumn(
                name: "NormalizedDomain",
                table: "UrlScans");

            migrationBuilder.DropColumn(
                name: "SafeBrowsingStatus",
                table: "UrlScans");

            migrationBuilder.DropColumn(
                name: "SafeBrowsingThreatType",
                table: "UrlScans");

            migrationBuilder.DropColumn(
                name: "SiteCategory",
                table: "UrlScans");

            migrationBuilder.DropColumn(
                name: "SiteCategoryTags",
                table: "UrlScans");
        }
    }
}
