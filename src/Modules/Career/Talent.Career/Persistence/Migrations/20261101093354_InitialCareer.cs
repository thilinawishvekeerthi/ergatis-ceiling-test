using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Talent.Career.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCareer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "career");

            migrationBuilder.CreateTable(
                name: "jobs",
                schema: "career",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    level = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_jobs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "career_paths",
                schema: "career",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_job_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_career_paths", x => x.id);
                    table.ForeignKey(
                        name: "FK_career_paths_jobs_from_job_id",
                        column: x => x.from_job_id,
                        principalSchema: "career",
                        principalTable: "jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_career_paths_jobs_to_job_id",
                        column: x => x.to_job_id,
                        principalSchema: "career",
                        principalTable: "jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "employee_profiles",
                schema: "career",
                columns: table => new
                {
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    current_job_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_employee_profiles", x => x.employee_id);
                    table.ForeignKey(
                        name: "FK_employee_profiles_jobs_current_job_id",
                        column: x => x.current_job_id,
                        principalSchema: "career",
                        principalTable: "jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "job_requirements",
                schema: "career",
                columns: table => new
                {
                    job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    competency_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    required_level = table.Column<int>(type: "integer", nullable: false),
                    weight = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_requirements", x => new { x.job_id, x.competency_code });
                    table.ForeignKey(
                        name: "FK_job_requirements_jobs_job_id",
                        column: x => x.job_id,
                        principalSchema: "career",
                        principalTable: "jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "employee_competencies",
                schema: "career",
                columns: table => new
                {
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    competency_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    level = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_employee_competencies", x => new { x.employee_id, x.competency_code });
                    table.ForeignKey(
                        name: "FK_employee_competencies_employee_profiles_employee_id",
                        column: x => x.employee_id,
                        principalSchema: "career",
                        principalTable: "employee_profiles",
                        principalColumn: "employee_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_career_paths_from_job_id_to_job_id",
                schema: "career",
                table: "career_paths",
                columns: new[] { "from_job_id", "to_job_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_career_paths_to_job_id",
                schema: "career",
                table: "career_paths",
                column: "to_job_id");

            migrationBuilder.CreateIndex(
                name: "ix_employee_profiles_current_job_id",
                schema: "career",
                table: "employee_profiles",
                column: "current_job_id");

            migrationBuilder.CreateIndex(
                name: "ix_job_requirements_competency_code",
                schema: "career",
                table: "job_requirements",
                column: "competency_code");

            migrationBuilder.CreateIndex(
                name: "ix_jobs_code",
                schema: "career",
                table: "jobs",
                column: "code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "career_paths",
                schema: "career");

            migrationBuilder.DropTable(
                name: "employee_competencies",
                schema: "career");

            migrationBuilder.DropTable(
                name: "job_requirements",
                schema: "career");

            migrationBuilder.DropTable(
                name: "employee_profiles",
                schema: "career");

            migrationBuilder.DropTable(
                name: "jobs",
                schema: "career");
        }
    }
}
