using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Talent.Workflow.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "workflow");

            migrationBuilder.CreateTable(
                name: "delegations",
                schema: "workflow",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    delegator_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    delegate_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    valid_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    valid_to = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_delegations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "workflow_definitions",
                schema: "workflow",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflow_definitions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "workflow_versions",
                schema: "workflow",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_no = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    payload_json = table.Column<string>(type: "jsonb", nullable: false),
                    checksum = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    published_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflow_versions", x => x.id);
                    table.ForeignKey(
                        name: "FK_workflow_versions_workflow_definitions_definition_id",
                        column: x => x.definition_id,
                        principalSchema: "workflow",
                        principalTable: "workflow_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "workflow_instances",
                schema: "workflow",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    definition_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    definition_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    subject_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    subject_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    subject_employee_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    current_state = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    context_json = table.Column<string>(type: "jsonb", nullable: false),
                    started_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflow_instances", x => x.id);
                    table.ForeignKey(
                        name: "FK_workflow_instances_workflow_versions_definition_version_id",
                        column: x => x.definition_version_id,
                        principalSchema: "workflow",
                        principalTable: "workflow_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "approval_tasks",
                schema: "workflow",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    instance_id = table.Column<Guid>(type: "uuid", nullable: false),
                    state = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    due_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    escalation_level = table.Column<int>(type: "integer", nullable: false),
                    sla_breached_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_approval_tasks", x => x.id);
                    table.ForeignKey(
                        name: "FK_approval_tasks_workflow_instances_instance_id",
                        column: x => x.instance_id,
                        principalSchema: "workflow",
                        principalTable: "workflow_instances",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workflow_history",
                schema: "workflow",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    instance_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence = table.Column<int>(type: "integer", nullable: false),
                    from_state = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    to_state = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    actor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflow_history", x => x.id);
                    table.ForeignKey(
                        name: "FK_workflow_history_workflow_instances_instance_id",
                        column: x => x.instance_id,
                        principalSchema: "workflow",
                        principalTable: "workflow_instances",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "approval_task_assignees",
                schema: "workflow",
                columns: table => new
                {
                    task_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    delegated_from = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_approval_task_assignees", x => new { x.task_id, x.user_id });
                    table.ForeignKey(
                        name: "FK_approval_task_assignees_approval_tasks_task_id",
                        column: x => x.task_id,
                        principalSchema: "workflow",
                        principalTable: "approval_tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_approval_task_assignees_user_id",
                schema: "workflow",
                table: "approval_task_assignees",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_approval_tasks_instance_id",
                schema: "workflow",
                table: "approval_tasks",
                column: "instance_id");

            migrationBuilder.CreateIndex(
                name: "ix_approval_tasks_status_due_at",
                schema: "workflow",
                table: "approval_tasks",
                columns: new[] { "status", "due_at" });

            migrationBuilder.CreateIndex(
                name: "ix_delegations_delegator_id",
                schema: "workflow",
                table: "delegations",
                column: "delegator_id");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_definitions_code",
                schema: "workflow",
                table: "workflow_definitions",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_workflow_history_instance_id_sequence",
                schema: "workflow",
                table: "workflow_history",
                columns: new[] { "instance_id", "sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_workflow_instances_definition_version_id",
                schema: "workflow",
                table: "workflow_instances",
                column: "definition_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_instances_subject_type_subject_id",
                schema: "workflow",
                table: "workflow_instances",
                columns: new[] { "subject_type", "subject_id" });

            migrationBuilder.CreateIndex(
                name: "ux_workflow_instances_active_subject",
                schema: "workflow",
                table: "workflow_instances",
                columns: new[] { "subject_type", "subject_id" },
                unique: true,
                filter: "status = 'Active'");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_versions_definition_id_version_no",
                schema: "workflow",
                table: "workflow_versions",
                columns: new[] { "definition_id", "version_no" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "approval_task_assignees",
                schema: "workflow");

            migrationBuilder.DropTable(
                name: "delegations",
                schema: "workflow");

            migrationBuilder.DropTable(
                name: "workflow_history",
                schema: "workflow");

            migrationBuilder.DropTable(
                name: "approval_tasks",
                schema: "workflow");

            migrationBuilder.DropTable(
                name: "workflow_instances",
                schema: "workflow");

            migrationBuilder.DropTable(
                name: "workflow_versions",
                schema: "workflow");

            migrationBuilder.DropTable(
                name: "workflow_definitions",
                schema: "workflow");
        }
    }
}
