using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eiti.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCashDrawerUserAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CashDrawerUserAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CashDrawerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashDrawerUserAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CashDrawerUserAssignments_CashDrawers_CashDrawerId",
                        column: x => x.CashDrawerId,
                        principalTable: "CashDrawers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CashDrawerUserAssignments_CashDrawerId",
                table: "CashDrawerUserAssignments",
                column: "CashDrawerId");

            migrationBuilder.CreateIndex(
                name: "IX_CashDrawerUserAssignments_UserId",
                table: "CashDrawerUserAssignments",
                column: "UserId",
                unique: true);

            migrationBuilder.Sql("""
                INSERT INTO CashDrawerUserAssignments (Id, CashDrawerId, UserId, AssignedAt)
                SELECT NEWID(), Id, AssignedUserId, SYSUTCDATETIME()
                FROM CashDrawers
                WHERE AssignedUserId IS NOT NULL
                """);

            migrationBuilder.DropIndex(
                name: "IX_CashDrawers_AssignedUserId",
                table: "CashDrawers");

            migrationBuilder.DropColumn(
                name: "AssignedUserId",
                table: "CashDrawers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AssignedUserId",
                table: "CashDrawers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql("""
                WITH RankedAssignments AS (
                    SELECT
                        CashDrawerId,
                        UserId,
                        ROW_NUMBER() OVER (PARTITION BY CashDrawerId ORDER BY AssignedAt, Id) AS RowNumber
                    FROM CashDrawerUserAssignments
                )
                UPDATE drawer
                SET AssignedUserId = assignment.UserId
                FROM CashDrawers drawer
                INNER JOIN RankedAssignments assignment
                    ON assignment.CashDrawerId = drawer.Id
                   AND assignment.RowNumber = 1
                """);

            migrationBuilder.CreateIndex(
                name: "IX_CashDrawers_AssignedUserId",
                table: "CashDrawers",
                column: "AssignedUserId");

            migrationBuilder.DropTable(
                name: "CashDrawerUserAssignments");
        }
    }
}
