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
                name: "review_cycles",
                schema: "assessment",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    period_start = table.Column<DateOnly>(type: "date", nullable: false),
                    period_end = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    performance_scale_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    potential_scale_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    grid_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    opened_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_review_cycles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "reviews",
                schema: "assessment",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cycle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reviewer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    performance_level = table.Column<int>(type: "integer", nullable: true),
                    potential_level = table.Column<int>(type: "integer", nullable: true),
                    comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    rated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    proposed_column = table.Column<int>(type: "integer", nullable: true),
                    proposed_row = table.Column<int>(type: "integer", nullable: true),
                    final_column = table.Column<int>(type: "integer", nullable: true),
                    final_row = table.Column<int>(type: "integer", nullable: true),
                    calibrated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    calibration_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    calibrated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reviews", x => x.id);
                    table.ForeignKey(
                        name: "FK_reviews_review_cycles_cycle_id",
                        column: x => x.cycle_id,
                        principalSchema: "assessment",
                        principalTable: "review_cycles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_review_cycles_name",
                schema: "assessment",
                table: "review_cycles",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_reviews_cycle_id_employee_id",
                schema: "assessment",
                table: "reviews",
                columns: new[] { "cycle_id", "employee_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_reviews_cycle_id_reviewer_id",
                schema: "assessment",
                table: "reviews",
                columns: new[] { "cycle_id", "reviewer_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reviews",
                schema: "assessment");

            migrationBuilder.DropTable(
                name: "review_cycles",
                schema: "assessment");
        }
    }
}
