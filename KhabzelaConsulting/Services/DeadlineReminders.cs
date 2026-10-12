using KhabzelaConsulting.Data;
using KhabzelaConsulting.Models;
using Microsoft.EntityFrameworkCore;

namespace KhabzelaConsulting.Services
{
    // Sends each client one reminder per upcoming SARS deadline, a week before it's due.
    public class DeadlineReminderService
    {
        public const int ReminderDaysBefore = 7;

        private readonly AppDbContext _db;
        private readonly NotificationService _notifications;
        private readonly SaClock _clock;
        private readonly ILogger<DeadlineReminderService> _logger;

        public DeadlineReminderService(AppDbContext db, NotificationService notifications, SaClock clock, ILogger<DeadlineReminderService> logger)
        {
            _db = db;
            _notifications = notifications;
            _clock = clock;
            _logger = logger;
        }

        public static TaxProfile ProfileOf(Client c) =>
            new(c.IsEmployer, c.IsVatRegistered, c.VatCategory, c.IsProvisionalTaxpayer, c.IsCompany, c.YearEndMonth);

        // Pass a client id to process one client (e.g. when they open the app); null processes everyone.
        public async Task<int> SendDueRemindersAsync(int? clientId = null, CancellationToken ct = default)
        {
            var today = _clock.Today;
            var clients = await _db.Clients.Include(c => c.User)
                .Where(c => clientId == null || c.Id == clientId).ToListAsync(ct);

            var sent = 0;
            foreach (var client in clients)
            {
                var due = TaxCalendar.Upcoming(ProfileOf(client), today, ReminderDaysBefore);
                if (due.Count == 0) continue;

                var keys = due.Select(d => "deadline:" + d.Key).ToList();
                var already = await _db.Notifications.Where(n => n.ClientId == client.Id && keys.Contains(n.DedupKey!))
                    .Select(n => n.DedupKey).ToListAsync(ct);

                foreach (var deadline in due.Where(d => !already.Contains("deadline:" + d.Key)))
                {
                    var days = (deadline.Due - today).Days;
                    var when = days == 0 ? "today" : days == 1 ? "tomorrow" : $"in {days} days";
                    var n = await _notifications.NotifyAsync(client, NotificationTypes.Deadline,
                        $"{deadline.Title} due {when}",
                        $"{deadline.Detail} Due {deadline.Due:dddd d MMMM yyyy}. Your accountant will be in touch if anything is needed from you.");
                    n.DedupKey = "deadline:" + deadline.Key;
                    sent++;
                }
            }

            if (sent > 0)
            {
                try
                {
                    await _db.SaveChangesAsync(ct);
                }
                catch (DbUpdateException ex)
                {
                    // Another run saved the same reminder first (unique ClientId + DedupKey). Nothing is lost.
                    _logger.LogInformation(ex, "Deadline reminders were already sent by another run.");
                    _db.ChangeTracker.Clear();
                    return 0;
                }
                _logger.LogInformation("Sent {Count} deadline reminder(s).", sent);
            }
            return sent;
        }
    }

    // Runs the reminders every few hours. Hosting may stop an idle site, so reminders are also
    // checked whenever a client opens the app or staff open the dashboard.
    public class DeadlineReminderJob : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<DeadlineReminderJob> _logger;

        public DeadlineReminderJob(IServiceScopeFactory scopes, ILogger<DeadlineReminderJob> logger)
        {
            _scopes = scopes;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromHours(4));
            do
            {
                try
                {
                    using var scope = _scopes.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<DeadlineReminderService>().SendDueRemindersAsync(ct: stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Deadline reminder run failed; will retry on the next run.");
                }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
    }
}
