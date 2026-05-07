using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eiti.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAccessProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccessProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SystemKey = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    IsSystem = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccessProfiles_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AccessProfilePermissions",
                columns: table => new
                {
                    AccessProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PermissionCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessProfilePermissions", x => new { x.AccessProfileId, x.PermissionCode });
                    table.ForeignKey(
                        name: "FK_AccessProfilePermissions_AccessProfiles_AccessProfileId",
                        column: x => x.AccessProfileId,
                        principalTable: "AccessProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddColumn<Guid>(
                name: "AccessProfileId",
                table: "Users",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "NewAccessProfileId",
                table: "UserRoleAudits",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NewAccessProfileName",
                table: "UserRoleAudits",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NewPermissionCodesCsv",
                table: "UserRoleAudits",
                type: "nvarchar(1200)",
                maxLength: 1200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "PreviousAccessProfileId",
                table: "UserRoleAudits",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreviousAccessProfileName",
                table: "UserRoleAudits",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreviousPermissionCodesCsv",
                table: "UserRoleAudits",
                type: "nvarchar(1200)",
                maxLength: 1200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_AccessProfiles_CompanyId_Name",
                table: "AccessProfiles",
                columns: new[] { "CompanyId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccessProfiles_CompanyId_SystemKey",
                table: "AccessProfiles",
                columns: new[] { "CompanyId", "SystemKey" },
                unique: true,
                filter: "[SystemKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Users_AccessProfileId",
                table: "Users",
                column: "AccessProfileId");

            // Seed default profiles from legacy UserRoles and migrate users
            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'[UserRoles]', N'U') IS NOT NULL
                BEGIN
                    CREATE TABLE #RoleProfiles (
                        SystemKey nvarchar(60) NOT NULL,
                        Name nvarchar(120) NOT NULL,
                        Description nvarchar(500) NOT NULL
                    );

                    INSERT INTO #RoleProfiles (SystemKey, Name, Description)
                    VALUES
                        (N'owner', N'Owner', N'Acceso total a ventas, caja y administracion de usuarios.'),
                        (N'admin', N'Admin', N'Administra la operacion comercial y de caja.'),
                        (N'seller', N'Vendedor', N'Opera ventas sin permisos de caja.'),
                        (N'cashier', N'Cajero', N'Opera caja sin administracion de usuarios.');

                    CREATE TABLE #RolePermissions (
                        RoleCode nvarchar(60) NOT NULL,
                        PermissionCode nvarchar(80) NOT NULL
                    );

                    INSERT INTO #RolePermissions (RoleCode, PermissionCode)
                    VALUES
                        (N'owner', N'sales.access'),
                        (N'owner', N'sales.create'),
                        (N'owner', N'sales.update'),
                        (N'owner', N'sales.delete'),
                        (N'owner', N'sales.pay'),
                        (N'owner', N'cash.access'),
                        (N'owner', N'cash.open'),
                        (N'owner', N'cash.close'),
                        (N'owner', N'cash.withdraw'),
                        (N'owner', N'cash.drawer.manage'),
                        (N'owner', N'cash.drawer.assign'),
                        (N'owner', N'cash.history.export'),
                        (N'owner', N'users.manage'),
                        (N'owner', N'sales.override_price'),
                        (N'owner', N'banks.manage'),
                        (N'owner', N'cheques.manage'),
                        (N'admin', N'sales.access'),
                        (N'admin', N'sales.create'),
                        (N'admin', N'sales.update'),
                        (N'admin', N'sales.delete'),
                        (N'admin', N'sales.pay'),
                        (N'admin', N'cash.access'),
                        (N'admin', N'cash.open'),
                        (N'admin', N'cash.close'),
                        (N'admin', N'cash.withdraw'),
                        (N'admin', N'cash.drawer.manage'),
                        (N'admin', N'cash.drawer.assign'),
                        (N'admin', N'cash.history.export'),
                        (N'admin', N'users.manage'),
                        (N'admin', N'sales.override_price'),
                        (N'admin', N'banks.manage'),
                        (N'admin', N'cheques.manage'),
                        (N'seller', N'sales.access'),
                        (N'seller', N'sales.create'),
                        (N'seller', N'sales.update'),
                        (N'seller', N'sales.pay'),
                        (N'cashier', N'cash.access'),
                        (N'cashier', N'cash.open'),
                        (N'cashier', N'cash.close'),
                        (N'cashier', N'cash.withdraw'),
                        (N'cashier', N'cash.history.export');

                    INSERT INTO AccessProfiles (Id, CompanyId, Name, Description, SystemKey, IsSystem, CreatedAt, UpdatedAt)
                    SELECT NEWID(), companies.CompanyId, roles.Name, roles.Description, roles.SystemKey, 1, SYSUTCDATETIME(), SYSUTCDATETIME()
                    FROM (SELECT DISTINCT CompanyId FROM Users) companies
                    CROSS JOIN #RoleProfiles roles
                    LEFT JOIN AccessProfiles existing
                        ON existing.CompanyId = companies.CompanyId
                        AND existing.SystemKey = roles.SystemKey
                    WHERE existing.Id IS NULL;

                    INSERT INTO AccessProfilePermissions (AccessProfileId, PermissionCode, AssignedAt)
                    SELECT DISTINCT profiles.Id, permissions.PermissionCode, SYSUTCDATETIME()
                    FROM AccessProfiles profiles
                    INNER JOIN #RolePermissions permissions
                        ON permissions.RoleCode = profiles.SystemKey
                    LEFT JOIN AccessProfilePermissions existing
                        ON existing.AccessProfileId = profiles.Id
                        AND existing.PermissionCode = permissions.PermissionCode
                    WHERE existing.AccessProfileId IS NULL;

                    ;WITH UserRoleCombos AS (
                        SELECT
                            users.Id AS UserId,
                            users.CompanyId,
                            STRING_AGG(userRoles.RoleCode, N',') WITHIN GROUP (ORDER BY userRoles.RoleCode) AS RoleCsv,
                            COUNT(*) AS RoleCount
                        FROM Users users
                        INNER JOIN UserRoles userRoles
                            ON userRoles.UserId = users.Id
                        GROUP BY users.Id, users.CompanyId
                    )
                    INSERT INTO AccessProfiles (Id, CompanyId, Name, Description, SystemKey, IsSystem, CreatedAt, UpdatedAt)
                    SELECT DISTINCT
                        NEWID(),
                        combos.CompanyId,
                        N'Migrado: ' + combos.RoleCsv,
                        N'Perfil generado automaticamente desde la combinacion legacy: ' + combos.RoleCsv,
                        NULL,
                        0,
                        SYSUTCDATETIME(),
                        SYSUTCDATETIME()
                    FROM UserRoleCombos combos
                    LEFT JOIN AccessProfiles existing
                        ON existing.CompanyId = combos.CompanyId
                        AND existing.Name = N'Migrado: ' + combos.RoleCsv
                        AND existing.SystemKey IS NULL
                    WHERE combos.RoleCount > 1
                        AND existing.Id IS NULL;

                    ;WITH UserRoleCombos AS (
                        SELECT
                            users.Id AS UserId,
                            users.CompanyId,
                            STRING_AGG(userRoles.RoleCode, N',') WITHIN GROUP (ORDER BY userRoles.RoleCode) AS RoleCsv,
                            COUNT(*) AS RoleCount
                        FROM Users users
                        INNER JOIN UserRoles userRoles
                            ON userRoles.UserId = users.Id
                        GROUP BY users.Id, users.CompanyId
                    )
                    INSERT INTO AccessProfilePermissions (AccessProfileId, PermissionCode, AssignedAt)
                    SELECT DISTINCT profiles.Id, permissions.PermissionCode, SYSUTCDATETIME()
                    FROM UserRoleCombos combos
                    INNER JOIN AccessProfiles profiles
                        ON profiles.CompanyId = combos.CompanyId
                        AND profiles.Name = N'Migrado: ' + combos.RoleCsv
                        AND profiles.SystemKey IS NULL
                    INNER JOIN UserRoles userRoles
                        ON userRoles.UserId = combos.UserId
                    INNER JOIN #RolePermissions permissions
                        ON permissions.RoleCode = userRoles.RoleCode
                    LEFT JOIN AccessProfilePermissions existing
                        ON existing.AccessProfileId = profiles.Id
                        AND existing.PermissionCode = permissions.PermissionCode
                    WHERE combos.RoleCount > 1
                        AND existing.AccessProfileId IS NULL;

                    ;WITH UserRoleCombos AS (
                        SELECT
                            users.Id AS UserId,
                            users.CompanyId,
                            STRING_AGG(userRoles.RoleCode, N',') WITHIN GROUP (ORDER BY userRoles.RoleCode) AS RoleCsv,
                            COUNT(*) AS RoleCount
                        FROM Users users
                        INNER JOIN UserRoles userRoles
                            ON userRoles.UserId = users.Id
                        GROUP BY users.Id, users.CompanyId
                    )
                    UPDATE users
                    SET AccessProfileId =
                        CASE
                            WHEN combos.RoleCount = 1 THEN singleProfiles.Id
                            ELSE comboProfiles.Id
                        END
                    FROM Users users
                    INNER JOIN UserRoleCombos combos
                        ON combos.UserId = users.Id
                    LEFT JOIN AccessProfiles singleProfiles
                        ON singleProfiles.CompanyId = users.CompanyId
                        AND singleProfiles.SystemKey = combos.RoleCsv
                    LEFT JOIN AccessProfiles comboProfiles
                        ON comboProfiles.CompanyId = users.CompanyId
                        AND comboProfiles.Name = N'Migrado: ' + combos.RoleCsv
                        AND comboProfiles.SystemKey IS NULL;

                    ;WITH AuditProfiles AS (
                        SELECT
                            audits.Id,
                            previousProfiles.Id AS PreviousProfileId,
                            previousProfiles.Name AS PreviousProfileName,
                            ISNULL((
                                SELECT STRING_AGG(permission.PermissionCode, N',') WITHIN GROUP (ORDER BY permission.PermissionCode)
                                FROM AccessProfilePermissions permission
                                WHERE permission.AccessProfileId = previousProfiles.Id
                            ), N'') AS PreviousPermissions,
                            newProfiles.Id AS NewProfileId,
                            newProfiles.Name AS NewProfileName,
                            ISNULL((
                                SELECT STRING_AGG(permission.PermissionCode, N',') WITHIN GROUP (ORDER BY permission.PermissionCode)
                                FROM AccessProfilePermissions permission
                                WHERE permission.AccessProfileId = newProfiles.Id
                            ), N'') AS NewPermissions
                        FROM UserRoleAudits audits
                        LEFT JOIN AccessProfiles previousProfiles
                            ON previousProfiles.CompanyId = audits.CompanyId
                            AND (
                                previousProfiles.SystemKey = audits.PreviousRolesCsv
                                OR previousProfiles.Name = N'Migrado: ' + audits.PreviousRolesCsv
                            )
                        LEFT JOIN AccessProfiles newProfiles
                            ON newProfiles.CompanyId = audits.CompanyId
                            AND (
                                newProfiles.SystemKey = audits.NewRolesCsv
                                OR newProfiles.Name = N'Migrado: ' + audits.NewRolesCsv
                            )
                    )
                    UPDATE audits
                    SET
                        PreviousAccessProfileId = mapped.PreviousProfileId,
                        PreviousAccessProfileName = mapped.PreviousProfileName,
                        PreviousPermissionCodesCsv = mapped.PreviousPermissions,
                        NewAccessProfileId = mapped.NewProfileId,
                        NewAccessProfileName = mapped.NewProfileName,
                        NewPermissionCodesCsv = mapped.NewPermissions
                    FROM UserRoleAudits audits
                    INNER JOIN AuditProfiles mapped
                        ON mapped.Id = audits.Id;

                    DROP TABLE #RolePermissions;
                    DROP TABLE #RoleProfiles;
                END
                """);

            migrationBuilder.Sql(
                """
                UPDATE UserRoleAudits
                SET PreviousPermissionCodesCsv = ISNULL(PreviousPermissionCodesCsv, N''),
                    NewPermissionCodesCsv = ISNULL(NewPermissionCodesCsv, N'')
                """);

            // Assign the owner system profile as fallback for users without a profile
            migrationBuilder.Sql(
                """
                UPDATE users
                SET AccessProfileId = fallback.Id
                FROM Users users
                INNER JOIN AccessProfiles fallback
                    ON fallback.CompanyId = users.CompanyId
                    AND fallback.SystemKey = N'owner'
                WHERE users.AccessProfileId IS NULL;
                """);

            // If there are still users without a profile (company has no profiles yet), seed them first
            migrationBuilder.Sql(
                """
                INSERT INTO AccessProfiles (Id, CompanyId, Name, Description, SystemKey, IsSystem, CreatedAt, UpdatedAt)
                SELECT NEWID(), u.CompanyId, N'Owner', N'Acceso total a ventas, caja y administracion de usuarios.', N'owner', 1, SYSUTCDATETIME(), SYSUTCDATETIME()
                FROM (SELECT DISTINCT CompanyId FROM Users WHERE AccessProfileId IS NULL) u
                LEFT JOIN AccessProfiles existing ON existing.CompanyId = u.CompanyId AND existing.SystemKey = N'owner'
                WHERE existing.Id IS NULL;

                UPDATE users
                SET AccessProfileId = fallback.Id
                FROM Users users
                INNER JOIN AccessProfiles fallback
                    ON fallback.CompanyId = users.CompanyId
                    AND fallback.SystemKey = N'owner'
                WHERE users.AccessProfileId IS NULL;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "AccessProfileId",
                table: "Users",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_AccessProfiles_AccessProfileId",
                table: "Users",
                column: "AccessProfileId",
                principalTable: "AccessProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'[UserRoles]', N'U') IS NOT NULL
                BEGIN
                    DROP TABLE [UserRoles];
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'[UserRoles]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [UserRoles] (
                        [UserId] uniqueidentifier NOT NULL,
                        [RoleCode] nvarchar(40) NOT NULL,
                        [AssignedAt] datetime2 NOT NULL,
                        CONSTRAINT [PK_UserRoles] PRIMARY KEY ([UserId], [RoleCode]),
                        CONSTRAINT [FK_UserRoles_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
                    );
                END
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Users_AccessProfiles_AccessProfileId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "AccessProfilePermissions");

            migrationBuilder.DropTable(
                name: "AccessProfiles");

            migrationBuilder.DropIndex(
                name: "IX_Users_AccessProfileId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "AccessProfileId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "NewAccessProfileId",
                table: "UserRoleAudits");

            migrationBuilder.DropColumn(
                name: "NewAccessProfileName",
                table: "UserRoleAudits");

            migrationBuilder.DropColumn(
                name: "NewPermissionCodesCsv",
                table: "UserRoleAudits");

            migrationBuilder.DropColumn(
                name: "PreviousAccessProfileId",
                table: "UserRoleAudits");

            migrationBuilder.DropColumn(
                name: "PreviousAccessProfileName",
                table: "UserRoleAudits");

            migrationBuilder.DropColumn(
                name: "PreviousPermissionCodesCsv",
                table: "UserRoleAudits");
        }
    }
}
