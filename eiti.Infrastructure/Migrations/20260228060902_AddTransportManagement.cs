using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eiti.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTransportManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasDelivery",
                table: "Sales",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "TransportAssignmentId",
                table: "Sales",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DriverProfiles",
                columns: table => new
                {
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LicenseNumber = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    LicenseCategory = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    LicenseExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EmergencyContactName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    EmergencyContactPhone = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DriverProfiles", x => x.EmployeeId);
                });

            migrationBuilder.CreateTable(
                name: "Employees",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FirstName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    DocumentNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    EmployeeRole = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Employees", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FleetLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PerformedByEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Odometer = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    FuelLiters = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    FuelCost = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    MaintenanceType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FleetLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SaleTransportAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SaleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DriverEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DispatchedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeliveredAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaleTransportAssignments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Vehicles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssignedDriverEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Plate = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Model = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Brand = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Year = table.Column<int>(type: "int", nullable: true),
                    FuelType = table.Column<int>(type: "int", nullable: false),
                    CurrentOdometer = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    LastFuelLoadedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastMaintenanceAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vehicles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Sales_TransportAssignmentId",
                table: "Sales",
                column: "TransportAssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_DriverProfiles_CompanyId_LicenseNumber",
                table: "DriverProfiles",
                columns: new[] { "CompanyId", "LicenseNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_CompanyId_EmployeeRole_IsActive",
                table: "Employees",
                columns: new[] { "CompanyId", "EmployeeRole", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Employees_CompanyId_LastName_FirstName",
                table: "Employees",
                columns: new[] { "CompanyId", "LastName", "FirstName" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetLogs_CompanyId_Type_OccurredAt",
                table: "FleetLogs",
                columns: new[] { "CompanyId", "Type", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetLogs_VehicleId_OccurredAt",
                table: "FleetLogs",
                columns: new[] { "VehicleId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SaleTransportAssignments_CompanyId_DriverEmployeeId_Status",
                table: "SaleTransportAssignments",
                columns: new[] { "CompanyId", "DriverEmployeeId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SaleTransportAssignments_CompanyId_VehicleId_Status",
                table: "SaleTransportAssignments",
                columns: new[] { "CompanyId", "VehicleId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SaleTransportAssignments_SaleId",
                table: "SaleTransportAssignments",
                column: "SaleId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_CompanyId_AssignedDriverEmployeeId_IsActive",
                table: "Vehicles",
                columns: new[] { "CompanyId", "AssignedDriverEmployeeId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_CompanyId_Plate",
                table: "Vehicles",
                columns: new[] { "CompanyId", "Plate" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Sales_SaleTransportAssignments_TransportAssignmentId",
                table: "Sales",
                column: "TransportAssignmentId",
                principalTable: "SaleTransportAssignments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Sales_SaleTransportAssignments_TransportAssignmentId",
                table: "Sales");

            migrationBuilder.DropTable(
                name: "DriverProfiles");

            migrationBuilder.DropTable(
                name: "Employees");

            migrationBuilder.DropTable(
                name: "FleetLogs");

            migrationBuilder.DropTable(
                name: "SaleTransportAssignments");

            migrationBuilder.DropTable(
                name: "Vehicles");

            migrationBuilder.DropIndex(
                name: "IX_Sales_TransportAssignmentId",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "HasDelivery",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "TransportAssignmentId",
                table: "Sales");
        }
    }
}
