using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fantastiche.Infrastructure.Common.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBombAuctions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BombAuctions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeagueSeasonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ListVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CallerTeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<string>(type: "nchar(1)", fixedLength: true, maxLength: 1, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Round = table.Column<int>(type: "int", nullable: false),
                    MinimumAmount = table.Column<int>(type: "int", nullable: false),
                    Deadline = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RevealStartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    NextRevealAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RevealedCount = table.Column<int>(type: "int", nullable: false),
                    PlayerAuctionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WinningTeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WinningAmount = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BombAuctions", x => x.Id);
                    table.UniqueConstraint("AK_BombAuctions_Id_LeagueSeasonId_LeagueId", x => new { x.Id, x.LeagueSeasonId, x.LeagueId });
                    table.CheckConstraint("CK_BombAuctions_Role", "[Role] IN (N'P', N'D', N'C', N'A')");
                    table.CheckConstraint("CK_BombAuctions_Round", "[Round] > 0 AND [MinimumAmount] > 0 AND [RevealedCount] >= 0");
                    table.CheckConstraint("CK_BombAuctions_Status", "[Status] IN (0, 1, 2, 3, 4)");
                    table.CheckConstraint("CK_BombAuctions_Winner", "([Status] = 2 AND [PlayerAuctionId] IS NOT NULL AND [WinningTeamId] IS NOT NULL AND [WinningAmount] IS NOT NULL AND [WinningAmount] > 0) OR ([Status] <> 2 AND [PlayerAuctionId] IS NULL AND [WinningTeamId] IS NULL AND [WinningAmount] IS NULL)");
                    table.ForeignKey(
                        name: "FK_BombAuctions_AuctionSessions_SessionId_LeagueSeasonId_LeagueId_ListVersionId",
                        columns: x => new { x.SessionId, x.LeagueSeasonId, x.LeagueId, x.ListVersionId },
                        principalTable: "AuctionSessions",
                        principalColumns: new[] { "Id", "LeagueSeasonId", "LeagueId", "ListVersionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BombAuctions_ListEntries_ListVersionId_PlayerId",
                        columns: x => new { x.ListVersionId, x.PlayerId },
                        principalTable: "ListEntries",
                        principalColumns: new[] { "ListVersionId", "PlayerId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BombAuctions_PlayerAuctions_PlayerAuctionId_LeagueSeasonId_LeagueId",
                        columns: x => new { x.PlayerAuctionId, x.LeagueSeasonId, x.LeagueId },
                        principalTable: "PlayerAuctions",
                        principalColumns: new[] { "Id", "LeagueSeasonId", "LeagueId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BombAuctions_Teams_CallerTeamId_LeagueSeasonId_LeagueId",
                        columns: x => new { x.CallerTeamId, x.LeagueSeasonId, x.LeagueId },
                        principalTable: "Teams",
                        principalColumns: new[] { "Id", "LeagueSeasonId", "LeagueId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BombAuctions_Teams_WinningTeamId_LeagueSeasonId_LeagueId",
                        columns: x => new { x.WinningTeamId, x.LeagueSeasonId, x.LeagueId },
                        principalTable: "Teams",
                        principalColumns: new[] { "Id", "LeagueSeasonId", "LeagueId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BombOffers",
                columns: table => new
                {
                    BombAuctionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Round = table.Column<int>(type: "int", nullable: false),
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeagueSeasonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<int>(type: "int", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BombOffers", x => new { x.BombAuctionId, x.Round, x.TeamId });
                    table.CheckConstraint("CK_BombOffers_Round", "[Round] > 0 AND [Position] >= 0");
                    table.CheckConstraint("CK_BombOffers_Submission", "([Amount] IS NULL AND [UserId] IS NULL AND [SubmittedAt] IS NULL) OR ([Amount] IS NOT NULL AND [Amount] > 0 AND [UserId] IS NOT NULL AND [SubmittedAt] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_BombOffers_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BombOffers_BombAuctions_BombAuctionId_LeagueSeasonId_LeagueId",
                        columns: x => new { x.BombAuctionId, x.LeagueSeasonId, x.LeagueId },
                        principalTable: "BombAuctions",
                        principalColumns: new[] { "Id", "LeagueSeasonId", "LeagueId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BombOffers_Teams_TeamId_LeagueSeasonId_LeagueId",
                        columns: x => new { x.TeamId, x.LeagueSeasonId, x.LeagueId },
                        principalTable: "Teams",
                        principalColumns: new[] { "Id", "LeagueSeasonId", "LeagueId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BombAuctions_CallerTeamId_LeagueSeasonId_LeagueId",
                table: "BombAuctions",
                columns: new[] { "CallerTeamId", "LeagueSeasonId", "LeagueId" });

            migrationBuilder.CreateIndex(
                name: "IX_BombAuctions_Deadline_Id",
                table: "BombAuctions",
                columns: new[] { "Deadline", "Id" },
                filter: "[Status] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_BombAuctions_ListVersionId_PlayerId",
                table: "BombAuctions",
                columns: new[] { "ListVersionId", "PlayerId" });

            migrationBuilder.CreateIndex(
                name: "IX_BombAuctions_NextRevealAt_Id",
                table: "BombAuctions",
                columns: new[] { "NextRevealAt", "Id" },
                filter: "[Status] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_BombAuctions_PlayerAuctionId_LeagueSeasonId_LeagueId",
                table: "BombAuctions",
                columns: new[] { "PlayerAuctionId", "LeagueSeasonId", "LeagueId" });

            migrationBuilder.CreateIndex(
                name: "IX_BombAuctions_SessionId",
                table: "BombAuctions",
                column: "SessionId",
                unique: true,
                filter: "[Status] < 2");

            migrationBuilder.CreateIndex(
                name: "IX_BombAuctions_SessionId_LeagueSeasonId_LeagueId_ListVersionId",
                table: "BombAuctions",
                columns: new[] { "SessionId", "LeagueSeasonId", "LeagueId", "ListVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_BombAuctions_WinningTeamId_LeagueSeasonId_LeagueId",
                table: "BombAuctions",
                columns: new[] { "WinningTeamId", "LeagueSeasonId", "LeagueId" });

            migrationBuilder.CreateIndex(
                name: "IX_BombOffers_BombAuctionId_LeagueSeasonId_LeagueId",
                table: "BombOffers",
                columns: new[] { "BombAuctionId", "LeagueSeasonId", "LeagueId" });

            migrationBuilder.CreateIndex(
                name: "IX_BombOffers_BombAuctionId_Round_Position",
                table: "BombOffers",
                columns: new[] { "BombAuctionId", "Round", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BombOffers_TeamId_LeagueSeasonId_LeagueId",
                table: "BombOffers",
                columns: new[] { "TeamId", "LeagueSeasonId", "LeagueId" });

            migrationBuilder.CreateIndex(
                name: "IX_BombOffers_UserId",
                table: "BombOffers",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BombOffers");

            migrationBuilder.DropTable(
                name: "BombAuctions");
        }
    }
}
