using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Talent.Security.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "security");

            migrationBuilder.CreateTable(
                name: "role_assignments",
                schema: "security",
                columns: table => new
                {
                    subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_assignments", x => new { x.subject, x.role });
                });

            migrationBuilder.CreateTable(
                name: "user_accounts",
                schema: "security",
                columns: table => new
                {
                    subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_accounts", x => x.subject);
                });

            migrationBuilder.CreateIndex(
                name: "ix_user_accounts_employee_id",
                schema: "security",
                table: "user_accounts",
                column: "employee_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "role_assignments",
                schema: "security");

            migrationBuilder.DropTable(
                name: "user_accounts",
                schema: "security");
        }
    }
}
