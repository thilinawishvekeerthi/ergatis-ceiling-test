using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Talent.Shared.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AppendOnlyAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE FUNCTION shared.audit_log_block_changes() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    RAISE EXCEPTION 'shared.audit_log is append-only';
END;
$$;

CREATE TRIGGER audit_log_no_update_delete
    BEFORE UPDATE OR DELETE ON shared.audit_log
    FOR EACH ROW EXECUTE FUNCTION shared.audit_log_block_changes();

CREATE TRIGGER audit_log_no_truncate
    BEFORE TRUNCATE ON shared.audit_log
    FOR EACH STATEMENT EXECUTE FUNCTION shared.audit_log_block_changes();");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP TRIGGER audit_log_no_truncate ON shared.audit_log;
DROP TRIGGER audit_log_no_update_delete ON shared.audit_log;
DROP FUNCTION shared.audit_log_block_changes();");
        }
    }
}
