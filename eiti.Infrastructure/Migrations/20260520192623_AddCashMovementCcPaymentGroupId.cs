using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eiti.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCashMovementCcPaymentGroupId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CcPaymentGroupId",
                table: "CashMovements",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CcPaymentGroupId",
                table: "CashMovements");
        }
    }
}
