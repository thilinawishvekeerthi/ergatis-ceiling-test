using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Talent.Assessment.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialAssessment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "assessment");

            migrationBuilder.CreateTable(
                name: "nine_box_evaluations",
                schema: "assessment",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    grid_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    performance_level = table.Column<short>(type: "smallint", nullable: false),
                    potential_level = table.Column<short>(type: "smallint", nullable: false),
                    box_number = table.Column<short>(type: "smallint", nullable: false),
                    box_label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    talent_score = table.Column<short>(type: "smallint", nullable: false),
                    band = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reviewer_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    evaluated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nine_box_evaluations", x => x.id);
                    table.UniqueConstraint("AK_nine_box_evaluations_id_employee_id", x => new { x.id, x.employee_id });
                    table.CheckConstraint("ck_nine_box_evaluations_box", "box_number = (potential_level - 1) * 3 + performance_level");
                    table.CheckConstraint("ck_nine_box_evaluations_performance", "performance_level BETWEEN 1 AND 3");
                    table.CheckConstraint("ck_nine_box_evaluations_potential", "potential_level BETWEEN 1 AND 3");
                    table.CheckConstraint("ck_nine_box_evaluations_score", "talent_score = performance_level * potential_level");
                });

            migrationBuilder.CreateTable(
                name: "latest_nine_box_placements",
                schema: "assessment",
                columns: table => new
                {
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    evaluation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    evaluated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_latest_nine_box_placements", x => x.employee_id);
                    table.ForeignKey(
                        name: "FK_latest_nine_box_placements_nine_box_evaluations_evaluation_~",
                        columns: x => new { x.evaluation_id, x.employee_id },
                        principalSchema: "assessment",
                        principalTable: "nine_box_evaluations",
                        principalColumns: new[] { "id", "employee_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_latest_nine_box_placements_evaluation_id_employee_id",
                schema: "assessment",
                table: "latest_nine_box_placements",
                columns: new[] { "evaluation_id", "employee_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_nine_box_evaluations_employee_id_evaluated_at",
                schema: "assessment",
                table: "nine_box_evaluations",
                columns: new[] { "employee_id", "evaluated_at" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "latest_nine_box_placements",
                schema: "assessment");

            migrationBuilder.DropTable(
                name: "nine_box_evaluations",
                schema: "assessment");
        }
    }
}
