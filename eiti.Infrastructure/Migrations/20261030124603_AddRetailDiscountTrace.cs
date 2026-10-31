using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eiti.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRetailDiscountTrace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "GeneralDiscountAt",
                table: "Sales",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GeneralDiscountByUserId",
                table: "Sales",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GeneralDiscountReason",
                table: "Sales",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MaxRetailDiscountPercent",
                table: "Companies",
                type: "numeric(5,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GeneralDiscountAt",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "GeneralDiscountByUserId",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "GeneralDiscountReason",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "MaxRetailDiscountPercent",
                table: "Companies");
        }
    }
}
