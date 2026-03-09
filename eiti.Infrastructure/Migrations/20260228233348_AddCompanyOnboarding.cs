using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eiti.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyOnboarding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CompanyOnboarding",
                columns: table => new
                {
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HasCreatedBranch = table.Column<bool>(type: "bit", nullable: false),
                    HasCreatedCashDrawer = table.Column<bool>(type: "bit", nullable: false),
                    HasCompletedInitialCashOpen = table.Column<bool>(type: "bit", nullable: false),
                    HasCreatedProduct = table.Column<bool>(type: "bit", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyOnboarding", x => x.CompanyId);
                });

            migrationBuilder.Sql("""
                INSERT INTO [CompanyOnboarding]
                    ([CompanyId], [HasCreatedBranch], [HasCreatedCashDrawer], [HasCompletedInitialCashOpen], [HasCreatedProduct], [CompletedAt], [UpdatedAt])
                SELECT
                    [Id],
                    CAST(1 AS bit),
                    CAST(1 AS bit),
                    CAST(1 AS bit),
                    CAST(1 AS bit),
                    GETUTCDATE(),
                    GETUTCDATE()
                FROM [Companies]
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM [CompanyOnboarding] co
                    WHERE co.[CompanyId] = [Companies].[Id]
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompanyOnboarding");
        }
    }
}
