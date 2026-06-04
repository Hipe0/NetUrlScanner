using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NetUrlScanner.Migrations
{
    /// <inheritdoc />
    public partial class AddGeolocationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "UrlScans",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CountryCode",
                table: "UrlScans",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CountryName",
                table: "UrlScans",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IpAddress",
                table: "UrlScans",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Isp",
                table: "UrlScans",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "UrlScans",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "UrlScans",
                type: "float",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "City",
                table: "UrlScans");

            migrationBuilder.DropColumn(
                name: "CountryCode",
                table: "UrlScans");

            migrationBuilder.DropColumn(
                name: "CountryName",
                table: "UrlScans");

            migrationBuilder.DropColumn(
                name: "IpAddress",
                table: "UrlScans");

            migrationBuilder.DropColumn(
                name: "Isp",
                table: "UrlScans");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "UrlScans");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "UrlScans");
        }
    }
}
