using Azure;
using Azure.Communication.Email;
using Microsoft.Extensions.Options;

namespace KhabzelaConsulting.Services
{
    public class AcsEmailOptions
    {
        // Azure Communication Services resource connection string (Keys blade).
        public string? ConnectionString { get; set; }

        // Verified sender, e.g. DoNotReply@<guid>.azurecomm.net for an Azure-managed domain.
        public string? SenderAddress { get; set; }

        public bool IsConfigured => !string.IsNullOrWhiteSpace(ConnectionString) && !string.IsNullOrWhiteSpace(SenderAddress);
    }

    // Sends email through Azure Communication Services Email (used for the Azure deployment).
    public class AcsEmailSender : IEmailSender
    {
        private readonly AcsEmailOptions _options;
        private readonly ILogger<AcsEmailSender> _logger;
        private readonly Lazy<EmailClient> _client;

        public AcsEmailSender(IOptions<AcsEmailOptions> options, ILogger<AcsEmailSender> logger)
        {
            _options = options.Value;
            _logger = logger;
            _client = new Lazy<EmailClient>(() => new EmailClient(_options.ConnectionString));
        }

        public bool IsConfigured => _options.IsConfigured;

        public async Task SendAsync(string to, string subject, string htmlBody, string? replyTo = null, CancellationToken ct = default)
        {
            if (!_options.IsConfigured)
            {
                _logger.LogWarning("Azure email not configured — email to {To} with subject {Subject} was not sent.", to, subject);
                return;
            }

            var message = new EmailMessage(_options.SenderAddress, new EmailRecipients(new[] { new EmailAddress(to) }),
                new EmailContent(subject) { Html = htmlBody });
            if (!string.IsNullOrWhiteSpace(replyTo)) message.ReplyTo.Add(new EmailAddress(replyTo));

            var operation = await _client.Value.SendAsync(WaitUntil.Completed, message, ct);
            if (operation.Value.Status != EmailSendStatus.Succeeded)
                throw new InvalidOperationException($"Azure email send ended with status {operation.Value.Status}.");
        }
    }

    // Public contact details shown on the website; overridable per deployment.
    public class SiteOptions
    {
        public string ContactEmail { get; set; } = "vukani@khabzelaconsulting.co.za";
    }
}
