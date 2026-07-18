# Equity Management System (EMS)

CB Bank — Investor Relations equity and shareholder lifecycle system, implemented
as an ASP.NET Core MVC application per the EMS functional specification
(shareholder onboarding & KYC, share issue/transfer/bonus/dividend, configurable
multi-level approval workflow, dashboards, reports, and administration), styled
with a CB Bank-inspired theme.

## Stack

- **ASP.NET Core 8 MVC** (Razor views, Bootstrap 5, Chart.js, Bootstrap Icons — all vendored locally, no CDN dependency)
- **Entity Framework Core** — SQL Server in production; SQLite as a zero-config local/sandbox fallback (see below)
- **ASP.NET Core Identity** for authentication, roles and lockout policy
- **ClosedXML** (Excel export) and **QuestPDF** (PDF export) for reports
- **xUnit** for domain calculation unit tests

## Solution layout

```
EMS.sln
db/
  schema.sql          Full SQL Server DDL script generated from the EF Core migration
  README.md           What it creates and how to (re)generate/apply it
src/
  EMS.Domain          Entities, enums, bonus/dividend calculation formulas (no framework dependencies)
  EMS.Application     Service interfaces, DTOs, the generic approval-workflow contract
  EMS.Infrastructure  EF Core DbContext + migrations, service implementations, Identity, DB seeding
  EMS.Web             MVC controllers, Razor views, wwwroot (CB Bank theme CSS, vendored JS libs)
tests/
  EMS.Tests           Unit tests for the bonus/dividend calculators
```

## Running locally

Requires the .NET 8 SDK.

```bash
cd src/EMS.Web
dotnet run
```

The app defaults to **SQLite** in the `Development` environment (`appsettings.Development.json`,
file `ems_dev.db`) so it runs without a SQL Server instance. On first run it creates the schema,
seeds roles, an administrator account, baseline master data, the default approval matrix, the
report catalogue, and a handful of demo shareholders/ledger entries so the dashboard isn't empty.

Sign in with the seeded administrator account:

| Username           | Password       | Role                  |
|--------------------|----------------|-----------------------|
| `admin@ems.local`  | `Passw0rd!123` | System Administrator  |

**Change this password (or disable the account) before any shared/non-local use** — it exists only
to make the freshly-seeded database usable.

### Targeting real SQL Server

Set `Database:Provider` to `SqlServer` (the default in `appsettings.json`) and point
`ConnectionStrings:SqlServer` at your instance, then apply the EF Core migration already
checked in under `src/EMS.Infrastructure/Persistence/Migrations`:

```bash
dotnet tool install --global dotnet-ef
dotnet ef database update --project src/EMS.Infrastructure --startup-project src/EMS.Infrastructure
```

If you'd rather hand a DBA a plain script instead of running `dotnet ef` against the target
server, use `db/schema.sql` (same migration, pre-rendered to idempotent T-SQL — see `db/README.md`).

Production/UAT/SIT should always run against SQL Server, per the functional spec; SQLite is a
local-development convenience only (and does not participate in the same migration history —
the SQLite path uses `EnsureCreated`, not `Migrate`).

## What's implemented

- **Shareholder Application & KYC** (personal/joint/corporate) — draft → submit → KYC decision → automatic shareholder registration with a generated `SH-YYNNNNNNN` ID.
- **Issue Shares** — capital/premium calculation, payment validation, ledger posting, certificate issuance on approval.
- **Transfer Shares** — trade and non-trade, available-balance validation, atomic debit/credit posting.
- **Bonus Shares** — configurable ratio and cash-remainder rate, calculation preview against posted holdings, batch versioning.
- **Dividend Management** — old/new share proration, settlement tracking (cash/transfer/reinvestment), outstanding balance.
- **Generic approval workflow engine** — one engine drives all four transaction types, resolving the route from a configurable, effective-dated approval matrix, snapshotting it at submission, and enforcing maker-checker (a submitter cannot approve their own item) and mandatory comments on reject/revert.
- **Dashboard** — the five baseline analytics (shareholding by group, by group %, yearly paid-up capital, yearly dividend %/provision, dividend comparison) plus pending-KYC/pending-approval/recent-transaction/data-quality-exception widgets, all derived live from posted ledger data (no separate reporting balance).
- **Reports** — all 16 cataloged reports (the 10 baseline + 6 operational/control reports), with on-screen grid, Excel export (typed cells + metadata sheet), and PDF export (branded header/footer, page numbers).
- **Administration** — Users & Roles, configurable Approval Matrix, Master Data (list-and-drawer pattern for groups/classes/branches/departments/NRC prefixes/document types/reason codes), append-only Audit Log viewer.
- **CB Bank theme** — navy/gold/white palette, header/sidebar/breadcrumb shell, status badges, approval timeline, stepper, data tables, responsive off-canvas sidebar on mobile, session-timeout warning.
- **Bilingual scaffolding** — English/Myanmar culture switch wired end-to-end (cookie-persisted `RequestLocalization`), demonstrated via `IStringLocalizer<SharedResource>` in the navigation; most in-page labels are still literal English strings (see below).

Verified end-to-end (see the smoke flow described below): application → KYC approval →
shareholder registration → issue-shares submission → maker-checker block on self-approval.

## Known simplifications / what's stubbed

This is a working foundation covering every module in the spec, not a production-hardened
build. Explicitly out of scope for this pass:

- **Document/attachment storage** — upload UI is present (drop zone) but there's no backing file
  store, malware scanning, or checksum pipeline; `Attachment` entities exist but nothing writes to them yet.
- **External integrations** — KYC/AML screening, core banking, email/SMS notifications, and
  digital signing are modeled as configuration points only (`ScreeningReference` field, etc.), not
  wired to any real provider.
- **Background jobs** — approval reminders, KYC review expiry, scheduled report generation and
  reconciliation are not implemented; reports run synchronously.
- **Fine-grained permissions** — authorization is role-based (`[Authorize(Roles = ...)]`); the
  spec's full action/data-scope permission matrix (12.2) is not built out.
- **Full bilingual coverage** — the localization *mechanism* works (culture cookie, resource
  files, `IStringLocalizer`), but only navigation labels are wired through it; most view text is
  still hard-coded English and would need `@Localizer[...]` applied throughout plus a professional
  Myanmar translation review before go-live.
- **Shareholder Application wizard** — implemented as a single sectioned page with conditional
  fields (not the 8-step wizard UI described in the spec) to keep scope manageable.
- **SQLite dev fallback quirks** — a couple of report/dashboard queries materialize rows and sum
  client-side instead of via SQL `SUM`, because SQLite's EF Core provider can't translate decimal
  aggregates; this is irrelevant once pointed at SQL Server (see `QueryExtensions.cs`).

## Tests

```bash
dotnet test tests/EMS.Tests
```

Covers the bonus-share and dividend calculation formulas against the worked examples in the
functional spec (6:1 ratio / MMK 5,000 remainder rate; 8% / MMK 10,000-per-share dividend).
