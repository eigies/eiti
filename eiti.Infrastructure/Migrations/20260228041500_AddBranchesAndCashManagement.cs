using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eiti.Infrastructure.Migrations;

public partial class AddBranchesAndCashManagement : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Branches",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                Code = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                Address = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Branches", x => x.Id);
                table.ForeignKey(
                    name: "FK_Branches_Companies_CompanyId",
                    column: x => x.CompanyId,
                    principalTable: "Companies",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "CashDrawers",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CashDrawers", x => x.Id);
                table.ForeignKey(
                    name: "FK_CashDrawers_Branches_BranchId",
                    column: x => x.BranchId,
                    principalTable: "Branches",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_CashDrawers_Companies_CompanyId",
                    column: x => x.CompanyId,
                    principalTable: "Companies",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "CashSessions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CashDrawerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OpenedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ClosedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                OpenedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                OpeningAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                ActualClosingAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                Status = table.Column<int>(type: "int", nullable: false),
                Notes = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CashSessions", x => x.Id);
                table.ForeignKey(
                    name: "FK_CashSessions_Branches_BranchId",
                    column: x => x.BranchId,
                    principalTable: "Branches",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_CashSessions_CashDrawers_CashDrawerId",
                    column: x => x.CashDrawerId,
                    principalTable: "CashDrawers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_CashSessions_Companies_CompanyId",
                    column: x => x.CompanyId,
                    principalTable: "Companies",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_CashSessions_Users_ClosedByUserId",
                    column: x => x.ClosedByUserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_CashSessions_Users_OpenedByUserId",
                    column: x => x.OpenedByUserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "CashMovements",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CashSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Type = table.Column<int>(type: "int", nullable: false),
                Direction = table.Column<int>(type: "int", nullable: false),
                Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                ReferenceType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                ReferenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                Description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CashMovements", x => x.Id);
                table.ForeignKey(
                    name: "FK_CashMovements_CashSessions_CashSessionId",
                    column: x => x.CashSessionId,
                    principalTable: "CashSessions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_CashMovements_Users_CreatedByUserId",
                    column: x => x.CreatedByUserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.AddColumn<Guid>(
            name: "BranchId",
            table: "Sales",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "CashSessionId",
            table: "Sales",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "PaidAt",
            table: "Sales",
            type: "datetime2",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Branches_CompanyId_Name",
            table: "Branches",
            columns: new[] { "CompanyId", "Name" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_CashDrawers_BranchId_Name",
            table: "CashDrawers",
            columns: new[] { "BranchId", "Name" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_CashDrawers_CompanyId",
            table: "CashDrawers",
            column: "CompanyId");

        migrationBuilder.CreateIndex(
            name: "IX_CashMovements_CashSessionId_OccurredAt",
            table: "CashMovements",
            columns: new[] { "CashSessionId", "OccurredAt" });

        migrationBuilder.CreateIndex(
            name: "IX_CashMovements_CreatedByUserId",
            table: "CashMovements",
            column: "CreatedByUserId");

        migrationBuilder.CreateIndex(
            name: "IX_CashSessions_BranchId",
            table: "CashSessions",
            column: "BranchId");

        migrationBuilder.CreateIndex(
            name: "IX_CashSessions_CashDrawerId_OpenedAt",
            table: "CashSessions",
            columns: new[] { "CashDrawerId", "OpenedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_CashSessions_CashDrawerId_Status",
            table: "CashSessions",
            columns: new[] { "CashDrawerId", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_CashSessions_ClosedByUserId",
            table: "CashSessions",
            column: "ClosedByUserId");

        migrationBuilder.CreateIndex(
            name: "IX_CashSessions_CompanyId",
            table: "CashSessions",
            column: "CompanyId");

        migrationBuilder.CreateIndex(
            name: "IX_CashSessions_OpenedByUserId",
            table: "CashSessions",
            column: "OpenedByUserId");

        migrationBuilder.CreateIndex(
            name: "IX_Sales_BranchId",
            table: "Sales",
            column: "BranchId");

        migrationBuilder.CreateIndex(
            name: "IX_Sales_CashSessionId",
            table: "Sales",
            column: "CashSessionId");

        migrationBuilder.Sql("""
            DECLARE @BranchMap TABLE
            (
                CompanyId UNIQUEIDENTIFIER NOT NULL,
                BranchId UNIQUEIDENTIFIER NOT NULL
            );

            INSERT INTO Branches (Id, CompanyId, Name, Code, Address, CreatedAt, UpdatedAt)
            OUTPUT inserted.CompanyId, inserted.Id INTO @BranchMap (CompanyId, BranchId)
            SELECT NEWID(), c.Id, 'Sucursal Principal', 'MAIN', NULL, SYSUTCDATETIME(), NULL
            FROM Companies c;

            INSERT INTO CashDrawers (Id, CompanyId, BranchId, Name, IsActive, CreatedAt, UpdatedAt)
            SELECT NEWID(), b.CompanyId, b.BranchId, 'Caja Principal', 1, SYSUTCDATETIME(), NULL
            FROM @BranchMap b;

            UPDATE s
            SET s.BranchId = m.BranchId
            FROM Sales s
            INNER JOIN @BranchMap m ON m.CompanyId = s.CompanyId;
            """);

        migrationBuilder.AlterColumn<Guid>(
            name: "BranchId",
            table: "Sales",
            type: "uniqueidentifier",
            nullable: false,
            oldClrType: typeof(Guid),
            oldType: "uniqueidentifier",
            oldNullable: true);

        migrationBuilder.AddForeignKey(
            name: "FK_Sales_Branches_BranchId",
            table: "Sales",
            column: "BranchId",
            principalTable: "Branches",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_Sales_CashSessions_CashSessionId",
            table: "Sales",
            column: "CashSessionId",
            principalTable: "CashSessions",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(name: "FK_Sales_Branches_BranchId", table: "Sales");
        migrationBuilder.DropForeignKey(name: "FK_Sales_CashSessions_CashSessionId", table: "Sales");
        migrationBuilder.DropIndex(name: "IX_Sales_BranchId", table: "Sales");
        migrationBuilder.DropIndex(name: "IX_Sales_CashSessionId", table: "Sales");
        migrationBuilder.DropColumn(name: "BranchId", table: "Sales");
        migrationBuilder.DropColumn(name: "CashSessionId", table: "Sales");
        migrationBuilder.DropColumn(name: "PaidAt", table: "Sales");

        migrationBuilder.DropTable(name: "CashMovements");
        migrationBuilder.DropTable(name: "CashSessions");
        migrationBuilder.DropTable(name: "CashDrawers");
        migrationBuilder.DropTable(name: "Branches");
    }
}
