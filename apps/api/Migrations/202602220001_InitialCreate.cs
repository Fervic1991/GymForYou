using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymForYou.Api.Migrations;

public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS ""Tenants"" (
  ""Id"" uuid PRIMARY KEY,
  ""Name"" varchar(120) NOT NULL,
  ""Slug"" varchar(120) NOT NULL UNIQUE,
  ""LogoUrl"" text NULL,
  ""PrimaryColor"" varchar(20) NOT NULL,
  ""SecondaryColor"" varchar(20) NOT NULL,
  ""CreatedAtUtc"" timestamptz NOT NULL
);");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TABLE IF EXISTS \"Tenants\";");
    }
}
