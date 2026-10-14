using KhabzelaConsulting.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace KhabzelaConsulting.Data
{
    public class SeedOptions
    {
        // The owner's admin login. Created only when a password is configured (user secrets / env vars).
        public string AdminEmail { get; set; } = "vukani@khabzelaconsulting.co.za";
        public string AdminName { get; set; } = "Vukani";
        public string? AdminPassword { get; set; }

        // Demo accounts + sample data so the lecturer can test the client and admin portals.
        public bool DemoData { get; set; }
        public string? DemoPassword { get; set; }
    }

    public static class DbSeeder
    {
        public const string DemoAdminEmail = "admin@demo.khabzela.co.za";
        public const string DemoClientEmail = "client@demo.khabzela.co.za";

        public static async Task SeedAsync(IServiceProvider services, SeedOptions options, bool useMigrations, ILogger logger)
        {
            var db = services.GetRequiredService<AppDbContext>();
            if (useMigrations) await db.Database.MigrateAsync();
            else await db.Database.EnsureCreatedAsync();

            var roles = services.GetRequiredService<RoleManager<IdentityRole>>();
            foreach (var role in new[] { Roles.Admin, Roles.Client })
            {
                if (!await roles.RoleExistsAsync(role)) await roles.CreateAsync(new IdentityRole(role));
            }

            await SeedServicesAndRulesAsync(db);

            var users = services.GetRequiredService<UserManager<ApplicationUser>>();
            if (!string.IsNullOrWhiteSpace(options.AdminPassword))
                await EnsureUserAsync(users, options.AdminEmail, options.AdminName, options.AdminPassword, Roles.Admin, "Accounting, Payroll & Tax", logger);
            else
                logger.LogWarning("Seed:AdminPassword not set — owner admin account not created.");

            if (options.DemoData)
            {
                if (string.IsNullOrWhiteSpace(options.DemoPassword))
                    logger.LogWarning("Seed:DemoData is on but Seed:DemoPassword is empty — demo data skipped.");
                else
                    await SeedDemoAsync(db, users, options.DemoPassword, logger);
            }
        }

        private static async Task SeedServicesAndRulesAsync(AppDbContext db)
        {
            if (await db.Services.AnyAsync()) return;

            var accounting = new Service { Key = "accounting", Name = "Accounting", SortOrder = 1, Description = "Monthly management accounts and annual financial statements." };
            var payroll = new Service { Key = "payroll", Name = "Payroll", SortOrder = 2, Description = "Weekly and monthly payroll, payslips, UIF declarations and IRP5s." };
            var tax = new Service { Key = "tax", Name = "Tax", SortOrder = 3, Description = "PAYE and VAT returns, provisional tax (IRP6) and income tax returns (IT14)." };
            db.Services.AddRange(accounting, payroll, tax);

            // Starting values from the Task 1 client meeting (owner-confirmed ranges).
            db.PricingRules.AddRange(
                new PricingRule { Service = payroll, Key = "base", Label = "Base fee", MinValue = 250, MaxValue = 250 },
                new PricingRule { Service = payroll, Key = "per-employee", Label = "Per employee", MinValue = 50, MaxValue = 100, Unit = "/ employee / month" },
                new PricingRule { Service = payroll, Key = "weekly-surcharge", Label = "Weekly payroll surcharge", MinValue = 200, MaxValue = 200 },
                new PricingRule { Service = accounting, Key = "turnover-lt1m", Label = "Turnover under R1m", MinValue = 900, MaxValue = 1400 },
                new PricingRule { Service = accounting, Key = "turnover-1to5m", Label = "Turnover R1m – R5m", MinValue = 1400, MaxValue = 2200 },
                new PricingRule { Service = accounting, Key = "turnover-5to20m", Label = "Turnover R5m – R20m", MinValue = 2200, MaxValue = 3500 },
                new PricingRule { Service = tax, Key = "turnover-lt1m", Label = "Turnover under R1m", MinValue = 650, MaxValue = 950 },
                new PricingRule { Service = tax, Key = "turnover-1to5m", Label = "Turnover R1m – R5m", MinValue = 950, MaxValue = 1500 },
                new PricingRule { Service = tax, Key = "turnover-5to20m", Label = "Turnover R5m – R20m", MinValue = 1500, MaxValue = 2400 });
            await db.SaveChangesAsync();
        }

        private static async Task<ApplicationUser?> EnsureUserAsync(UserManager<ApplicationUser> users, string email, string name, string password, string role, string? speciality, ILogger logger)
        {
            var user = await users.FindByEmailAsync(email);
            if (user != null) return user;

            user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, FullName = name, Speciality = speciality };
            var result = await users.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                logger.LogError("Could not create seed user {Email}: {Errors}", email, string.Join("; ", result.Errors.Select(e => e.Description)));
                return null;
            }
            await users.AddToRoleAsync(user, role);
            return user;
        }

        private static async Task SeedDemoAsync(AppDbContext db, UserManager<ApplicationUser> users, string password, ILogger logger)
        {
            var admin = await EnsureUserAsync(users, DemoAdminEmail, "Demo Accountant", password, Roles.Admin, "Accounting, Payroll & Tax", logger);
            if (admin == null || await db.Clients.AnyAsync()) return;

            var demoClients = new (string Email, string Name, string? Company, string Status)[]
            {
                (DemoClientEmail, "Demo Client", "Demo Trading (Pty) Ltd", TaxStatuses.Submitted),
                ("naledi@demo.khabzela.co.za", "Naledi Demo", null, TaxStatuses.NotFiled),
                ("sipho@demo.khabzela.co.za", "Sipho Demo", "Demo Construction CC", TaxStatuses.SarsQuery),
            };

            var services = await db.Services.ToDictionaryAsync(s => s.Key);
            var number = 1001;
            var today = DateTime.UtcNow.Date;

            foreach (var (email, name, company, status) in demoClients)
            {
                var user = await EnsureUserAsync(users, email, name, password, Roles.Client, null, logger);
                if (user == null) continue;

                var client = new Client { User = user, CompanyName = company, TaxStatus = status, Accountant = admin };
                // Demo tax profiles so the deadline reminders have something to work with.
                client.IsEmployer = company != null;
                client.IsCompany = company != null;
                client.IsVatRegistered = email == DemoClientEmail;
                client.VatCategory = "A";
                db.Clients.Add(client);

                var quote = new Quote { Client = client, EstimateLow = 850, EstimateHigh = 1450, Status = QuoteStatuses.New };
                quote.Lines.Add(new QuoteLine { Service = services["payroll"], Summary = "12 employees · monthly", AnswersJson = "{\"service\":\"payroll\",\"employees\":12,\"frequency\":\"monthly\"}" });
                db.Quotes.Add(quote);

                db.Invoices.AddRange(
                    new Invoice { Number = number++, Client = client, Description = "Payroll — last month", Amount = 1150, IssuedAt = today.AddDays(-40), DueDate = today.AddDays(-26), Status = InvoiceStatuses.Paid, PaidAt = today.AddDays(-28) },
                    new Invoice { Number = number++, Client = client, Description = "Payroll — this month", Amount = 1150, IssuedAt = today.AddDays(-5), DueDate = today.AddDays(9) });

                db.Notifications.Add(new Notification { Client = client, Type = NotificationTypes.Tax, Title = $"Your tax status changed to “{status}”", Message = "Your accountant will follow up if anything is needed.", SentById = admin.Id, SentAt = DateTime.UtcNow.AddDays(-1) });
            }
            await db.SaveChangesAsync();
        }
    }
}
