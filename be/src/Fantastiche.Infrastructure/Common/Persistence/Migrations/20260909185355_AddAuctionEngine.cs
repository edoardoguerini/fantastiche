using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fantastiche.Infrastructure.Common.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuctionEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuctionSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeagueSeasonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ListVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CurrentPosition = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuctionSessions", x => x.Id);
                    table.UniqueConstraint("AK_AuctionSessions_Id_LeagueSeasonId_LeagueId", x => new { x.Id, x.LeagueSeasonId, x.LeagueId });
                    table.UniqueConstraint("AK_AuctionSessions_Id_LeagueSeasonId_LeagueId_ListVersionId", x => new { x.Id, x.LeagueSeasonId, x.LeagueId, x.ListVersionId });
                    table.CheckConstraint("CK_AuctionSessions_CurrentPosition", "[CurrentPosition] >= 0");
                    table.CheckConstraint("CK_AuctionSessions_Status", "[Status] IN (0, 1, 2)");
                    table.CheckConstraint("CK_AuctionSessions_Version", "[Version] >= 1");
                    table.ForeignKey(
                        name: "FK_AuctionSessions_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuctionSessions_LeagueSeasons_LeagueSeasonId_LeagueId",
                        columns: x => new { x.LeagueSeasonId, x.LeagueId },
                        principalTable: "LeagueSeasons",
                        principalColumns: new[] { "Id", "LeagueId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuctionSessions_ListVersions_ListVersionId",
                        column: x => x.ListVersionId,
                        principalTable: "ListVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CallOrderEntries",
                columns: table => new
                {
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeagueSeasonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CallOrderEntries", x => new { x.SessionId, x.TeamId });
                    table.ForeignKey(
                        name: "FK_CallOrderEntries_AuctionSessions_SessionId_LeagueSeasonId_LeagueId",
                        columns: x => new { x.SessionId, x.LeagueSeasonId, x.LeagueId },
                        principalTable: "AuctionSessions",
                        principalColumns: new[] { "Id", "LeagueSeasonId", "LeagueId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CallOrderEntries_Teams_TeamId_LeagueSeasonId_LeagueId",
                        columns: x => new { x.TeamId, x.LeagueSeasonId, x.LeagueId },
                        principalTable: "Teams",
                        principalColumns: new[] { "Id", "LeagueSeasonId", "LeagueId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CommandReceipts",
                columns: table => new
                {
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CommandType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    PayloadHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ResultJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommandReceipts", x => new { x.SessionId, x.UserId, x.RequestId });
                    table.ForeignKey(
                        name: "FK_CommandReceipts_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommandReceipts_AuctionSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "AuctionSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlayerAuctions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeagueSeasonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ListVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<int>(type: "int", nullable: false),
                    CallerTeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WinningTeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<string>(type: "nchar(1)", fixedLength: true, maxLength: 1, nullable: false),
                    DurationSeconds = table.Column<int>(type: "int", nullable: false),
                    IncrementOptionsJson = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CurrentAmount = table.Column<int>(type: "int", nullable: false),
                    BidSequence = table.Column<int>(type: "int", nullable: false),
                    Deadline = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ClosedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerAuctions", x => x.Id);
                    table.UniqueConstraint("AK_PlayerAuctions_Id_LeagueSeasonId_LeagueId", x => new { x.Id, x.LeagueSeasonId, x.LeagueId });
                    table.CheckConstraint("CK_PlayerAuctions_CurrentAmount", "[CurrentAmount] > 0");
                    table.CheckConstraint("CK_PlayerAuctions_DurationSeconds", "[DurationSeconds] IN (5, 10, 15, 20, 25, 30)");
                    table.CheckConstraint("CK_PlayerAuctions_Role", "[Role] IN (N'P', N'D', N'C', N'A')");
                    table.CheckConstraint("CK_PlayerAuctions_Status", "[Status] IN (0, 1)");
                    table.ForeignKey(
                        name: "FK_PlayerAuctions_AuctionSessions_SessionId_LeagueSeasonId_LeagueId_ListVersionId",
                        columns: x => new { x.SessionId, x.LeagueSeasonId, x.LeagueId, x.ListVersionId },
                        principalTable: "AuctionSessions",
                        principalColumns: new[] { "Id", "LeagueSeasonId", "LeagueId", "ListVersionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlayerAuctions_ListEntries_ListVersionId_PlayerId",
                        columns: x => new { x.ListVersionId, x.PlayerId },
                        principalTable: "ListEntries",
                        principalColumns: new[] { "ListVersionId", "PlayerId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlayerAuctions_Teams_CallerTeamId_LeagueSeasonId_LeagueId",
                        columns: x => new { x.CallerTeamId, x.LeagueSeasonId, x.LeagueId },
                        principalTable: "Teams",
                        principalColumns: new[] { "Id", "LeagueSeasonId", "LeagueId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlayerAuctions_Teams_WinningTeamId_LeagueSeasonId_LeagueId",
                        columns: x => new { x.WinningTeamId, x.LeagueSeasonId, x.LeagueId },
                        principalTable: "Teams",
                        principalColumns: new[] { "Id", "LeagueSeasonId", "LeagueId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Bids",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlayerAuctionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeagueSeasonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<int>(type: "int", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    AcceptedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bids", x => x.Id);
                    table.CheckConstraint("CK_Bids_Amount", "[Amount] > 0");
                    table.ForeignKey(
                        name: "FK_Bids_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Bids_PlayerAuctions_PlayerAuctionId_LeagueSeasonId_LeagueId",
                        columns: x => new { x.PlayerAuctionId, x.LeagueSeasonId, x.LeagueId },
                        principalTable: "PlayerAuctions",
                        principalColumns: new[] { "Id", "LeagueSeasonId", "LeagueId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Bids_Teams_TeamId_LeagueSeasonId_LeagueId",
                        columns: x => new { x.TeamId, x.LeagueSeasonId, x.LeagueId },
                        principalTable: "Teams",
                        principalColumns: new[] { "Id", "LeagueSeasonId", "LeagueId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BudgetMovements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeagueSeasonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlayerAuctionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BudgetMovements", x => x.Id);
                    table.CheckConstraint("CK_BudgetMovements_Amount", "[Amount] < 0");
                    table.ForeignKey(
                        name: "FK_BudgetMovements_PlayerAuctions_PlayerAuctionId_LeagueSeasonId_LeagueId",
                        columns: x => new { x.PlayerAuctionId, x.LeagueSeasonId, x.LeagueId },
                        principalTable: "PlayerAuctions",
                        principalColumns: new[] { "Id", "LeagueSeasonId", "LeagueId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BudgetMovements_Teams_TeamId_LeagueSeasonId_LeagueId",
                        columns: x => new { x.TeamId, x.LeagueSeasonId, x.LeagueId },
                        principalTable: "Teams",
                        principalColumns: new[] { "Id", "LeagueSeasonId", "LeagueId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RosterEntries",
                columns: table => new
                {
                    LeagueSeasonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlayerAuctionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<string>(type: "nchar(1)", fixedLength: true, maxLength: 1, nullable: false),
                    Price = table.Column<int>(type: "int", nullable: false),
                    AcquiredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RosterEntries", x => new { x.LeagueSeasonId, x.PlayerId });
                    table.CheckConstraint("CK_RosterEntries_Price", "[Price] > 0");
                    table.CheckConstraint("CK_RosterEntries_Role", "[Role] IN (N'P', N'D', N'C', N'A')");
                    table.ForeignKey(
                        name: "FK_RosterEntries_PlayerAuctions_PlayerAuctionId_LeagueSeasonId_LeagueId",
                        columns: x => new { x.PlayerAuctionId, x.LeagueSeasonId, x.LeagueId },
                        principalTable: "PlayerAuctions",
                        principalColumns: new[] { "Id", "LeagueSeasonId", "LeagueId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RosterEntries_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RosterEntries_Teams_TeamId_LeagueSeasonId_LeagueId",
                        columns: x => new { x.TeamId, x.LeagueSeasonId, x.LeagueId },
                        principalTable: "Teams",
                        principalColumns: new[] { "Id", "LeagueSeasonId", "LeagueId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuctionSessions_CreatedByUserId",
                table: "AuctionSessions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AuctionSessions_LeagueSeasonId",
                table: "AuctionSessions",
                column: "LeagueSeasonId",
                unique: true,
                filter: "[Status] < 2");

            migrationBuilder.CreateIndex(
                name: "IX_AuctionSessions_LeagueSeasonId_LeagueId",
                table: "AuctionSessions",
                columns: new[] { "LeagueSeasonId", "LeagueId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuctionSessions_ListVersionId",
                table: "AuctionSessions",
                column: "ListVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_Bids_PlayerAuctionId_LeagueSeasonId_LeagueId",
                table: "Bids",
                columns: new[] { "PlayerAuctionId", "LeagueSeasonId", "LeagueId" });

            migrationBuilder.CreateIndex(
                name: "IX_Bids_PlayerAuctionId_Sequence",
                table: "Bids",
                columns: new[] { "PlayerAuctionId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bids_TeamId_LeagueSeasonId_LeagueId",
                table: "Bids",
                columns: new[] { "TeamId", "LeagueSeasonId", "LeagueId" });

            migrationBuilder.CreateIndex(
                name: "IX_Bids_UserId",
                table: "Bids",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetMovements_PlayerAuctionId",
                table: "BudgetMovements",
                column: "PlayerAuctionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BudgetMovements_PlayerAuctionId_LeagueSeasonId_LeagueId",
                table: "BudgetMovements",
                columns: new[] { "PlayerAuctionId", "LeagueSeasonId", "LeagueId" });

            migrationBuilder.CreateIndex(
                name: "IX_BudgetMovements_TeamId_LeagueSeasonId_LeagueId",
                table: "BudgetMovements",
                columns: new[] { "TeamId", "LeagueSeasonId", "LeagueId" });

            migrationBuilder.CreateIndex(
                name: "IX_CallOrderEntries_SessionId_LeagueSeasonId_LeagueId",
                table: "CallOrderEntries",
                columns: new[] { "SessionId", "LeagueSeasonId", "LeagueId" });

            migrationBuilder.CreateIndex(
                name: "IX_CallOrderEntries_SessionId_Position",
                table: "CallOrderEntries",
                columns: new[] { "SessionId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CallOrderEntries_TeamId_LeagueSeasonId_LeagueId",
                table: "CallOrderEntries",
                columns: new[] { "TeamId", "LeagueSeasonId", "LeagueId" });

            migrationBuilder.CreateIndex(
                name: "IX_CommandReceipts_UserId",
                table: "CommandReceipts",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerAuctions_CallerTeamId_LeagueSeasonId_LeagueId",
                table: "PlayerAuctions",
                columns: new[] { "CallerTeamId", "LeagueSeasonId", "LeagueId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerAuctions_Deadline_Id",
                table: "PlayerAuctions",
                columns: new[] { "Deadline", "Id" },
                filter: "[Status] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerAuctions_ListVersionId_PlayerId",
                table: "PlayerAuctions",
                columns: new[] { "ListVersionId", "PlayerId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerAuctions_SessionId",
                table: "PlayerAuctions",
                column: "SessionId",
                unique: true,
                filter: "[Status] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerAuctions_SessionId_LeagueSeasonId_LeagueId_ListVersionId",
                table: "PlayerAuctions",
                columns: new[] { "SessionId", "LeagueSeasonId", "LeagueId", "ListVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerAuctions_SessionId_Number",
                table: "PlayerAuctions",
                columns: new[] { "SessionId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerAuctions_WinningTeamId_LeagueSeasonId_LeagueId",
                table: "PlayerAuctions",
                columns: new[] { "WinningTeamId", "LeagueSeasonId", "LeagueId" });

            migrationBuilder.CreateIndex(
                name: "IX_RosterEntries_PlayerAuctionId",
                table: "RosterEntries",
                column: "PlayerAuctionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RosterEntries_PlayerAuctionId_LeagueSeasonId_LeagueId",
                table: "RosterEntries",
                columns: new[] { "PlayerAuctionId", "LeagueSeasonId", "LeagueId" });

            migrationBuilder.CreateIndex(
                name: "IX_RosterEntries_PlayerId",
                table: "RosterEntries",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_RosterEntries_TeamId_LeagueSeasonId_LeagueId",
                table: "RosterEntries",
                columns: new[] { "TeamId", "LeagueSeasonId", "LeagueId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Bids");

            migrationBuilder.DropTable(
                name: "BudgetMovements");

            migrationBuilder.DropTable(
                name: "CallOrderEntries");

            migrationBuilder.DropTable(
                name: "CommandReceipts");

            migrationBuilder.DropTable(
                name: "RosterEntries");

            migrationBuilder.DropTable(
                name: "PlayerAuctions");

            migrationBuilder.DropTable(
                name: "AuctionSessions");
        }
    }
}
