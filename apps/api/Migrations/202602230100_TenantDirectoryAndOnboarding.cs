using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymForYou.Api.Migrations;

public partial class TenantDirectoryAndOnboarding : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
ALTER TABLE IF EXISTS ""Tenants"" ADD COLUMN IF NOT EXISTS ""JoinCode"" character varying(24);
ALTER TABLE IF EXISTS ""Tenants"" ADD COLUMN IF NOT EXISTS ""City"" character varying(120);
ALTER TABLE IF EXISTS ""Tenants"" ADD COLUMN IF NOT EXISTS ""Address"" character varying(200);
ALTER TABLE IF EXISTS ""Tenants"" ADD COLUMN IF NOT EXISTS ""Phone"" character varying(30);
ALTER TABLE IF EXISTS ""Tenants"" ADD COLUMN IF NOT EXISTS ""IsSuspended"" boolean NOT NULL DEFAULT false;
UPDATE ""Tenants"" SET ""JoinCode"" = UPPER(substring(md5(""Id""::text || clock_timestamp()::text), 1, 8)) WHERE COALESCE(""JoinCode"", '') = '';
CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Tenants_JoinCode"" ON ""Tenants"" (""JoinCode"");
");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
DROP INDEX IF EXISTS ""IX_Tenants_JoinCode"";
ALTER TABLE IF EXISTS ""Tenants"" DROP COLUMN IF EXISTS ""JoinCode"";
ALTER TABLE IF EXISTS ""Tenants"" DROP COLUMN IF EXISTS ""City"";
ALTER TABLE IF EXISTS ""Tenants"" DROP COLUMN IF EXISTS ""Address"";
ALTER TABLE IF EXISTS ""Tenants"" DROP COLUMN IF EXISTS ""Phone"";
ALTER TABLE IF EXISTS ""Tenants"" DROP COLUMN IF EXISTS ""IsSuspended"";
");
    }
}
