# Equity Management System

CB Bank PCL — Equity & Shareholder Certificate Management.

A full-stack implementation of the system flow: **Share Purchase → Certificate
Creation → Certificate Management → Certificate Printing (with barcode) →
Reporting & Inquiry**, with role-based access matching the four user roles
(Equity Officer, Approver/Manager, System Administrator, Inquiry User).

## Stack

- **Backend**: Node.js, Express, TypeScript, Prisma ORM, SQLite, JWT auth, `bwip-js` for Code128 barcode generation.
- **Frontend**: React, TypeScript, Vite, React Router, Axios.

## Project layout

```
backend/    Express API, Prisma schema, seed script
frontend/   React single-page app
```

## Running locally

### Backend

```bash
cd backend
npm install
cp .env.example .env        # adjust JWT_SECRET for anything beyond local dev
npx prisma migrate deploy   # creates prisma/dev.db and applies the schema
npm run seed                # creates demo users
npm run dev                 # starts the API on http://localhost:4000
```

### Frontend

```bash
cd frontend
npm install
npm run dev                 # starts the app on http://localhost:5173 (proxies /api to :4000)
```

Open http://localhost:5173 and sign in with one of the seeded demo accounts
(password `password123` for all):

| Email                          | Role            |
|---------------------------------|-----------------|
| equity.officer@cbbank.test      | Equity Officer  |
| approver@cbbank.test            | Approver (Manager) |
| admin@cbbank.test               | System Administrator |
| inquiry@cbbank.test             | Inquiry User (read-only) |

## Workflow covered

1. **Share Purchase** — Equity Officer records a shareholder's purchase
   request (paper/online) and verifies KYC/payment/documents.
2. **Certificate Creation** — Equity Officer creates a certificate request
   from a verified transaction; the Approver reviews and approves/rejects it,
   which issues the certificate (with a generated certificate number and
   barcode value).
3. **Certificate Management** — Search/view shareholder & certificate data;
   update, cancel, replace, or reissue certificates, with full status history.
4. **Certificate Printing** — Equity Officer requests printing; Approver
   approves; the system renders a certificate preview with a real Code128
   barcode; printing is recorded (who/when) as the print history.
5. **Reporting & Inquiry** — Certificate register, shareholder list, print
   history, and the full activity/audit log.

All mutating actions are role-gated server-side (not just hidden in the UI)
and written to an `ActivityLog` for audit/compliance.

## Notes

- SQLite is used for zero-config local development; Prisma's schema can be
  repointed at Postgres/MySQL by changing the `datasource` provider — since
  SQLite has no native enum type, status/role fields are plain strings
  validated at the application layer (`backend/src/utils/enums.ts`) rather
  than Prisma enums, so no schema changes are needed for either engine.
- `npm audit` flags a moderate/high advisory in `esbuild`/`vite`'s dev
  server (arbitrary origins can query the dev server) — it only affects
  `npm run dev`, not production builds, and fixing it requires a breaking
  Vite major-version bump.
