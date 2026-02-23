# GymForYou MVP (Gym SaaS)

MVP multi-tenant per gestione palestre + area membro, esteso con feature sales-critical.

## Stack
- Frontend: Next.js App Router + TypeScript + Tailwind (`/apps/web`)
- Backend: ASP.NET Core Web API .NET 8 + EF Core (`/apps/api`)
- DB: PostgreSQL
- Auth: JWT + refresh token
- Payments: Stripe Checkout + webhook + pagamenti manuali
- Infra: Docker Compose (`web`, `api`, `db`, `pgadmin`)

## Funzionalità principali
- Multi-tenant con isolamento query/write per `TenantId`
- Onboarding membri automatico per tenant via `slug` o `joinCode` (no tenantId manuale)
- Staff + membri + classi + booking/waitlist
- Check-in QR: generazione token, scanner web staff, fallback codice
- Regole check-in: membro sospeso o subscription non `ACTIVE` => rifiutato
- Subscription manuali + rinnovi manuali con scadenza
- Pagamenti manuali con `PaymentMethod`: `CASH`, `BANK_TRANSFER`, `POS`, `STRIPE`
- Calendario settimanale + `SessionException` (cancel/reschedule/trainer override)
- Policy no-show/late-cancel con blocco prenotazioni automatico
- Auto-promozione waitlist FIFO quando un BOOKED libera posto
- Export CSV pagamenti e report summary (churn/frequenza/top classi)
- Webhook Stripe robusto: idempotenza evento, metadata guard, sync stati subscription

## Entità aggiunte (sales-critical)
- `TenantSettings`: `CancelCutoffHours`, `MaxNoShows30d`, `WeeklyBookingLimit`, `BookingBlockDays`
- `SessionException`: eccezioni su singola sessione
- `MemberProfile`: `CheckInCode`, `CheckInCodeExpiresAtUtc`, `BookingBlockedUntilUtc`
- `Payment`: `Method`, `Notes`
- `MemberSubscription`: `IsManual`
- `BookingStatus`: include `LATE_CANCEL`

## Endpoint REST (nuovi/estesi)

### Check-in QR
- `POST /members/{memberUserId}/checkin-qr`
- `POST /members/checkin/qr`
- `GET /members/{memberUserId}/checkins`

### Tenant policy
- `GET /tenant/settings`
- `PUT /tenant/settings`

### Member onboarding via join link (public)
- `GET /join/{code}`
- `POST /auth/register-member` body:
  - `{ joinCode, fullName, email, phone, password }`
  - crea membro nel tenant risolto e ritorna `AuthResponse` (JWT + refresh)

### Locale tenant (i18n)
- `GET /tenant/settings` include `defaultLocale` (`it|es`)
- `PATCH /platform/tenants/{tenantId}/locale` (Platform super admin)
  - body: `{ "defaultLocale": "it" | "es" }`
  - response: `{ tenantId, defaultLocale }`

### Platform super admin
- `POST /auth/platform/login`
- `GET /platform/tenants`
- `GET /platform/tenants/overview` (statistiche globali cross-tenant)
- `GET /platform/tenants/{tenantId}`
- `GET /platform/tenants/{tenantId}/members`
- `GET /platform/tenants/{tenantId}/classes`
- `POST /platform/tenants`
- `PATCH /platform/tenants/{tenantId}/billing`

### Booking policy / status
- `GET /bookings/me?from=&to=`
- `PATCH /bookings/{bookingId}/status` (`NO_SHOW`, `LATE_CANCEL`, ...)
- `PATCH /bookings/{bookingId}/cancel` (auto late-cancel secondo cutoff)

### Calendario eccezioni
- `POST /classes/sessions/exceptions`
- `GET /classes/sessions?weekStart=YYYY-MM-DD`

### Billing manuale / export
- `POST /billing/payments/manual`
- `POST /billing/subscriptions/manual`
- `GET /billing/payments/export?from=&to=&method=`
- `GET /billing/me/subscriptions`
- `GET /billing/me/payments`

### Report
- `GET /reports/summary`

### Owner tenant directory
- `GET /tenant/profile` (OWNER/MANAGER)
- `GET /tenant/join-link` (OWNER/MANAGER)
- `POST /tenant/join-link/rotate` (OWNER/MANAGER)

## Web pages
Staff:
- `/login`
- `/dashboard`
- `/members`
- `/classes` (calendario + eccezioni)
- `/bookings` (status no-show/late-cancel)
- `/checkin` (scanner/fallback codice)
- `/billing` (manual payments/subscriptions + export)
- `/reports`
- `/videos` (CRUD video training)
- `/platform/login` (login super admin)
- `/platform/tenants` (Platform Console: directory + overview + billing/scadenze tenant)
- `/platform/tenants/[tenantId]` (detail tenant: tab staff cross-tenant + sospensione tenant)
- `/tenant` (solo OWNER: link iscrizione, QR, joinCode)

Area membro:
- `/app`
- `/app/calendar` (calendario membro separato, settimanale, stato/CTA booking)
- `/app/schedule`
- `/app/my-bookings` (lista prenotazioni future + cancel)
- `/app/subscription`
- `/app/videos` (catalogo video + progress tracking)
- `/join/[code]` (registrazione membro via join link/QR)

## Environment variables
Copia `.env.example` in `.env`.

Obbligatorie:
- DB: `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD`, `POSTGRES_PORT`
- JWT: `JWT_SECRET`, `JWT_ISSUER`, `JWT_AUDIENCE`
- Platform admin: `PLATFORM_ADMIN_EMAIL`, `PLATFORM_ADMIN_PASSWORD` (`PLATFORM_ADMIN_KEY` resta fallback legacy)
- Stripe: `STRIPE_SECRET_KEY`, `STRIPE_WEBHOOK_SECRET`
- URL/porte: `WEB_BASE_URL`, `API_BASE_URL`, `NEXT_PUBLIC_API_URL`, `WEB_PORT`, `API_PORT`
- pgAdmin: `PGADMIN_EMAIL`, `PGADMIN_PASSWORD`
- Job reminder: `RENEWAL_REMINDER_RUN_HOUR_UTC` (opzionale, default `7`)

## Avvio locale (Docker)
```bash
cp .env.example .env
docker compose up --build
```

Servizi default:
- Web: http://localhost:13000
- API + Swagger: http://localhost:18081/swagger
- PostgreSQL: localhost:15432
- pgAdmin: http://localhost:5050

## Seed iniziale
- 1 tenant demo (`demo-gym`) con `JoinCode=DEMO123`
- 1 owner (`owner@gym.local` / `Owner123!`)
- 1 trainer
- 5 membri (`member1..5@gym.local` / `Member123!`)
- 3 corsi
- 2 piani
- subscription attiva seed per i membri demo
- tenant settings default policy
- tenant demo con `DefaultLocale = "it"`

Credenziali demo:
- Staff owner: `owner@gym.local` / `Owner123!`
- Member demo: `member1@gym.local` / `Member123!`
- Super admin platform: `superadmin@gym.local` / `SuperAdmin123!`

## SaaS foundation (scenario B minimal)
- Sospensione tenant:
  - `PATCH /platform/tenants/{tenantId}/suspension` body `{ "isSuspended": true|false }`
  - tenant sospeso blocca `POST /auth/login`, `POST /auth/register-member`, `POST /billing/checkout` con `403 Tenant suspended`
  - non blocca `/stripe/*` e `/platform/*`
- Staff management cross-tenant (Platform only):
  - `GET /platform/tenants/{tenantId}/staff`
  - `PATCH /platform/tenants/{tenantId}/staff/{userId}/role`
  - `PATCH /platform/tenants/{tenantId}/staff/{userId}/disable`
  - guard: un solo `OWNER` attivo per tenant
- Renewal reminders:
  - `POST /notifications/renewal-reminders` (OWNER/MANAGER)
  - job giornaliero automatico su tutte le palestre non sospese
  - deduplica nello stesso giorno per la stessa subscription
  - dashboard include KPI `expiringMembers` + badge scadenze

## Auto-onboarding membri (join link/QR)
- Link palestra (esempio): `https://tuodominio/join/DEMO123`
- QR code generato in area owner/manager (`/tenant`)
- Il membro apre il link, completa registrazione e viene associato automaticamente al tenant corretto.

Nota email:
- Unicità email è per tenant (stessa email ammessa su tenant diversi, non sullo stesso tenant).

## Stripe webhook (test)
1. Imposta `STRIPE_SECRET_KEY` in `.env`
2. Avvia stack
3. In altro terminale:
```bash
stripe listen --forward-to localhost:18081/stripe/webhook
```
4. Copia `whsec_...` in `STRIPE_WEBHOOK_SECRET`
5. Riavvia API
6. Trigger scenari:
```bash
stripe trigger checkout.session.completed
stripe trigger invoice.payment_succeeded
stripe trigger invoice.payment_failed
stripe trigger customer.subscription.deleted
```

Policy webhook:
- Idempotenza: `WebhookEventLogs` con unique su `(Provider, StripeEventId)`; eventi duplicati rispondono `200` senza riprocessare.
- Metadata checkout obbligatori: `TenantId`, `MemberId`, `PlanId`; se mancanti o tenant mismatch, evento loggato e ignorato.
- Eventi gestiti:
  - `checkout.session.completed` -> crea/aggiorna `MemberSubscriptions` in `ACTIVE`
  - `invoice.payment_succeeded` -> `MemberSubscriptions` in `ACTIVE` + `Payments` `paid`
  - `invoice.payment_failed` -> `MemberSubscriptions` in `PAST_DUE` + `Payments` `failed`
  - `customer.subscription.deleted` -> `MemberSubscriptions` in `CANCELED`

## Test unitari
```bash
docker run --rm -v "$PWD":/src -w /src mcr.microsoft.com/dotnet/sdk:8.0 dotnet test apps/api-tests/GymForYou.Api.Tests.csproj
```

Coperti:
- capacità/waitlist booking
- auto-promozione waitlist (1 posto, 2 posti, waitlist vuota, no over-capacity)
- enforcement blocco prenotazioni (no-show/late-cancel)
- tenant isolation guard
- check-in rules (subscription/stato membro)
- verifica firma webhook Stripe
- idempotenza webhook Stripe (duplicate event non duplica subscription/payment)
- member booking cancel authorization (own booking / other user / other tenant)

## Journey membro: cancellazione prenotazione
1. Login su `/app` con credenziali membro demo.
2. Vai su `/app/schedule` e crea una prenotazione.
3. Vai su `/app/my-bookings` e clicca `Cancella`.
4. Il backend chiama `PATCH /bookings/{id}/cancel`.
5. Se oltre cutoff policy, stato booking diventa `LATE_CANCEL`; altrimenti `CANCELED`.
6. Viene scritto `NotificationLogs` con tipo `BookingCanceled`.

## Lingua tenant (i18n) - verifica
1. Apri `/platform/login` e fai login come super admin.
2. Apri `/platform/tenants`.
3. In `Tenant Detail`, imposta `Lingua palestra` su `Italiano` o `Español` e salva.
4. Login staff/member del tenant: l'app legge `/tenant/settings` all'avvio e applica la lingua UI del tenant automaticamente.
5. Staff/member non hanno switch lingua: la lingua segue sempre `DefaultLocale` del tenant.

## Migrazioni
- Migrazioni disponibili in `apps/api/Migrations`
- In runtime viene applicato anche uno schema patcher non distruttivo (`SchemaPatcher`) per ambienti già avviati.
