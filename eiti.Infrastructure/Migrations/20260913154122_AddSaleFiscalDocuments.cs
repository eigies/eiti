using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eiti.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSaleFiscalDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AutomaticInvoicing",
                table: "Companies",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AutomaticInvoicing",
                table: "Branches",
                type: "boolean",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SaleFiscalDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    SaleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    FiscalDocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReversedDocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    DocumentType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    PointOfSale = table.Column<int>(type: "integer", nullable: true),
                    Number = table.Column<long>(type: "bigint", nullable: true),
                    AuthorizationCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    AuthorizationExpiry = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    QrUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IssuedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaleFiscalDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SaleFiscalDocuments_Sales_SaleId",
                        column: x => x.SaleId,
                        principalTable: "Sales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SaleFiscalDocuments_CompanyId",
                table: "SaleFiscalDocuments",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleFiscalDocuments_FiscalDocumentId",
                table: "SaleFiscalDocuments",
                column: "FiscalDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleFiscalDocuments_ReversedDocumentId",
                table: "SaleFiscalDocuments",
                column: "ReversedDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleFiscalDocuments_SaleId_Kind_Sequence",
                table: "SaleFiscalDocuments",
                columns: new[] { "SaleId", "Kind", "Sequence" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SaleFiscalDocuments");

            migrationBuilder.DropColumn(
                name: "AutomaticInvoicing",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "AutomaticInvoicing",
                table: "Branches");
        }
    }
}
