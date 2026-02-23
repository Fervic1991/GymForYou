using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymForYou.Api.Migrations;

public partial class AddTenantJoinLinks : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS ""TenantJoinLinks"" (
  ""Id"" uuid PRIMARY KEY,
  ""TenantId"" uuid NOT NULL,
  ""Code"" character varying(40) NOT NULL,
  ""IsActive"" boolean NOT NULL,
  ""ExpiresAtUtc"" timestamp with time zone NULL,
  ""MaxUses"" integer NULL,
  ""UsesCount"" integer NOT NULL,
  ""CreatedAtUtc"" timestamp with time zone NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS ""IX_TenantJoinLinks_Code"" ON ""TenantJoinLinks"" (""Code"");
CREATE INDEX IF NOT EXISTS ""IX_TenantJoinLinks_TenantId_IsActive_CreatedAtUtc"" ON ""TenantJoinLinks"" (""TenantId"", ""IsActive"", ""CreatedAtUtc"");
");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
DROP TABLE IF EXISTS ""TenantJoinLinks"";
");
    }
}
