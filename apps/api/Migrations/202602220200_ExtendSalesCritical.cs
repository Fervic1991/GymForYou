using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymForYou.Api.Migrations;

public partial class ExtendSalesCritical : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
ALTER TABLE IF EXISTS ""MemberProfiles"" ADD COLUMN IF NOT EXISTS ""CheckInCode"" character varying(80) NOT NULL DEFAULT '';
ALTER TABLE IF EXISTS ""MemberProfiles"" ADD COLUMN IF NOT EXISTS ""CheckInCodeExpiresAtUtc"" timestamp with time zone NULL;
ALTER TABLE IF EXISTS ""MemberProfiles"" ADD COLUMN IF NOT EXISTS ""BookingBlockedUntilUtc"" timestamp with time zone NULL;
ALTER TABLE IF EXISTS ""Bookings"" ADD COLUMN IF NOT EXISTS ""CanceledAtUtc"" timestamp with time zone NULL;
ALTER TABLE IF EXISTS ""Bookings"" ADD COLUMN IF NOT EXISTS ""PromotedAtUtc"" timestamp with time zone NULL;
ALTER TABLE IF EXISTS ""Payments"" ADD COLUMN IF NOT EXISTS ""Method"" integer NOT NULL DEFAULT 3;
ALTER TABLE IF EXISTS ""Payments"" ADD COLUMN IF NOT EXISTS ""Notes"" character varying(200) NULL;
ALTER TABLE IF EXISTS ""MemberSubscriptions"" ADD COLUMN IF NOT EXISTS ""IsManual"" boolean NOT NULL DEFAULT false;
UPDATE ""MemberProfiles"" SET ""CheckInCode"" = substring(md5(""Id""::text || clock_timestamp()::text), 1, 16) WHERE COALESCE(""CheckInCode"", '') = '';

CREATE TABLE IF NOT EXISTS ""SessionExceptions"" (
  ""Id"" uuid PRIMARY KEY,
  ""TenantId"" uuid NOT NULL,
  ""SessionId"" uuid NOT NULL,
  ""Cancelled"" boolean NOT NULL,
  ""RescheduledStartAtUtc"" timestamp with time zone NULL,
  ""RescheduledEndAtUtc"" timestamp with time zone NULL,
  ""TrainerOverrideUserId"" uuid NULL,
  ""Reason"" character varying(180) NULL,
  ""CreatedAtUtc"" timestamp with time zone NOT NULL
);

CREATE TABLE IF NOT EXISTS ""TenantSettings"" (
  ""Id"" uuid PRIMARY KEY,
  ""TenantId"" uuid NOT NULL,
  ""CancelCutoffHours"" integer NOT NULL,
  ""MaxNoShows30d"" integer NOT NULL,
  ""WeeklyBookingLimit"" integer NOT NULL,
  ""BookingBlockDays"" integer NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS ""IX_TenantSettings_TenantId"" ON ""TenantSettings"" (""TenantId"");
CREATE UNIQUE INDEX IF NOT EXISTS ""IX_MemberProfiles_TenantId_CheckInCode"" ON ""MemberProfiles"" (""TenantId"", ""CheckInCode"");
CREATE INDEX IF NOT EXISTS ""IX_Bookings_TenantId_SessionId_Status"" ON ""Bookings"" (""TenantId"", ""SessionId"", ""Status"");
CREATE INDEX IF NOT EXISTS ""IX_Bookings_TenantId_MemberUserId_Status_CreatedAtUtc"" ON ""Bookings"" (""TenantId"", ""MemberUserId"", ""Status"", ""CreatedAtUtc"");
CREATE INDEX IF NOT EXISTS ""IX_ClassSessions_TenantId_StartAtUtc"" ON ""ClassSessions"" (""TenantId"", ""StartAtUtc"");
ALTER TABLE IF EXISTS ""WebhookEventLogs"" ADD COLUMN IF NOT EXISTS ""StripeEventId"" character varying(120);
ALTER TABLE IF EXISTS ""WebhookEventLogs"" ADD COLUMN IF NOT EXISTS ""Outcome"" character varying(120) NOT NULL DEFAULT 'processed';
UPDATE ""WebhookEventLogs"" SET ""StripeEventId"" = COALESCE(NULLIF(""StripeEventId"", ''), ""EventId"") WHERE COALESCE(""StripeEventId"", '') = '' AND COALESCE(""EventId"", '') <> '';
DELETE FROM ""WebhookEventLogs"" a
USING ""WebhookEventLogs"" b
WHERE a.""Id"" > b.""Id""
  AND a.""Provider"" = b.""Provider""
  AND COALESCE(a.""StripeEventId"", a.""EventId"") = COALESCE(b.""StripeEventId"", b.""EventId"");
UPDATE ""WebhookEventLogs"" SET ""StripeEventId"" = 'unknown_' || ""Id""::text WHERE COALESCE(""StripeEventId"", '') = '';
ALTER TABLE IF EXISTS ""WebhookEventLogs"" ALTER COLUMN ""StripeEventId"" SET NOT NULL;
CREATE UNIQUE INDEX IF NOT EXISTS ""IX_WebhookEventLogs_Provider_StripeEventId"" ON ""WebhookEventLogs"" (""Provider"", ""StripeEventId"");
CREATE INDEX IF NOT EXISTS ""IX_MemberSubscriptions_TenantId_StripeSubscriptionId"" ON ""MemberSubscriptions"" (""TenantId"", ""StripeSubscriptionId"");
DELETE FROM ""Payments"" a
USING ""Payments"" b
WHERE a.""Id"" > b.""Id""
  AND COALESCE(a.""StripeInvoiceId"", '') <> ''
  AND a.""StripeInvoiceId"" = b.""StripeInvoiceId"";
DELETE FROM ""Payments"" a
USING ""Payments"" b
WHERE a.""Id"" > b.""Id""
  AND COALESCE(a.""StripePaymentIntentId"", '') <> ''
  AND a.""StripePaymentIntentId"" = b.""StripePaymentIntentId"";
CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Payments_StripeInvoiceId"" ON ""Payments"" (""StripeInvoiceId"");
CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Payments_StripePaymentIntentId"" ON ""Payments"" (""StripePaymentIntentId"");
");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
DROP TABLE IF EXISTS ""SessionExceptions"";
DROP TABLE IF EXISTS ""TenantSettings"";
");
    }
}
