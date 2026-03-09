using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eiti.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerCompanyScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Customers_DocumentType_DocumentNumber",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Customers_Email",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Customers_TaxId",
                table: "Customers");

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "Customers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql(@"
DECLARE @FallbackCompanyId uniqueidentifier = '11111111-1111-1111-1111-111111111111';

IF NOT EXISTS (SELECT 1 FROM [Companies] WHERE [Id] = @FallbackCompanyId)
    THROW 50001, 'Migration aborted: fallback company 11111111-1111-1111-1111-111111111111 was not found.', 1;

CREATE TABLE #CustomerCompanyMap
(
    [OriginalCustomerId] uniqueidentifier NOT NULL,
    [CompanyId] uniqueidentifier NOT NULL,
    [TargetCustomerId] uniqueidentifier NULL,
    [IsPrimary] bit NOT NULL DEFAULT(0),
    CONSTRAINT [PK_CustomerCompanyMap] PRIMARY KEY ([OriginalCustomerId], [CompanyId])
);

INSERT INTO #CustomerCompanyMap ([OriginalCustomerId], [CompanyId])
SELECT DISTINCT s.[CustomerId], s.[CompanyId]
FROM [Sales] s
WHERE s.[CustomerId] IS NOT NULL;

INSERT INTO #CustomerCompanyMap ([OriginalCustomerId], [CompanyId])
SELECT c.[Id], @FallbackCompanyId
FROM [Customers] c
WHERE NOT EXISTS (
    SELECT 1
    FROM #CustomerCompanyMap m
    WHERE m.[OriginalCustomerId] = c.[Id]
);

;WITH RankedMap AS
(
    SELECT
        m.[OriginalCustomerId],
        m.[CompanyId],
        ROW_NUMBER() OVER (PARTITION BY m.[OriginalCustomerId] ORDER BY m.[CompanyId]) AS [RowNumber]
    FROM #CustomerCompanyMap m
)
UPDATE m
SET
    m.[IsPrimary] = 1,
    m.[TargetCustomerId] = m.[OriginalCustomerId]
FROM #CustomerCompanyMap m
INNER JOIN RankedMap r
    ON r.[OriginalCustomerId] = m.[OriginalCustomerId]
    AND r.[CompanyId] = m.[CompanyId]
WHERE r.[RowNumber] = 1;

UPDATE c
SET c.[CompanyId] = m.[CompanyId]
FROM [Customers] c
INNER JOIN #CustomerCompanyMap m
    ON m.[OriginalCustomerId] = c.[Id]
WHERE m.[IsPrimary] = 1;

CREATE TABLE #CustomersToClone
(
    [OriginalCustomerId] uniqueidentifier NOT NULL,
    [CompanyId] uniqueidentifier NOT NULL,
    [NewCustomerId] uniqueidentifier NOT NULL,
    [Name] nvarchar(201) NOT NULL,
    [FirstName] nvarchar(100) NOT NULL,
    [LastName] nvarchar(100) NOT NULL,
    [Email] nvarchar(255) NOT NULL,
    [Phone] nvarchar(30) NOT NULL,
    [DocumentType] int NULL,
    [DocumentNumber] nvarchar(30) NULL,
    [TaxId] nvarchar(20) NULL,
    [AddressId] uniqueidentifier NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    CONSTRAINT [PK_CustomersToClone] PRIMARY KEY ([OriginalCustomerId], [CompanyId])
);

INSERT INTO #CustomersToClone
(
    [OriginalCustomerId],
    [CompanyId],
    [NewCustomerId],
    [Name],
    [FirstName],
    [LastName],
    [Email],
    [Phone],
    [DocumentType],
    [DocumentNumber],
    [TaxId],
    [AddressId],
    [CreatedAt],
    [UpdatedAt]
)
SELECT
    m.[OriginalCustomerId],
    m.[CompanyId],
    NEWID(),
    c.[Name],
    c.[FirstName],
    c.[LastName],
    c.[Email],
    c.[Phone],
    c.[DocumentType],
    c.[DocumentNumber],
    c.[TaxId],
    c.[AddressId],
    c.[CreatedAt],
    c.[UpdatedAt]
FROM #CustomerCompanyMap m
INNER JOIN [Customers] c
    ON c.[Id] = m.[OriginalCustomerId]
WHERE m.[IsPrimary] = 0;

INSERT INTO [Customers]
(
    [Id],
    [CompanyId],
    [Name],
    [FirstName],
    [LastName],
    [Email],
    [Phone],
    [DocumentType],
    [DocumentNumber],
    [TaxId],
    [AddressId],
    [CreatedAt],
    [UpdatedAt]
)
SELECT
    [NewCustomerId],
    [CompanyId],
    [Name],
    [FirstName],
    [LastName],
    [Email],
    [Phone],
    [DocumentType],
    [DocumentNumber],
    [TaxId],
    [AddressId],
    [CreatedAt],
    [UpdatedAt]
FROM #CustomersToClone;

UPDATE m
SET m.[TargetCustomerId] = c.[NewCustomerId]
FROM #CustomerCompanyMap m
INNER JOIN #CustomersToClone c
    ON c.[OriginalCustomerId] = m.[OriginalCustomerId]
    AND c.[CompanyId] = m.[CompanyId]
WHERE m.[IsPrimary] = 0;

UPDATE s
SET s.[CustomerId] = m.[TargetCustomerId]
FROM [Sales] s
INNER JOIN #CustomerCompanyMap m
    ON m.[OriginalCustomerId] = s.[CustomerId]
    AND m.[CompanyId] = s.[CompanyId]
WHERE s.[CustomerId] IS NOT NULL
    AND s.[CustomerId] <> m.[TargetCustomerId];

IF EXISTS (SELECT 1 FROM [Customers] WHERE [CompanyId] IS NULL)
    THROW 50002, 'Migration aborted: unresolved customers without CompanyId.', 1;

DROP TABLE #CustomersToClone;
DROP TABLE #CustomerCompanyMap;
");

            migrationBuilder.AlterColumn<Guid>(
                name: "CompanyId",
                table: "Customers",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Customers_CompanyId_DocumentType_DocumentNumber",
                table: "Customers",
                columns: new[] { "CompanyId", "DocumentType", "DocumentNumber" },
                unique: true,
                filter: "[DocumentType] IS NOT NULL AND [DocumentNumber] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_CompanyId_Email",
                table: "Customers",
                columns: new[] { "CompanyId", "Email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Customers_CompanyId_Name",
                table: "Customers",
                columns: new[] { "CompanyId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_Customers_CompanyId_TaxId",
                table: "Customers",
                columns: new[] { "CompanyId", "TaxId" },
                unique: true,
                filter: "[TaxId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Customers_Companies_CompanyId",
                table: "Customers",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Customers_Companies_CompanyId",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Customers_CompanyId_DocumentType_DocumentNumber",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Customers_CompanyId_Email",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Customers_CompanyId_Name",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Customers_CompanyId_TaxId",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "Customers");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_DocumentType_DocumentNumber",
                table: "Customers",
                columns: new[] { "DocumentType", "DocumentNumber" },
                unique: true,
                filter: "[DocumentType] IS NOT NULL AND [DocumentNumber] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_Email",
                table: "Customers",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Customers_TaxId",
                table: "Customers",
                column: "TaxId",
                unique: true,
                filter: "[TaxId] IS NOT NULL");
        }
    }
}
