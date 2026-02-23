using Microsoft.EntityFrameworkCore;

namespace GymForYou.Api.Data;

public static class SchemaPatcher
{
    public static async Task ApplyAsync(AppDbContext db)
    {
        var sql = """
ALTER TABLE IF EXISTS "MemberProfiles" ADD COLUMN IF NOT EXISTS "CheckInCode" character varying(80) NOT NULL DEFAULT '';
ALTER TABLE IF EXISTS "MemberProfiles" ADD COLUMN IF NOT EXISTS "CheckInCodeExpiresAtUtc" timestamp with time zone NULL;
ALTER TABLE IF EXISTS "MemberProfiles" ADD COLUMN IF NOT EXISTS "BookingBlockedUntilUtc" timestamp with time zone NULL;
ALTER TABLE IF EXISTS "Bookings" ADD COLUMN IF NOT EXISTS "CanceledAtUtc" timestamp with time zone NULL;
ALTER TABLE IF EXISTS "Bookings" ADD COLUMN IF NOT EXISTS "PromotedAtUtc" timestamp with time zone NULL;
ALTER TABLE IF EXISTS "Payments" ADD COLUMN IF NOT EXISTS "Method" integer NOT NULL DEFAULT 3;
ALTER TABLE IF EXISTS "Payments" ADD COLUMN IF NOT EXISTS "Notes" character varying(200) NULL;
ALTER TABLE IF EXISTS "MemberSubscriptions" ADD COLUMN IF NOT EXISTS "IsManual" boolean NOT NULL DEFAULT false;
UPDATE "MemberProfiles" SET "CheckInCode" = substring(md5("Id"::text || clock_timestamp()::text), 1, 16) WHERE COALESCE("CheckInCode", '') = '';

CREATE TABLE IF NOT EXISTS "SessionExceptions" (
  "Id" uuid PRIMARY KEY,
  "TenantId" uuid NOT NULL,
  "SessionId" uuid NOT NULL,
  "Cancelled" boolean NOT NULL,
  "RescheduledStartAtUtc" timestamp with time zone NULL,
  "RescheduledEndAtUtc" timestamp with time zone NULL,
  "TrainerOverrideUserId" uuid NULL,
  "Reason" character varying(180) NULL,
  "CreatedAtUtc" timestamp with time zone NOT NULL
);

CREATE TABLE IF NOT EXISTS "TenantSettings" (
  "Id" uuid PRIMARY KEY,
  "TenantId" uuid NOT NULL,
  "CancelCutoffHours" integer NOT NULL,
  "MaxNoShows30d" integer NOT NULL,
  "WeeklyBookingLimit" integer NOT NULL,
  "BookingBlockDays" integer NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_TenantSettings_TenantId" ON "TenantSettings" ("TenantId");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_MemberProfiles_TenantId_CheckInCode" ON "MemberProfiles" ("TenantId", "CheckInCode");
CREATE INDEX IF NOT EXISTS "IX_Bookings_TenantId_SessionId_Status" ON "Bookings" ("TenantId", "SessionId", "Status");
CREATE INDEX IF NOT EXISTS "IX_Bookings_TenantId_MemberUserId_Status_CreatedAtUtc" ON "Bookings" ("TenantId", "MemberUserId", "Status", "CreatedAtUtc");
CREATE INDEX IF NOT EXISTS "IX_ClassSessions_TenantId_StartAtUtc" ON "ClassSessions" ("TenantId", "StartAtUtc");
ALTER TABLE IF EXISTS "GymClasses" ADD COLUMN IF NOT EXISTS "WeeklyDayOfWeek" integer NOT NULL DEFAULT 1;
ALTER TABLE IF EXISTS "GymClasses" ADD COLUMN IF NOT EXISTS "StartTimeUtc" character varying(5) NOT NULL DEFAULT '15:00';
ALTER TABLE IF EXISTS "GymClasses" ADD COLUMN IF NOT EXISTS "DurationMinutes" integer NOT NULL DEFAULT 60;
ALTER TABLE IF EXISTS "GymClasses" ADD COLUMN IF NOT EXISTS "IsActive" boolean NOT NULL DEFAULT true;
ALTER TABLE IF EXISTS "WebhookEventLogs" ADD COLUMN IF NOT EXISTS "StripeEventId" character varying(120);
ALTER TABLE IF EXISTS "WebhookEventLogs" ADD COLUMN IF NOT EXISTS "Outcome" character varying(120) NOT NULL DEFAULT 'processed';
UPDATE "WebhookEventLogs" SET "StripeEventId" = COALESCE(NULLIF("StripeEventId", ''), "EventId") WHERE COALESCE("StripeEventId", '') = '' AND COALESCE("EventId", '') <> '';
DELETE FROM "WebhookEventLogs" a
USING "WebhookEventLogs" b
WHERE a."Id" > b."Id"
  AND a."Provider" = b."Provider"
  AND COALESCE(a."StripeEventId", a."EventId") = COALESCE(b."StripeEventId", b."EventId");
UPDATE "WebhookEventLogs" SET "StripeEventId" = 'unknown_' || "Id"::text WHERE COALESCE("StripeEventId", '') = '';
ALTER TABLE IF EXISTS "WebhookEventLogs" ALTER COLUMN "StripeEventId" SET NOT NULL;
CREATE UNIQUE INDEX IF NOT EXISTS "IX_WebhookEventLogs_Provider_StripeEventId" ON "WebhookEventLogs" ("Provider", "StripeEventId");
CREATE INDEX IF NOT EXISTS "IX_MemberSubscriptions_TenantId_StripeSubscriptionId" ON "MemberSubscriptions" ("TenantId", "StripeSubscriptionId");
DELETE FROM "Payments" a
USING "Payments" b
WHERE a."Id" > b."Id"
  AND COALESCE(a."StripeInvoiceId", '') <> ''
  AND a."StripeInvoiceId" = b."StripeInvoiceId";
DELETE FROM "Payments" a
USING "Payments" b
WHERE a."Id" > b."Id"
  AND COALESCE(a."StripePaymentIntentId", '') <> ''
  AND a."StripePaymentIntentId" = b."StripePaymentIntentId";
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Payments_StripeInvoiceId" ON "Payments" ("StripeInvoiceId");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Payments_StripePaymentIntentId" ON "Payments" ("StripePaymentIntentId");
ALTER TABLE IF EXISTS "Tenants" ADD COLUMN IF NOT EXISTS "DefaultLocale" character varying(2);
UPDATE "Tenants" SET "DefaultLocale" = 'it' WHERE COALESCE("DefaultLocale", '') = '';
ALTER TABLE IF EXISTS "Tenants" ALTER COLUMN "DefaultLocale" SET DEFAULT 'it';
ALTER TABLE IF EXISTS "Tenants" ALTER COLUMN "DefaultLocale" SET NOT NULL;
ALTER TABLE IF EXISTS "Tenants" ADD COLUMN IF NOT EXISTS "JoinCode" character varying(24);
ALTER TABLE IF EXISTS "Tenants" ADD COLUMN IF NOT EXISTS "City" character varying(120);
ALTER TABLE IF EXISTS "Tenants" ADD COLUMN IF NOT EXISTS "Address" character varying(200);
ALTER TABLE IF EXISTS "Tenants" ADD COLUMN IF NOT EXISTS "Phone" character varying(30);
ALTER TABLE IF EXISTS "Tenants" ADD COLUMN IF NOT EXISTS "IsSuspended" boolean NOT NULL DEFAULT false;
ALTER TABLE IF EXISTS "Tenants" ADD COLUMN IF NOT EXISTS "BillingStatus" character varying(20) NOT NULL DEFAULT 'PAID';
ALTER TABLE IF EXISTS "Tenants" ADD COLUMN IF NOT EXISTS "BillingValidUntilUtc" timestamp with time zone NULL;
ALTER TABLE IF EXISTS "Tenants" ADD COLUMN IF NOT EXISTS "BillingLastUpdatedAtUtc" timestamp with time zone NULL;
UPDATE "Tenants" SET "JoinCode" = UPPER(substring(md5("Id"::text || clock_timestamp()::text), 1, 8)) WHERE COALESCE("JoinCode", '') = '';
UPDATE "Tenants" SET "BillingStatus" = 'PAID' WHERE COALESCE("BillingStatus",'') = '';
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Tenants_JoinCode" ON "Tenants" ("JoinCode");

CREATE TABLE IF NOT EXISTS "TenantJoinLinks" (
  "Id" uuid PRIMARY KEY,
  "TenantId" uuid NOT NULL,
  "Code" character varying(40) NOT NULL,
  "IsActive" boolean NOT NULL,
  "ExpiresAtUtc" timestamp with time zone NULL,
  "MaxUses" integer NULL,
  "UsesCount" integer NOT NULL,
  "CreatedAtUtc" timestamp with time zone NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_TenantJoinLinks_Code" ON "TenantJoinLinks" ("Code");
CREATE INDEX IF NOT EXISTS "IX_TenantJoinLinks_TenantId_IsActive_CreatedAtUtc" ON "TenantJoinLinks" ("TenantId", "IsActive", "CreatedAtUtc");

CREATE TABLE IF NOT EXISTS "ExerciseVideos" (
  "Id" uuid PRIMARY KEY,
  "TenantId" uuid NOT NULL,
  "Title" character varying(140) NOT NULL,
  "Category" character varying(80) NOT NULL,
  "VideoUrl" character varying(2000) NOT NULL,
  "ThumbnailUrl" character varying(2000) NULL,
  "Description" text NOT NULL,
  "Provider" integer NOT NULL,
  "DurationSeconds" integer NOT NULL,
  "IsPublished" boolean NOT NULL,
  "CreatedAtUtc" timestamp with time zone NOT NULL,
  "UpdatedAtUtc" timestamp with time zone NOT NULL
);

CREATE TABLE IF NOT EXISTS "VideoProgresses" (
  "Id" uuid PRIMARY KEY,
  "TenantId" uuid NOT NULL,
  "VideoId" uuid NOT NULL,
  "MemberUserId" uuid NOT NULL,
  "WatchedSeconds" integer NOT NULL,
  "Completed" boolean NOT NULL,
  "LastViewedAtUtc" timestamp with time zone NOT NULL
);

CREATE INDEX IF NOT EXISTS "IX_ExerciseVideos_TenantId_Category" ON "ExerciseVideos" ("TenantId", "Category");
CREATE INDEX IF NOT EXISTS "IX_ExerciseVideos_TenantId_IsPublished_CreatedAtUtc" ON "ExerciseVideos" ("TenantId", "IsPublished", "CreatedAtUtc");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_VideoProgresses_TenantId_VideoId_MemberUserId" ON "VideoProgresses" ("TenantId", "VideoId", "MemberUserId");
CREATE INDEX IF NOT EXISTS "IX_VideoProgresses_TenantId_MemberUserId_LastViewedAtUtc" ON "VideoProgresses" ("TenantId", "MemberUserId", "LastViewedAtUtc");
""";

        await db.Database.ExecuteSqlRawAsync(sql);
    }
}
