using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fantastiche.Infrastructure.Common.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBombWaitingLobby : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BombAuctions_Deadline_Id",
                table: "BombAuctions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BombAuctions_Status",
                table: "BombAuctions");

            migrationBuilder.CreateIndex(
                name: "IX_BombAuctions_Deadline_Id",
                table: "BombAuctions",
                columns: new[] { "Deadline", "Id" },
                filter: "[Status] < 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BombAuctions_Status",
                table: "BombAuctions",
                sql: "[Status] IN (-1, 0, 1, 2, 3, 4)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BombAuctions_Deadline_Id",
                table: "BombAuctions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BombAuctions_Status",
                table: "BombAuctions");

            migrationBuilder.CreateIndex(
                name: "IX_BombAuctions_Deadline_Id",
                table: "BombAuctions",
                columns: new[] { "Deadline", "Id" },
                filter: "[Status] = 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BombAuctions_Status",
                table: "BombAuctions",
                sql: "[Status] IN (0, 1, 2, 3, 4)");
        }
    }
}
