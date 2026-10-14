using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace KhabzelaConsulting.Services
{
    public class SmtpOptions
    {
        public string? Host { get; set; }
        public int Port { get; set; } = 587;
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string FromAddress { get; set; } = "vukani@khabzelaconsulting.co.za";
        public string FromName { get; set; } = "Khabzela Consulting";
        public bool UseStartTls { get; set; } = true;

        // Where contact-form submissions are delivered.
        public string ContactRecipient { get; set; } = "vukani@khabzelaconsulting.co.za";

        public bool IsConfigured => !string.IsNullOrWhiteSpace(Host);
    }

    public interface IEmailSender
    {
        bool IsConfigured { get; }
        Task SendAsync(string to, string subject, string htmlBody, string? replyTo = null, CancellationToken ct = default);
    }

    // Sends through the firm's mailbox (Afrihost SMTP). When SMTP isn't configured (local development)
    // the message is written to the log instead, so nothing fails silently.
    public class SmtpEmailSender : IEmailSender
    {
        private readonly SmtpOptions _options;
        private readonly ILogger<SmtpEmailSender> _logger;

        public SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger)
        {
            _options = options.Value;
            _logger = logger;
        }

        public bool IsConfigured => _options.IsConfigured;

        public async Task SendAsync(string to, string subject, string htmlBody, string? replyTo = null, CancellationToken ct = default)
        {
            if (!_options.IsConfigured)
            {
                _logger.LogWarning("SMTP not configured — email to {To} with subject {Subject} was not sent.", to, subject);
                return;
            }

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
            message.To.Add(MailboxAddress.Parse(to));
            if (!string.IsNullOrWhiteSpace(replyTo)) message.ReplyTo.Add(MailboxAddress.Parse(replyTo));
            message.Subject = subject;
            message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

            using var client = new SmtpClient { Timeout = 15000 };
            await client.ConnectAsync(_options.Host!, _options.Port,
                _options.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.SslOnConnect, ct);
            if (!string.IsNullOrEmpty(_options.Username))
                await client.AuthenticateAsync(_options.Username, _options.Password ?? "", ct);
            await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);
        }
    }
}
