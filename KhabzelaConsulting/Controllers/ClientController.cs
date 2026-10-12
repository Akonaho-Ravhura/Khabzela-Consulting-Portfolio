using System.Globalization;
using System.Text.Json;
using KhabzelaConsulting.Data;
using KhabzelaConsulting.Models;
using KhabzelaConsulting.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KhabzelaConsulting.Controllers
{
    // Customer-facing app (mobile-first, installable as a PWA). Every query is scoped to the signed-in client.
    [Authorize(Roles = Roles.Client)]
    public class ClientController : Controller
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _users;
        private readonly SignInManager<ApplicationUser> _signIn;
        private readonly NotificationService _notifications;
        private readonly DeadlineReminderService _reminders;
        private readonly SaClock _clock;
        private readonly ILogger<ClientController> _logger;

        public ClientController(AppDbContext db, UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn, NotificationService notifications, DeadlineReminderService reminders, SaClock clock, ILogger<ClientController> logger)
        {
            _db = db;
            _users = users;
            _signIn = signIn;
            _notifications = notifications;
            _reminders = reminders;
            _clock = clock;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var client = await CurrentClientAsync();
            var today = _clock.Today;

            // Reminders also run in the background; checking here means they arrive even if the site was idle.
            await _reminders.SendDueRemindersAsync(client.Id);
            var deadlines = TaxCalendar.Upcoming(DeadlineReminderService.ProfileOf(client), today, 90).Take(4).ToList();

            var activity = new List<ActivityItem>();
            var invoice = await _db.Invoices.Where(i => i.ClientId == client.Id).OrderByDescending(i => i.IssuedAt).FirstOrDefaultAsync();
            if (invoice != null)
            {
                var status = invoice.DisplayStatus(today);
                activity.Add(new ActivityItem($"Invoice #{invoice.Number}", status, status == InvoiceStatuses.Paid ? "success" : "muted", Url.Action(nameof(Invoices))!));
            }
            var notification = await _db.Notifications.Where(n => n.ClientId == client.Id).OrderByDescending(n => n.SentAt).FirstOrDefaultAsync();
            if (notification != null)
                activity.Add(new ActivityItem(notification.Title, Relative(_clock.ToLocal(notification.SentAt)), "muted", Url.Action(nameof(Notifications))!));
            var now = _clock.Now;
            var appointment = await _db.Appointments.Include(a => a.Consultant)
                .Where(a => a.ClientId == client.Id && a.Status == AppointmentStatuses.Booked && a.ScheduledAt >= now)
                .OrderBy(a => a.ScheduledAt).FirstOrDefaultAsync();
            if (appointment != null)
                activity.Add(new ActivityItem($"Appointment with {appointment.Consultant.FirstName}", appointment.ScheduledAt.ToString("ddd d, HH:mm"), "muted", Url.Action(nameof(Notifications))!));

            return View(new ClientHomePage(client.User.FirstName, client.User.Initials[..1], today, deadlines, activity));
        }

        public async Task<IActionResult> Quote()
        {
            var rules = await _db.PricingRules.Include(r => r.Service).ToListAsync();
            ViewData["RulesJson"] = JsonSerializer.Serialize(PricingCalculator.ToClientModel(rules));
            return View();
        }

        public async Task<IActionResult> Book()
        {
            var today = _clock.Today;
            var days = BookingSlots.Days(today);
            var consultants = (await _users.GetUsersInRoleAsync(Roles.Admin)).OrderBy(u => u.CreatedAt)
                .Select(u => new ConsultantOption(u.Id, u.Initials, u.FirstName, u.Speciality ?? "Accounting, Payroll & Tax")).ToList();

            var from = days[0];
            var to = days[^1].AddDays(1);
            var taken = await _db.Appointments
                .Where(a => a.Status == AppointmentStatuses.Booked && a.ScheduledAt >= from && a.ScheduledAt < to)
                .Select(a => new { a.ConsultantId, a.ScheduledAt }).ToListAsync();

            var booked = consultants.ToDictionary(c => c.Id, c => days.ToDictionary(
                d => d.ToString("yyyy-MM-dd"),
                d => taken.Where(t => t.ConsultantId == c.Id && t.ScheduledAt.Date == d).Select(t => t.ScheduledAt.ToString("HH:mm")).ToList()));

            return View(new BookPage(days, BookingSlots.Times, consultants, booked));
        }

        // Combined "get a quote + book an appointment" flow: one request creates the quote, its line,
        // the appointment and the confirmation notification.
        [HttpPost]
        public async Task<IActionResult> Book(BookingInput input)
        {
            if (!ModelState.IsValid) return BadRequest(new { error = "Some booking details are missing. Please go back and try again." });

            var quoteInput = new QuoteInput(input.Service, input.Employees, input.Frequency, input.Bracket);
            var quoteError = PricingCalculator.Validate(quoteInput);
            if (quoteError != null) return BadRequest(new { error = quoteError });

            if (!DateTime.TryParseExact($"{input.Day} {input.Time}", "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var slot)
                || !BookingSlots.IsValidSlot(slot, _clock.Today))
                return BadRequest(new { error = "That time isn't available any more. Please choose another slot." });

            var consultant = await _users.FindByIdAsync(input.ConsultantId);
            if (consultant == null || !await _users.IsInRoleAsync(consultant, Roles.Admin))
                return BadRequest(new { error = "Please choose who you'd like to meet with." });

            if (await _db.Appointments.AnyAsync(a => a.ConsultantId == consultant.Id && a.ScheduledAt == slot && a.Status == AppointmentStatuses.Booked))
                return Conflict(new { error = $"Sorry — {consultant.FirstName} was just booked at {input.Time}. Please pick another time." });

            var client = await CurrentClientAsync();
            var rules = await _db.PricingRules.Include(r => r.Service).ToListAsync();
            var estimate = PricingCalculator.Calculate(quoteInput, rules);
            var service = await _db.Services.SingleAsync(s => s.Key == input.Service);

            var quote = new Quote { Client = client, EstimateLow = estimate.Low, EstimateHigh = estimate.High };
            quote.Lines.Add(new QuoteLine
            {
                Service = service,
                Summary = estimate.Summary,
                AnswersJson = JsonSerializer.Serialize(input.Service == "payroll"
                    ? (object)new { service = input.Service, employees = input.Employees, frequency = input.Frequency }
                    : new { service = input.Service, bracket = input.Bracket })
            });
            var appointment = new Appointment { Client = client, Quote = quote, ConsultantId = consultant.Id, ScheduledAt = slot, Notes = input.Notes?.Trim() };
            _db.Quotes.Add(quote);
            _db.Appointments.Add(appointment);

            var estimateText = PricingCalculator.FormatRange(estimate.Low, estimate.High);
            await _notifications.NotifyAsync(client, NotificationTypes.Appointment, "Appointment confirmed",
                $"{service.Name} consultation with {consultant.FirstName} on {slot:dddd d MMMM} at {slot:HH:mm}. Estimate: {estimateText}{(estimate.HasPrice ? " / month" : "")}.");

            await _db.SaveChangesAsync();
            _logger.LogInformation("Client {ClientId} booked {Slot} with {ConsultantId} (quote {QuoteId}).", client.Id, slot, consultant.Id, quote.Id);

            return Json(new
            {
                when = slot.ToString("ddd d MMM, HH:mm"),
                with = consultant.FirstName,
                service = $"{service.Name} · {estimate.Summary}",
                estimate = estimate.HasPrice ? estimateText + " / mo" : estimateText
            });
        }

        public async Task<IActionResult> Notifications()
        {
            var client = await CurrentClientAsync();
            var items = await _db.Notifications.Where(n => n.ClientId == client.Id).OrderByDescending(n => n.SentAt).Take(100).ToListAsync();
            return View(items.Select(n => new NotificationRow(n.Id, n.Type switch
            {
                NotificationTypes.Invoice => "i-file",
                NotificationTypes.Deadline or NotificationTypes.Appointment => "i-calendar",
                _ => "i-clock"
            }, n.Title, n.Message, Relative(_clock.ToLocal(n.SentAt)),
            n.Type == NotificationTypes.Appointment ? NotificationTypes.Deadline : n.Type, n.ReadAt == null)).ToList());
        }

        [HttpPost]
        public async Task<IActionResult> MarkAllRead()
        {
            var client = await CurrentClientAsync();
            var unread = await _db.Notifications.Where(n => n.ClientId == client.Id && n.ReadAt == null).ToListAsync();
            unread.ForEach(n => n.ReadAt = DateTime.UtcNow);
            await _db.SaveChangesAsync();
            return Json(new { updated = unread.Count });
        }

        public async Task<IActionResult> Invoices()
        {
            var client = await CurrentClientAsync();
            var today = _clock.Today;
            var invoices = await _db.Invoices.Where(i => i.ClientId == client.Id).OrderByDescending(i => i.IssuedAt).ToListAsync();
            ViewData["PaidThisYear"] = invoices.Where(i => i.Status == InvoiceStatuses.Paid && i.PaidAt?.Year == today.Year).Sum(i => i.Amount);
            return View(invoices.Select(i => ToRow(i, client, today)).ToList());
        }

        // Printable invoice — the browser's "Save as PDF" produces the PDF.
        public async Task<IActionResult> Invoice(int id)
        {
            var client = await CurrentClientAsync();
            var invoice = await _db.Invoices.FirstOrDefaultAsync(i => i.Id == id && i.ClientId == client.Id);
            if (invoice == null) return NotFound();
            ViewData["Company"] = client.CompanyName;
            return View(ToRow(invoice, client, _clock.Today));
        }

        public async Task<IActionResult> Profile()
        {
            var client = await CurrentClientAsync();
            var u = client.User;
            var services = await _db.QuoteLines.Where(l => l.Quote.ClientId == client.Id && l.Quote.Status == QuoteStatuses.Won)
                .Select(l => l.Service.Name).Distinct().ToListAsync();
            var summary = $"{(services.Count > 0 ? string.Join(" + ", services) + " client" : "Client")} since {_clock.ToLocal(client.CreatedAt).Year}"
                          + (client.Accountant != null ? $" · Accountant: {client.Accountant.FirstName}" : "");

            return View(new ProfilePage(u.FullName, u.Initials[..1], u.Email ?? "", u.PhoneNumber, client.CompanyName, summary,
                client.NotifyTaxStatus, client.NotifyDeadlines, client.NotifyByEmail, u.TwoFactorEnabled, client.Accountant?.PhoneNumber));
        }

        [HttpPost]
        public async Task<IActionResult> Profile(ProfileInput input)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { error = ModelState.Values.SelectMany(v => v.Errors).First().ErrorMessage });

            var client = await CurrentClientAsync();
            var user = client.User;
            var email = input.Email.Trim();

            if (!string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
            {
                if (await _users.FindByEmailAsync(email) != null) return BadRequest(new { error = "Another account already uses that email address." });
                user.Email = email;
                user.UserName = email;
            }
            user.FullName = input.FullName.Trim();
            user.PhoneNumber = string.IsNullOrWhiteSpace(input.Phone) ? null : input.Phone.Trim();
            client.CompanyName = string.IsNullOrWhiteSpace(input.Company) ? null : input.Company.Trim();

            var result = await _users.UpdateAsync(user);
            if (!result.Succeeded) return BadRequest(new { error = result.Errors.First().Description });
            await _db.SaveChangesAsync();
            await _signIn.RefreshSignInAsync(user);
            return Json(new { ok = true });
        }

        [HttpPost]
        public async Task<IActionResult> Preference(PreferenceInput input)
        {
            if (!ModelState.IsValid) return BadRequest(new { error = "Unknown preference." });
            var client = await CurrentClientAsync();
            switch (input.Pref)
            {
                case "tax": client.NotifyTaxStatus = input.Enabled; break;
                case "deadlines": client.NotifyDeadlines = input.Enabled; break;
                case "email": client.NotifyByEmail = input.Enabled; break;
            }
            await _db.SaveChangesAsync();
            return Json(new { ok = true });
        }

        private async Task<Client> CurrentClientAsync()
        {
            var userId = _users.GetUserId(User)!;
            var client = await _db.Clients.Include(c => c.User).Include(c => c.Accountant).FirstOrDefaultAsync(c => c.UserId == userId);
            if (client != null) return client;

            // Recover if registration stopped between creating the login and the client record.
            var accountant = (await _users.GetUsersInRoleAsync(Roles.Admin)).OrderBy(u => u.CreatedAt).FirstOrDefault();
            client = new Client { UserId = userId, AccountantId = accountant?.Id };
            _db.Clients.Add(client);
            await _db.SaveChangesAsync();
            return await _db.Clients.Include(c => c.User).Include(c => c.Accountant).FirstAsync(c => c.Id == client.Id);
        }

        private static InvoiceRow ToRow(Invoice i, Client c, DateTime today) =>
            new(i.Id, $"#{i.Number}", c.User.FullName, c.Id, i.Description, i.IssuedAt, i.DueDate, i.Amount, i.DisplayStatus(today));

        private string Relative(DateTime local)
        {
            var today = _clock.Today;
            if (local.Date == today) return $"Today, {local:HH:mm}";
            if (local.Date == today.AddDays(-1)) return $"Yesterday, {local:HH:mm}";
            var days = (today - local.Date).Days;
            return days < 7 ? $"{days} days ago" : local.ToString("d MMM yyyy");
        }
    }
}
