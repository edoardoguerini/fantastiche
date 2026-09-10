using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fantastiche.Infrastructure.Common.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogMarketData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CurrentMantraQuotation",
                table: "ListEntries",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CurrentQuotation",
                table: "ListEntries",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Fvm",
                table: "ListEntries",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InitialMantraQuotation",
                table: "ListEntries",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InitialQuotation",
                table: "ListEntries",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsTransferred",
                table: "ListEntries",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MantraFvm",
                table: "ListEntries",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MantraRole",
                table: "ListEntries",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentMantraQuotation",
                table: "ListEntries");

            migrationBuilder.DropColumn(
                name: "CurrentQuotation",
                table: "ListEntries");

            migrationBuilder.DropColumn(
                name: "Fvm",
                table: "ListEntries");

            migrationBuilder.DropColumn(
                name: "InitialMantraQuotation",
                table: "ListEntries");

            migrationBuilder.DropColumn(
                name: "InitialQuotation",
                table: "ListEntries");

            migrationBuilder.DropColumn(
                name: "IsTransferred",
                table: "ListEntries");

            migrationBuilder.DropColumn(
                name: "MantraFvm",
                table: "ListEntries");

            migrationBuilder.DropColumn(
                name: "MantraRole",
                table: "ListEntries");
        }
    }
}
