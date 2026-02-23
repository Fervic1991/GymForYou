using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymForYou.Api.Migrations;

public partial class AddTenantDefaultLocale : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
ALTER TABLE IF EXISTS ""Tenants"" ADD COLUMN IF NOT EXISTS ""DefaultLocale"" character varying(2);
UPDATE ""Tenants"" SET ""DefaultLocale"" = 'it' WHERE COALESCE(""DefaultLocale"", '') = '';
ALTER TABLE IF EXISTS ""Tenants"" ALTER COLUMN ""DefaultLocale"" SET DEFAULT 'it';
ALTER TABLE IF EXISTS ""Tenants"" ALTER COLUMN ""DefaultLocale"" SET NOT NULL;
");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"ALTER TABLE IF EXISTS ""Tenants"" DROP COLUMN IF EXISTS ""DefaultLocale"";");
    }
}
