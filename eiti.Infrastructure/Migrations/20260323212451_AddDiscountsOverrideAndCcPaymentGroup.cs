using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eiti.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDiscountsOverrideAndCcPaymentGroup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "GeneralDiscountPercent",
                table: "Sales",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ManualOverridePrice",
                table: "Sales",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OriginalTotal",
                table: "Sales",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "OverriddenAt",
                table: "Sales",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OverriddenByUserId",
                table: "Sales",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountPercent",
                table: "SaleDetails",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "GroupId",
                table: "SaleCcPayments",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GeneralDiscountPercent",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "ManualOverridePrice",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "OriginalTotal",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "OverriddenAt",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "OverriddenByUserId",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "DiscountPercent",
                table: "SaleDetails");

            migrationBuilder.DropColumn(
                name: "GroupId",
                table: "SaleCcPayments");
        }
    }
}
