using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eiti.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserFirstNameLastName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                table: "Users",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LastName",
                table: "Users",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            // Backfill para usuarios existentes: parte "Username" por el primer espacio en
            // nombre/apellido (mismo heuristico que UserEmployeeLinking.SplitUsername en C#),
            // ya que hasta ahora el username era el unico dato de nombre visible disponible.
            migrationBuilder.Sql(
                """
                UPDATE "Users"
                SET
                    "FirstName" = CASE
                        WHEN position(' ' in trim("Username")) > 0
                            THEN split_part(trim("Username"), ' ', 1)
                        ELSE trim("Username")
                    END,
                    "LastName" = CASE
                        WHEN position(' ' in trim("Username")) > 0
                            THEN trim(substring(trim("Username") from position(' ' in trim("Username")) + 1))
                        ELSE trim("Username")
                    END
                WHERE "FirstName" = '' AND "LastName" = '';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FirstName",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LastName",
                table: "Users");
        }
    }
}
