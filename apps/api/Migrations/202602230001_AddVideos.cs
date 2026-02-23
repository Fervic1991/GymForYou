using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymForYou.Api.Migrations;

public partial class AddVideos : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS ""ExerciseVideos"" (
  ""Id"" uuid PRIMARY KEY,
  ""TenantId"" uuid NOT NULL,
  ""Title"" character varying(140) NOT NULL,
  ""Category"" character varying(80) NOT NULL,
  ""VideoUrl"" character varying(2000) NOT NULL,
  ""ThumbnailUrl"" character varying(2000) NULL,
  ""Description"" text NOT NULL,
  ""Provider"" integer NOT NULL,
  ""DurationSeconds"" integer NOT NULL,
  ""IsPublished"" boolean NOT NULL,
  ""CreatedAtUtc"" timestamp with time zone NOT NULL,
  ""UpdatedAtUtc"" timestamp with time zone NOT NULL
);

CREATE TABLE IF NOT EXISTS ""VideoProgresses"" (
  ""Id"" uuid PRIMARY KEY,
  ""TenantId"" uuid NOT NULL,
  ""VideoId"" uuid NOT NULL,
  ""MemberUserId"" uuid NOT NULL,
  ""WatchedSeconds"" integer NOT NULL,
  ""Completed"" boolean NOT NULL,
  ""LastViewedAtUtc"" timestamp with time zone NOT NULL
);

CREATE INDEX IF NOT EXISTS ""IX_ExerciseVideos_TenantId_Category"" ON ""ExerciseVideos"" (""TenantId"", ""Category"");
CREATE INDEX IF NOT EXISTS ""IX_ExerciseVideos_TenantId_IsPublished_CreatedAtUtc"" ON ""ExerciseVideos"" (""TenantId"", ""IsPublished"", ""CreatedAtUtc"");
CREATE UNIQUE INDEX IF NOT EXISTS ""IX_VideoProgresses_TenantId_VideoId_MemberUserId"" ON ""VideoProgresses"" (""TenantId"", ""VideoId"", ""MemberUserId"");
CREATE INDEX IF NOT EXISTS ""IX_VideoProgresses_TenantId_MemberUserId_LastViewedAtUtc"" ON ""VideoProgresses"" (""TenantId"", ""MemberUserId"", ""LastViewedAtUtc"");
");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
DROP TABLE IF EXISTS ""VideoProgresses"";
DROP TABLE IF EXISTS ""ExerciseVideos"";
");
    }
}
