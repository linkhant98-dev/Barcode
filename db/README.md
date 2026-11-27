# Database schema

`schema.sql` is the full SQL Server DDL script for the EMS database, generated directly
from the EF Core migration in `src/EMS.Infrastructure/Persistence/Migrations`
(`dotnet ef migrations script --idempotent`). It is idempotent — safe to run against an
empty database or re-run against one that already has some/all of the migration applied;
it checks `__EFMigrationsHistory` and only creates what's missing.

## Applying it

```bash
sqlcmd -S <server> -d EMS -i db/schema.sql
```

or open it in SSMS / Azure Data Studio and execute against your target database.

To regenerate after a model change (new migration), from the repo root:

```bash
dotnet tool install --global dotnet-ef   # if not already installed
dotnet ef migrations script --idempotent \
  --project src/EMS.Infrastructure --startup-project src/EMS.Infrastructure \
  -o db/schema.sql
```

## What it creates

- ASP.NET Core Identity tables (`AspNetUsers`, `AspNetRoles`, etc.)
- Shareholder aggregate: `Shareholders`, `Persons`, `Corporates`, `CorporateSignatories`,
  `BeneficialOwners`, `JointHolders`, `Addresses`, `Contacts`, `BankAccounts`,
  `RelationshipDeclarations`
- Applications/KYC: `ShareholderApplications`, `ApplicationJointHolders`, `KycCases`
- Master data: `NrcPrefixes`, `Geographies`, `BankBranches`, `Departments`,
  `ShareholderGroups`, `ShareClasses`, `DocumentTypes`, `ReasonCodes`,
  `DividendParameters`, `BonusParameters`, `NumberSequences`
- Shares: `ShareTransactions`, `ShareIssues`, `ShareTransfers`, `ShareLedgerEntries`,
  `ShareCertificates`
- Corporate actions: `BonusEvents`, `BonusEntitlements`, `DividendEvents`,
  `DividendEntitlements`, `DividendSettlements`
- Workflow: `ApprovalMatrixRules`, `ApprovalInstances`, `ApprovalSteps`, `Delegations`
- Documents/audit/notifications: `Attachments`, `AuditLogEntries`, `Notifications`
- Reporting: `ReportDefinitions`, `ReportExecutions`, `DashboardSnapshots`,
  `ReconciliationResults`

Money columns are `decimal(19,4)`; share-quantity columns that need fractional precision
(bonus/dividend proration) are `decimal(19,6)`; mutable aggregate roots carry a
`rowversion` column for optimistic concurrency; master-data tables have a filtered unique
index on `Code` (active rows only), per the functional spec's DB standards (section 16.2).

## Seed data

There is **no separate seed SQL script** — seeding (roles, the initial administrator,
baseline master data, the default approval matrix, the report catalogue, and demo
shareholders/ledger entries) is done in application code
(`src/EMS.Infrastructure/Seed/DbSeeder.cs`) and runs automatically the first time the app
starts against an empty database. This is deliberate: the administrator account's password
goes through ASP.NET Core Identity's `PasswordHasher` (PBKDF2 with a random salt), which
isn't something to fake in a static SQL script. If you need to seed a database without
running the app, call `DbSeeder.SeedAsync(serviceProvider)` from a small console/host
context, or extract the master-data/approval-matrix portions into your own script using
`schema.sql`'s table shapes as reference.
