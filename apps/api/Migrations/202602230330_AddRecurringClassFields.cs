using Microsoft.EntityFrameworkCore.Migrations;

namespace GymForYou.Api.Migrations;

public partial class AddRecurringClassFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
ALTER TABLE IF EXISTS "GymClasses" ADD COLUMN IF NOT EXISTS "WeeklyDayOfWeek" integer NOT NULL DEFAULT 1;
ALTER TABLE IF EXISTS "GymClasses" ADD COLUMN IF NOT EXISTS "StartTimeUtc" character varying(5) NOT NULL DEFAULT '15:00';
ALTER TABLE IF EXISTS "GymClasses" ADD COLUMN IF NOT EXISTS "DurationMinutes" integer NOT NULL DEFAULT 60;
ALTER TABLE IF EXISTS "GymClasses" ADD COLUMN IF NOT EXISTS "IsActive" boolean NOT NULL DEFAULT true;
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
ALTER TABLE IF EXISTS "GymClasses" DROP COLUMN IF EXISTS "IsActive";
ALTER TABLE IF EXISTS "GymClasses" DROP COLUMN IF EXISTS "DurationMinutes";
ALTER TABLE IF EXISTS "GymClasses" DROP COLUMN IF EXISTS "StartTimeUtc";
ALTER TABLE IF EXISTS "GymClasses" DROP COLUMN IF EXISTS "WeeklyDayOfWeek";
""");
    }
}
