using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eiti.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSaleSourceChannel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SourceChannel",
                table: "Sales",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TransferCounterpartSessionId",
                table: "CashMovements",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SourceChannel",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "TransferCounterpartSessionId",
                table: "CashMovements");
        }
    }
}
