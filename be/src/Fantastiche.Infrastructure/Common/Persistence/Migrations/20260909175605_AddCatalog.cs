using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fantastiche.Infrastructure.Common.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ListVersionId",
                table: "LeagueSeasons",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Clubs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NormalizedName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clubs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ListVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SeasonName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ContentHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    EntryCount = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ListVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ListVersions_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Players",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ExternalId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Players", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ListEntries",
                columns: table => new
                {
                    ListVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClubId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Role = table.Column<string>(type: "nchar(1)", fixedLength: true, maxLength: 1, nullable: false),
                    ClubName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BirthDate = table.Column<DateTime>(type: "date", nullable: false),
                    Nationality = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PreferredFoot = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ListEntries", x => new { x.ListVersionId, x.PlayerId });
                    table.ForeignKey(
                        name: "FK_ListEntries_Clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "Clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ListEntries_ListVersions_ListVersionId",
                        column: x => x.ListVersionId,
                        principalTable: "ListVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ListEntries_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LeagueSeasons_ListVersionId",
                table: "LeagueSeasons",
                column: "ListVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_Clubs_Source_NormalizedName",
                table: "Clubs",
                columns: new[] { "Source", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ListEntries_ClubId",
                table: "ListEntries",
                column: "ClubId");

            migrationBuilder.CreateIndex(
                name: "IX_ListEntries_ListVersionId_Role_ClubName",
                table: "ListEntries",
                columns: new[] { "ListVersionId", "Role", "ClubName" });

            migrationBuilder.CreateIndex(
                name: "IX_ListEntries_PlayerId",
                table: "ListEntries",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_ListVersions_CreatedByUserId",
                table: "ListVersions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ListVersions_Source_SeasonName_ContentHash",
                table: "ListVersions",
                columns: new[] { "Source", "SeasonName", "ContentHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ListVersions_Status_SeasonName_CreatedAt",
                table: "ListVersions",
                columns: new[] { "Status", "SeasonName", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Players_Source_ExternalId",
                table: "Players",
                columns: new[] { "Source", "ExternalId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_LeagueSeasons_ListVersions_ListVersionId",
                table: "LeagueSeasons",
                column: "ListVersionId",
                principalTable: "ListVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LeagueSeasons_ListVersions_ListVersionId",
                table: "LeagueSeasons");

            migrationBuilder.DropTable(
                name: "ListEntries");

            migrationBuilder.DropTable(
                name: "Clubs");

            migrationBuilder.DropTable(
                name: "ListVersions");

            migrationBuilder.DropTable(
                name: "Players");

            migrationBuilder.DropIndex(
                name: "IX_LeagueSeasons_ListVersionId",
                table: "LeagueSeasons");

            migrationBuilder.DropColumn(
                name: "ListVersionId",
                table: "LeagueSeasons");
        }
    }
}
