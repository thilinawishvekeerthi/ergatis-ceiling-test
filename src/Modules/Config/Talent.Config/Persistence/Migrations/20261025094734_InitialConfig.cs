using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Talent.Config.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "config");

            migrationBuilder.CreateTable(
                name: "config_definitions",
                schema: "config",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_config_definitions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "config_versions",
                schema: "config",
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
                    table.PrimaryKey("PK_config_versions", x => x.id);
                    table.ForeignKey(
                        name: "FK_config_versions_config_definitions_definition_id",
                        column: x => x.definition_id,
                        principalSchema: "config",
                        principalTable: "config_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "scale_levels",
                schema: "config",
                columns: table => new
                {
                    version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    level = table.Column<int>(type: "integer", nullable: false),
                    label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    min_score = table.Column<decimal>(type: "numeric(9,3)", precision: 9, scale: 3, nullable: false),
                    band = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scale_levels", x => new { x.version_id, x.level });
                    table.ForeignKey(
                        name: "FK_scale_levels_config_versions_version_id",
                        column: x => x.version_id,
                        principalSchema: "config",
                        principalTable: "config_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_config_definitions_type_code",
                schema: "config",
                table: "config_definitions",
                columns: new[] { "type", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_config_versions_definition_id_version_no",
                schema: "config",
                table: "config_versions",
                columns: new[] { "definition_id", "version_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_scale_levels_band",
                schema: "config",
                table: "scale_levels",
                column: "band");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "scale_levels",
                schema: "config");

            migrationBuilder.DropTable(
                name: "config_versions",
                schema: "config");

            migrationBuilder.DropTable(
                name: "config_definitions",
                schema: "config");
        }
    }
}
