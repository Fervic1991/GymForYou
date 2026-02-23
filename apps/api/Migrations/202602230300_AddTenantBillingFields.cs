using Microsoft.EntityFrameworkCore.Migrations;

namespace GymForYou.Api.Migrations;

public partial class AddTenantBillingFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
ALTER TABLE IF EXISTS "Tenants" ADD COLUMN IF NOT EXISTS "BillingStatus" character varying(20) NOT NULL DEFAULT 'PAID';
ALTER TABLE IF EXISTS "Tenants" ADD COLUMN IF NOT EXISTS "BillingValidUntilUtc" timestamp with time zone NULL;
ALTER TABLE IF EXISTS "Tenants" ADD COLUMN IF NOT EXISTS "BillingLastUpdatedAtUtc" timestamp with time zone NULL;
UPDATE "Tenants" SET "BillingStatus" = 'PAID' WHERE COALESCE("BillingStatus",'') = '';
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
ALTER TABLE IF EXISTS "Tenants" DROP COLUMN IF EXISTS "BillingLastUpdatedAtUtc";
ALTER TABLE IF EXISTS "Tenants" DROP COLUMN IF EXISTS "BillingValidUntilUtc";
ALTER TABLE IF EXISTS "Tenants" DROP COLUMN IF EXISTS "BillingStatus";
""");
    }
}
