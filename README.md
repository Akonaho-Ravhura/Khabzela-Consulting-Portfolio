# Khabzela Consulting

Website and client app for Khabzela Consulting, a Centurion-based accounting firm (accounting, payroll and tax).

- **Public website**: home, offering, about, contact (the contact form emails the owner).
- **Client app** (mobile-first, installable as a PWA on phones and desktops): combined *get a quote + book an appointment* flow, tax-status and deadline notifications, invoices, profile and notification preferences.
- **Admin portal** (staff/owner, "Go to the admin portal" on the sign-in page): dashboard, clients with tax-status updates (each update notifies the client), pricing rules that drive the quote calculator, quote requests and invoicing.
- **Automatic SARS deadline reminders**: each client has a tax profile (employer, VAT category, provisional taxpayer, company, year-end). The app works out their EMP201, EMP501, VAT201, IRP6 and ITR14 due dates (moved to the previous business day for weekends and public holidays) and reminds the client 7 days before each one. Staff see all deadlines for the next 14 days on the dashboard. No SARS login or client tax data is needed.

## Tech stack

ASP.NET Core MVC (.NET 10), Entity Framework Core, ASP.NET Core Identity, Azure SQL Database (SQLite for local development), MailKit (SMTP), xUnit, and GitHub Actions.

## Project structure

```
KhabzelaConsulting/            Web app (MVC, layered)
  Controllers/                 Home (public), Account (auth), Client (client app), Admin (staff portal)
  Models/                      Entities (domain model / ER diagram), form inputs, page models
  Data/                        AppDbContext, EF Core migrations, seeding
  Services/                    PricingCalculator, NotificationService, email, SA time & tax deadlines
  ViewComponents/              Layout badges (unread notifications, new quote requests)
  Views/, wwwroot/             Razor views, per-page CSS/JS, PWA manifest + service worker
KhabzelaConsulting.Tests/      Unit + integration tests (xUnit, WebApplicationFactory)
.github/workflows/ci.yml       Build and test on every push / pull request
```

## Run locally

```bash
cd KhabzelaConsulting
dotnet run --launch-profile http     # http://localhost:5078
```

Development uses a local SQLite file (`khabzela-dev.db`) and seeds demo data:

| Role   | Email                        | Password    |
|--------|------------------------------|-------------|
| Admin  | admin@demo.khabzela.co.za    | `Demo@2026` |
| Client | client@demo.khabzela.co.za   | `Demo@2026` |

Run the tests with `dotnet test KhabzelaConsulting/KhabzelaConsulting.slnx`.

## Production configuration

Never commit secrets. Set these as environment variables on the server (or with `dotnet user-secrets` locally):

| Setting | Purpose |
|---|---|
| `ConnectionStrings__DefaultConnection` | Azure SQL connection string. Migrations run automatically on start-up. |
| `Seed__AdminPassword` | Creates the owner's admin login (`vukani@khabzelaconsulting.co.za`) on first start. |
| `Seed__DemoData` / `Seed__DemoPassword` | Optional demo accounts for markers (`true` / a password). |
| `Smtp__Host`, `Smtp__Port`, `Smtp__Username`, `Smtp__Password` | Mailbox used to send contact-form messages, password resets and notification emails. |

## Security

- Passwords are hashed by ASP.NET Core Identity. Accounts lock for 15 minutes after 5 failed sign-ins, and authenticator-app two-factor authentication is optional.
- Access is role-based (`Admin` / `Client`), and every client query is scoped to the signed-in client.
- Anti-forgery tokens are required on every POST. EF Core uses parameterised queries, which prevents SQL injection.
- Login, registration, password-reset and contact forms are rate-limited. The contact form also has a spam honeypot.
- Security headers are set, and HTTPS/HSTS is enforced in production.
- No client documents are stored (a POPIA scope decision from the client meeting).
