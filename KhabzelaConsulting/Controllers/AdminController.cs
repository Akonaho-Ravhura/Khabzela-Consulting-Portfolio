using KhabzelaConsulting.Data;
using KhabzelaConsulting.Models;
using KhabzelaConsulting.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KhabzelaConsulting.Controllers
{
    // Staff / owner admin portal.
    [Authorize(Roles = Roles.Admin)]
    public class AdminController : Controller
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _users;
        private readonly NotificationService _notifications;
        private readonly DeadlineReminderService _reminders;
        private readonly SaClock _clock;
        private readonly IEmailSender _email;
        private readonly ILogger<AdminController> _logger;

        public AdminController(AppDbContext db, UserManager<ApplicationUser> users, NotificationService notifications, DeadlineReminderService reminders, SaClock clock, IEmailSender email, ILogger<AdminController> logger)
        {
            _db = db;
            _users = users;
            _notifications = notifications;
            _reminders = reminders;
            _clock = clock;
            _email = email;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var today = _clock.Today;
            var monthStart = new DateTime(today.Year, today.Month, 1);
            var monthStartUtc = monthStart.AddHours(-2);
            var lastMonthStartUtc = monthStart.AddMonths(-1).AddHours(-2);

            var clients = await _db.Clients.CountAsync();
            var newClients = await _db.Clients.CountAsync(c => c.CreatedAt >= monthStartUtc);
            var pending = await _db.Quotes.CountAsync(q => q.Status == QuoteStatuses.New);
            var todayUtc = today.AddHours(-2);
            var newToday = await _db.Quotes.CountAsync(q => q.Status == QuoteStatuses.New && q.CreatedAt >= todayUtc);
            var paid = await _db.Invoices.Where(i => i.Status == InvoiceStatuses.Paid && i.PaidAt >= lastMonthStartUtc)
                .Select(i => new { i.Amount, i.PaidAt }).ToListAsync();
            var revenue = paid.Where(p => p.PaidAt >= monthStartUtc).Sum(p => p.Amount);
            var lastRevenue = paid.Where(p => p.PaidAt < monthStartUtc).Sum(p => p.Amount);
            var trend = lastRevenue == 0 ? "No payments last month" : $"{(revenue >= lastRevenue ? "+" : "−")}{Math.Abs((revenue - lastRevenue) / lastRevenue):P0} vs last month";

            ViewData["Stats"] = new List<DashboardStat>
            {
                new("Active clients", clients.ToString(), $"+{newClients} this month", Url.Action(nameof(Clients))!),
                new("Pending quote requests", pending.ToString(), $"{newToday} new today", Url.Action(nameof(QuoteRequests))!),
                new("This month's revenue", PricingCalculator.FormatRand(revenue), trend, Url.Action(nameof(Invoices))!),
            };

            // Upcoming SARS deadlines across all clients, so the team can prepare before clients are reminded.
            await _reminders.SendDueRemindersAsync();
            var clientList = await _db.Clients.Include(c => c.User).ToListAsync();
            ViewData["Deadlines"] = clientList
                .SelectMany(c => TaxCalendar.Upcoming(DeadlineReminderService.ProfileOf(c), today, 14)
                    .Select(d => new ClientDeadlineRow(c.Id, c.User.FullName, d.Title, d.Detail, d.Due, (d.Due - today).Days)))
                .OrderBy(d => d.Due).ThenBy(d => d.Client).ToList();
            return View((await QuoteRowsAsync()).Take(5).ToList());
        }

        public async Task<IActionResult> Clients()
        {
            var clients = await _db.Clients.Include(c => c.User).OrderBy(c => c.User.FullName).ToListAsync();
            var services = (await _db.QuoteLines.Where(l => l.Quote.Status == QuoteStatuses.Won)
                    .Select(l => new { l.Quote.ClientId, l.Service.Name }).Distinct().ToListAsync())
                .ToLookup(x => x.ClientId, x => x.Name);

            return View(clients.Select(c => new ClientRow(c.Id, c.User.FullName, c.User.Initials, c.User.Email ?? "",
                services[c.Id].Any() ? string.Join(" + ", services[c.Id]) : "—", c.TaxStatus, _clock.ToLocal(c.CreatedAt).Year)).ToList());
        }

        public async Task<IActionResult> ClientDetail(int id)
        {
            var client = await _db.Clients.Include(c => c.User).FirstOrDefaultAsync(c => c.Id == id);
            if (client == null) return NotFound();

            var history = await _db.Notifications.Where(n => n.ClientId == id).OrderByDescending(n => n.SentAt).Take(20).ToListAsync();
            var services = await _db.QuoteLines.Where(l => l.Quote.ClientId == id && l.Quote.Status == QuoteStatuses.Won)
                .Select(l => l.Service.Name).Distinct().ToListAsync();
            var summary = $"{(services.Count > 0 ? string.Join(" + ", services) + " client" : "Client")} since {_clock.ToLocal(client.CreatedAt).Year}";

            return View(new ClientDetailPage(client.Id, client.User.FullName, client.User.Initials, client.User.Email ?? "", client.User.PhoneNumber,
                client.CompanyName, summary, client.TaxStatus,
                history.Select(h => new HistoryItem(h.Title, $"Sent {SentText(h.SentAt)} · {(h.Channel == "app+email" ? "App + email" : "App")}{(h.ReadAt != null ? " · Read" : "")}")).ToList(),
                DeadlineReminderService.ProfileOf(client),
                TaxCalendar.Upcoming(DeadlineReminderService.ProfileOf(client), _clock.Today, 180)));
        }

        // Which SARS returns apply to the client — drives their automatic deadline reminders.
        [HttpPost]
        public async Task<IActionResult> UpdateTaxProfile(int id, TaxProfileInput input)
        {
            if (!ModelState.IsValid) return BadRequest(new { error = ModelState.Values.SelectMany(v => v.Errors).First().ErrorMessage });

            var client = await _db.Clients.FindAsync(id);
            if (client == null) return NotFound(new { error = "Client not found." });

            client.IsEmployer = input.IsEmployer;
            client.IsVatRegistered = input.IsVatRegistered;
            client.VatCategory = input.VatCategory;
            client.IsProvisionalTaxpayer = input.IsProvisionalTaxpayer;
            client.IsCompany = input.IsCompany;
            client.YearEndMonth = input.YearEndMonth;
            await _db.SaveChangesAsync();

            var deadlines = TaxCalendar.Upcoming(DeadlineReminderService.ProfileOf(client), _clock.Today, 180);
            return Json(new { deadlines = deadlines.Select(d => new { title = d.Title, detail = d.Detail, due = d.Due.ToString("ddd d MMM yyyy") }) });
        }

        // Updates the client's tax status (and/or sends a note) and notifies them — the feature that answers
        // the owner's pain point of clients not hearing about status changes.
        [HttpPost]
        public async Task<IActionResult> UpdateTaxStatus(int id, TaxStatusInput input)
        {
            if (!ModelState.IsValid || !TaxStatuses.All.Contains(input.Status))
                return BadRequest(new { error = "Choose a valid tax status." });

            var client = await _db.Clients.Include(c => c.User).FirstOrDefaultAsync(c => c.Id == id);
            if (client == null) return NotFound(new { error = "Client not found." });

            var note = input.Note?.Trim();
            var changed = client.TaxStatus != input.Status;
            if (!changed && string.IsNullOrEmpty(note)) return BadRequest(new { error = "Choose a new status or add a note first." });

            client.TaxStatus = input.Status;
            var title = changed ? $"Your tax status changed to “{input.Status}”" : "Note from your accountant";
            var message = string.IsNullOrEmpty(note) ? (changed ? "No action needed — your accountant will follow up if anything is required." : null) : note;
            var sent = await _notifications.NotifyAsync(client, NotificationTypes.Tax, title, message, _users.GetUserId(User));
            await _db.SaveChangesAsync();

            _logger.LogInformation("Tax status for client {ClientId} set to {Status} by {UserId}.", id, input.Status, _users.GetUserId(User));
            return Json(new { title = changed ? $"Status → {input.Status}" : "Note from your accountant", meta = $"Sent {SentText(sent.SentAt)} · {(sent.Channel == "app+email" ? "App + email" : "App")}" });
        }

        public async Task<IActionResult> PricingRules()
        {
            var services = await _db.Services.Include(s => s.PricingRules).OrderBy(s => s.SortOrder).ToListAsync();
            return View(services.Select(s => new RuleGroup(s.Name, s.PricingRules.OrderBy(r => r.Id)
                .Select(r => new RuleRow(r.Id, r.Label, PricingCalculator.FormatRule(r), r.MinValue, r.MaxValue, r.Unit, r.Status)).ToList())).ToList());
        }

        [HttpPost]
        public async Task<IActionResult> UpdatePricingRule(int id, PricingRuleInput input)
        {
            if (!RuleStatuses.All.Contains(input.Status)) ModelState.AddModelError(nameof(input.Status), "Choose Active or Draft.");
            if (input.MaxValue < input.MinValue) ModelState.AddModelError(nameof(input.MaxValue), "The maximum can't be less than the minimum.");
            if (!ModelState.IsValid) return BadRequest(new { error = ModelState.Values.SelectMany(v => v.Errors).First().ErrorMessage });

            var rule = await _db.PricingRules.FindAsync(id);
            if (rule == null) return NotFound(new { error = "Rule not found." });

            rule.Label = input.Label.Trim();
            rule.MinValue = input.MinValue;
            rule.MaxValue = input.MaxValue;
            rule.Status = input.Status;
            rule.UpdatedAt = DateTime.UtcNow;
            rule.UpdatedById = _users.GetUserId(User);
            await _db.SaveChangesAsync();

            return Json(new { label = rule.Label, formula = PricingCalculator.FormatRule(rule), status = rule.Status });
        }

        public async Task<IActionResult> QuoteRequests() => View(await QuoteRowsAsync());

        [HttpPost]
        public async Task<IActionResult> UpdateQuoteStatus(int id, QuoteStatusInput input)
        {
            if (!QuoteStatuses.All.Contains(input.Status)) return BadRequest(new { error = "Choose a valid status." });
            var quote = await _db.Quotes.FindAsync(id);
            if (quote == null) return NotFound(new { error = "Quote request not found." });
            quote.Status = input.Status;
            await _db.SaveChangesAsync();
            return Json(new { status = quote.Status });
        }

        public async Task<IActionResult> Invoices()
        {
            var today = _clock.Today;
            var invoices = await _db.Invoices.Include(i => i.Client).ThenInclude(c => c.User).OrderByDescending(i => i.Number).ToListAsync();
            ViewData["Clients"] = await _db.Clients.Include(c => c.User).OrderBy(c => c.User.FullName)
                .Select(c => new KeyValuePair<int, string>(c.Id, c.User.FullName)).ToListAsync();
            return View(invoices.Select(i => new InvoiceRow(i.Id, $"#{i.Number}", i.Client.User.FullName, i.ClientId, i.Description, i.IssuedAt, i.DueDate, i.Amount, i.DisplayStatus(today))).ToList());
        }

        [HttpPost]
        public async Task<IActionResult> CreateInvoice(InvoiceInput input)
        {
            if (input.DueDate.HasValue && input.DueDate.Value.Date < _clock.Today)
                ModelState.AddModelError(nameof(input.DueDate), "The due date can't be in the past.");
            if (!ModelState.IsValid) return BadRequest(new { error = ModelState.Values.SelectMany(v => v.Errors).First().ErrorMessage });

            var client = await _db.Clients.Include(c => c.User).FirstOrDefaultAsync(c => c.Id == input.ClientId);
            if (client == null) return BadRequest(new { error = "Choose a client." });

            var number = (await _db.Invoices.MaxAsync(i => (int?)i.Number) ?? 1000) + 1;
            var invoice = new Invoice
            {
                Number = number,
                Client = client,
                Description = input.Description.Trim(),
                Amount = Math.Round(input.Amount, 2),
                DueDate = input.DueDate!.Value.Date,
            };
            _db.Invoices.Add(invoice);
            await _notifications.NotifyAsync(client, NotificationTypes.Invoice, $"New invoice #{number}",
                $"{invoice.Description} — {PricingCalculator.FormatRand(invoice.Amount)}, due {invoice.DueDate:d MMM yyyy}.", _users.GetUserId(User));
            await _db.SaveChangesAsync();

            return Json(new { id = invoice.Id, number = $"#{number}", client = client.User.FullName, description = invoice.Description, amount = invoice.Amount, due = invoice.DueDate.ToString("d MMM") });
        }

        [HttpPost]
        public async Task<IActionResult> MarkInvoicePaid(int id)
        {
            var invoice = await _db.Invoices.Include(i => i.Client).ThenInclude(c => c.User).FirstOrDefaultAsync(i => i.Id == id);
            if (invoice == null) return NotFound(new { error = "Invoice not found." });
            if (invoice.Status == InvoiceStatuses.Paid) return Json(new { ok = true });

            invoice.Status = InvoiceStatuses.Paid;
            invoice.PaidAt = DateTime.UtcNow;
            await _notifications.NotifyAsync(invoice.Client, NotificationTypes.Invoice, $"Invoice #{invoice.Number} marked paid", "Thank you — we've received your payment.", _users.GetUserId(User));
            await _db.SaveChangesAsync();
            return Json(new { ok = true });
        }

        // ----- Team: administrators are invited by an existing administrator, never self-registered. -----

        public async Task<IActionResult> Team()
        {
            var me = _users.GetUserId(User);
            var admins = (await _users.GetUsersInRoleAsync(Roles.Admin)).OrderBy(u => u.CreatedAt).ToList();
            var rows = new List<TeamRow>();
            foreach (var u in admins)
                rows.Add(new TeamRow(u.Id, u.FullName, u.Initials, u.Email ?? "", u.Speciality, await _users.HasPasswordAsync(u), u.Id == me));
            return View(rows);
        }

        [HttpPost]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("forms")]
        public async Task<IActionResult> InviteTeamMember(TeamMemberInput input)
        {
            if (!ModelState.IsValid) return BadRequest(new { error = ModelState.Values.SelectMany(v => v.Errors).First().ErrorMessage });

            var email = input.Email.Trim();
            if (await _users.FindByEmailAsync(email) is { } existing)
            {
                return BadRequest(new
                {
                    error = await _users.IsInRoleAsync(existing, Roles.Admin)
                        ? "This person is already on the team."
                        : "This email belongs to a client account. Use a different (work) email for team access."
                });
            }

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = input.FullName.Trim(),
                Speciality = string.IsNullOrWhiteSpace(input.Speciality) ? null : input.Speciality.Trim(),
            };
            var created = await _users.CreateAsync(user); // no password until they accept the invitation
            if (!created.Succeeded) return BadRequest(new { error = created.Errors.First().Description });
            await _users.AddToRoleAsync(user, Roles.Admin);

            var (link, emailed) = await SendInviteAsync(user);
            _logger.LogInformation("Team member {Email} invited by {UserId}.", email, _users.GetUserId(User));
            return Json(new { id = user.Id, name = user.FullName, initials = user.Initials, email, speciality = user.Speciality, link, emailed });
        }

        [HttpPost]
        public async Task<IActionResult> ResendInvite(string id)
        {
            var user = await _users.FindByIdAsync(id);
            if (user == null || !await _users.IsInRoleAsync(user, Roles.Admin)) return NotFound(new { error = "Team member not found." });
            if (await _users.HasPasswordAsync(user)) return BadRequest(new { error = "They've already set up their account." });

            var (link, emailed) = await SendInviteAsync(user);
            return Json(new { link, emailed });
        }

        [HttpPost]
        public async Task<IActionResult> RemoveTeamMember(string id)
        {
            if (id == _users.GetUserId(User)) return BadRequest(new { error = "You can't remove your own access." });

            var user = await _users.FindByIdAsync(id);
            if (user == null || !await _users.IsInRoleAsync(user, Roles.Admin)) return NotFound(new { error = "Team member not found." });
            if ((await _users.GetUsersInRoleAsync(Roles.Admin)).Count <= 1) return BadRequest(new { error = "The team needs at least one administrator." });

            // Keep the account (their past appointments and notes still reference it) but revoke access.
            await _users.RemoveFromRoleAsync(user, Roles.Admin);
            await _users.SetLockoutEnabledAsync(user, true);
            await _users.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
            await _users.UpdateSecurityStampAsync(user); // signs them out everywhere
            _logger.LogInformation("Team member {UserId} removed by {AdminId}.", id, _users.GetUserId(User));
            return Json(new { ok = true });
        }

        // Emails the invitation when SMTP is set up; the link is also returned so it can be shared another way.
        private async Task<(string Link, bool Emailed)> SendInviteAsync(ApplicationUser user)
        {
            var token = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(System.Text.Encoding.UTF8.GetBytes(
                await _users.GenerateUserTokenAsync(user, InviteTokenProvider.ProviderName, InviteTokenProvider.Purpose)));
            var link = Url.Action("ResetPassword", "Account", new { email = user.Email, token, invite = true }, Request.Scheme)!;

            var emailed = false;
            if (_email.IsConfigured)
            {
                try
                {
                    await _email.SendAsync(user.Email!, "You've been invited to the Khabzela Consulting admin portal",
                        $"<p>Hi {System.Net.WebUtility.HtmlEncode(user.FirstName)},</p><p>You've been added to the Khabzela Consulting team. " +
                        $"Choose a password to finish setting up your account (this link expires in 3 days):</p>" +
                        $"<p><a href=\"{System.Net.WebUtility.HtmlEncode(link)}\">Set up my account</a></p>");
                    emailed = true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Invitation email to {UserId} failed.", user.Id);
                }
            }
            return (link, emailed);
        }

        private async Task<List<QuoteRow>> QuoteRowsAsync()
        {
            var quotes = await _db.Quotes
                .Include(q => q.Client).ThenInclude(c => c.User)
                .Include(q => q.Lines).ThenInclude(l => l.Service)
                .Include(q => q.Appointment)
                .OrderByDescending(q => q.CreatedAt).Take(200).ToListAsync();
            var now = _clock.Now;

            return quotes.Select(q => new QuoteRow(q.Id, q.Client.User.FullName, q.Client.User.Email ?? "",
                string.Join(" + ", q.Lines.Select(l => l.Service.Name)),
                string.Join(" · ", q.Lines.Select(l => l.Summary)),
                q.EstimateLow.HasValue ? $"{PricingCalculator.FormatRand(q.EstimateLow.Value)} – {PricingCalculator.FormatRand(q.EstimateHigh!.Value)}" : "—",
                q.Appointment == null ? "—" : q.Appointment.ScheduledAt < now ? "Done" : q.Appointment.ScheduledAt.ToString("ddd d MMM, HH:mm"),
                q.Status, _clock.ToLocal(q.CreatedAt))).ToList();
        }

        private string SentText(DateTime utc)
        {
            var local = _clock.ToLocal(utc);
            return local.Date == _clock.Today ? $"to client today, {local:HH:mm}" : $"to client {local:d MMM}";
        }
    }
}
