using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Talent.Integration.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "integration");

            migrationBuilder.CreateTable(
                name: "dead_letters",
                schema: "integration",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    system = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    entity = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    external_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    payload_json = table.Column<string>(type: "jsonb", nullable: false),
                    error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    first_failed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_failed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dead_letters", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "external_id_map",
                schema: "integration",
                columns: table => new
                {
                    system = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    entity = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    external_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    internal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_external_id_map", x => new { x.system, x.entity, x.external_id });
                });

            migrationBuilder.CreateTable(
                name: "org_units",
                schema: "integration",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    source_modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    synced_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_org_units", x => x.id);
                    table.ForeignKey(
                        name: "FK_org_units_org_units_parent_id",
                        column: x => x.parent_id,
                        principalSchema: "integration",
                        principalTable: "org_units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sync_state",
                schema: "integration",
                columns: table => new
                {
                    system = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    entity = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    last_success_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sync_state", x => new { x.system, x.entity });
                });

            migrationBuilder.CreateTable(
                name: "positions",
                schema: "integration",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    org_unit_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    source_modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    synced_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_positions", x => x.id);
                    table.ForeignKey(
                        name: "FK_positions_org_units_org_unit_id",
                        column: x => x.org_unit_id,
                        principalSchema: "integration",
                        principalTable: "org_units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "employees",
                schema: "integration",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    first_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    last_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    org_unit_id = table.Column<Guid>(type: "uuid", nullable: true),
                    position_id = table.Column<Guid>(type: "uuid", nullable: true),
                    manager_id = table.Column<Guid>(type: "uuid", nullable: true),
                    hire_date = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    source_modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    synced_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_employees", x => x.id);
                    table.ForeignKey(
                        name: "FK_employees_employees_manager_id",
                        column: x => x.manager_id,
                        principalSchema: "integration",
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_employees_org_units_org_unit_id",
                        column: x => x.org_unit_id,
                        principalSchema: "integration",
                        principalTable: "org_units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_employees_positions_position_id",
                        column: x => x.position_id,
                        principalSchema: "integration",
                        principalTable: "positions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_dead_letters_last_failed_at",
                schema: "integration",
                table: "dead_letters",
                column: "last_failed_at");

            migrationBuilder.CreateIndex(
                name: "ux_dead_letters_open",
                schema: "integration",
                table: "dead_letters",
                columns: new[] { "system", "entity", "external_id" },
                unique: true,
                filter: "resolved_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_employees_employee_number",
                schema: "integration",
                table: "employees",
                column: "employee_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_employees_manager_id",
                schema: "integration",
                table: "employees",
                column: "manager_id");

            migrationBuilder.CreateIndex(
                name: "ix_employees_org_unit_id",
                schema: "integration",
                table: "employees",
                column: "org_unit_id");

            migrationBuilder.CreateIndex(
                name: "ix_employees_position_id",
                schema: "integration",
                table: "employees",
                column: "position_id");

            migrationBuilder.CreateIndex(
                name: "ix_external_id_map_entity_internal_id",
                schema: "integration",
                table: "external_id_map",
                columns: new[] { "entity", "internal_id" });

            migrationBuilder.CreateIndex(
                name: "ix_org_units_code",
                schema: "integration",
                table: "org_units",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_org_units_parent_id",
                schema: "integration",
                table: "org_units",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "ix_positions_code",
                schema: "integration",
                table: "positions",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_positions_org_unit_id",
                schema: "integration",
                table: "positions",
                column: "org_unit_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dead_letters",
                schema: "integration");

            migrationBuilder.DropTable(
                name: "employees",
                schema: "integration");

            migrationBuilder.DropTable(
                name: "external_id_map",
                schema: "integration");

            migrationBuilder.DropTable(
                name: "sync_state",
                schema: "integration");

            migrationBuilder.DropTable(
                name: "positions",
                schema: "integration");

            migrationBuilder.DropTable(
                name: "org_units",
                schema: "integration");
        }
    }
}
