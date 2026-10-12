using System.Net;
using KhabzelaConsulting.Data;
using KhabzelaConsulting.Models;

namespace KhabzelaConsulting.Services
{
    // Creates in-app notifications (the client's record of what happened) and, if the client opted in,
    // sends an email copy. Email failures are logged and never block the action that triggered them.
    public class NotificationService
    {
        private readonly AppDbContext _db;
        private readonly IEmailSender _email;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(AppDbContext db, IEmailSender email, ILogger<NotificationService> logger)
        {
            _db = db;
            _email = email;
            _logger = logger;
        }

        // Adds the notification to the context; the caller saves changes. Client.User must be loaded.
        public async Task<Notification> NotifyAsync(Client client, string type, string title, string? message, string? sentById = null)
        {
            var wantsEmail = client.NotifyByEmail && type switch
            {
                NotificationTypes.Tax => client.NotifyTaxStatus,
                NotificationTypes.Deadline => client.NotifyDeadlines,
                _ => true
            };

            var notification = new Notification
            {
                Client = client,
                Type = type,
                Title = title,
                Message = message,
                SentById = sentById,
                Channel = "app"
            };
            _db.Notifications.Add(notification);

            if (wantsEmail && _email.IsConfigured && !string.IsNullOrEmpty(client.User?.Email))
            {
                try
                {
                    var body = $"<p>Hi {WebUtility.HtmlEncode(client.User.FirstName)},</p>" +
                               $"<p><strong>{WebUtility.HtmlEncode(title)}</strong></p>" +
                               (string.IsNullOrWhiteSpace(message) ? "" : $"<p>{WebUtility.HtmlEncode(message)}</p>") +
                               "<p>Sign in to the Khabzela Consulting app for details.</p><p>— Khabzela Consulting</p>";
                    await _email.SendAsync(client.User.Email, title, body);
                    notification.Channel = "app+email";
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Email copy of notification '{Title}' to client {ClientId} failed.", title, client.Id);
                }
            }
            return notification;
        }
    }

    public static class BookingSlots
    {
        public static readonly string[] Times = { "09:00", "10:30", "13:00", "14:30", "15:30", "16:30" };
        public const int DaysAhead = 5;

        // Next five working days, starting tomorrow.
        public static List<DateTime> Days(DateTime today)
        {
            var days = new List<DateTime>();
            for (var d = today.Date.AddDays(1); days.Count < DaysAhead; d = d.AddDays(1))
            {
                if (d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)) days.Add(d);
            }
            return days;
        }

        public static bool IsValidSlot(DateTime slot, DateTime today) =>
            Days(today).Contains(slot.Date) && Times.Contains(slot.ToString("HH:mm"));
    }
}
