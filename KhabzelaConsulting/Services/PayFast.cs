using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using KhabzelaConsulting.Data;
using KhabzelaConsulting.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KhabzelaConsulting.Services
{
    public class PayFastOptions
    {
        public string? MerchantId { get; set; }
        public string? MerchantKey { get; set; }

        // Must match the passphrase set on the PayFast account (Settings → Security), if any.
        public string? Passphrase { get; set; }

        // True uses sandbox.payfast.co.za (test payments, no real money).
        public bool UseSandbox { get; set; } = true;

        // Public address of the site, e.g. https://khabzela-consulting.azurewebsites.net.
        // PayFast calls back to it, so it must be reachable from the internet.
        public string? SiteUrl { get; set; }

        public bool IsConfigured => !string.IsNullOrWhiteSpace(MerchantId) && !string.IsNullOrWhiteSpace(MerchantKey);

        public string Host => UseSandbox ? "sandbox.payfast.co.za" : "www.payfast.co.za";
        public string ProcessUrl => $"https://{Host}/eng/process";
        public string ValidateUrl => $"https://{Host}/eng/query/validate";
    }

    // Online invoice payments through PayFast. The client is sent to PayFast's hosted payment page,
    // and PayFast notifies the site (ITN) when the payment completes, which marks the invoice paid
    // automatically. No card details ever touch this application.
    public class PayFastService
    {
        private readonly PayFastOptions _options;
        private readonly AppDbContext _db;
        private readonly NotificationService _notifications;
        private readonly IEmailSender _email;
        private readonly SmtpOptions _smtp;
        private readonly IHttpClientFactory _http;
        private readonly ILogger<PayFastService> _logger;

        public PayFastService(IOptions<PayFastOptions> options, AppDbContext db, NotificationService notifications, IEmailSender email,
            IOptions<SmtpOptions> smtp, IHttpClientFactory http, ILogger<PayFastService> logger)
        {
            _options = options.Value;
            _db = db;
            _notifications = notifications;
            _email = email;
            _smtp = smtp.Value;
            _http = http;
            _logger = logger;
        }

        public bool IsConfigured => _options.IsConfigured;
        public bool IsSandbox => _options.UseSandbox;
        public string ProcessUrl => _options.ProcessUrl;
        private bool HasPassphrase => !string.IsNullOrWhiteSpace(_options.Passphrase);

        // The fields posted to PayFast, in the order PayFast uses to check the signature.
        public List<KeyValuePair<string, string>> BuildPaymentFields(Invoice invoice, ApplicationUser user, string siteUrl)
        {
            var baseUrl = (string.IsNullOrWhiteSpace(_options.SiteUrl) ? siteUrl : _options.SiteUrl).TrimEnd('/');
            var lastName = user.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1).LastOrDefault() ?? "";
            var fields = new List<KeyValuePair<string, string>>
            {
                new("merchant_id", _options.MerchantId!),
                new("merchant_key", _options.MerchantKey!),
                new("return_url", $"{baseUrl}/Client/Invoices?paid={invoice.Id}"),
                new("cancel_url", $"{baseUrl}/Client/Invoices"),
                new("notify_url", $"{baseUrl}/Payments/PayFastNotify"),
                new("name_first", user.FirstName),
                new("name_last", lastName),
                new("email_address", user.Email ?? ""),
                new("m_payment_id", invoice.Id.ToString(CultureInfo.InvariantCulture)),
                new("amount", invoice.Amount.ToString("0.00", CultureInfo.InvariantCulture)),
                new("item_name", $"Khabzela Consulting invoice #{invoice.Number}"),
                new("item_description", Truncate(invoice.Description, 255)),
            };
            fields = fields.Where(f => !string.IsNullOrWhiteSpace(f.Value)).Select(f => new KeyValuePair<string, string>(f.Key, f.Value.Trim())).ToList();
            // A signed request needs the account's passphrase; without one PayFast accepts the request unsigned.
            if (HasPassphrase) fields.Add(new("signature", Signature(fields, _options.Passphrase)));
            return fields;
        }

        // PayFast signature: MD5 of the url-encoded "key=value&..." string, plus the passphrase if one is set.
        public static string Signature(IEnumerable<KeyValuePair<string, string>> fields, string? passphrase)
        {
            var data = Encode(fields.Where(f => f.Key != "signature"));
            if (!string.IsNullOrEmpty(passphrase)) data += "&passphrase=" + UrlEncode(passphrase.Trim());
            return Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(data))).ToLowerInvariant();
        }

        private static string Encode(IEnumerable<KeyValuePair<string, string>> fields) =>
            string.Join("&", fields.Select(f => $"{f.Key}={UrlEncode(f.Value ?? "")}"));

        // PHP urlencode style (spaces as "+", upper-case hex), which PayFast uses.
        private static string UrlEncode(string value) => WebUtility.UrlEncode(value);

        private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];

        // Handles PayFast's Instant Transaction Notification. Returns a short result for the log.
        public async Task<string> HandleNotificationAsync(IReadOnlyList<KeyValuePair<string, string>> posted, CancellationToken ct = default)
        {
            if (!IsConfigured) return "payments not configured";

            string Get(string key) => posted.FirstOrDefault(p => p.Key == key).Value ?? "";

            // 1. With a passphrase set, the signature proves the data wasn't changed and came from PayFast.
            if (HasPassphrase && !string.Equals(Get("signature"), Signature(posted, _options.Passphrase), StringComparison.OrdinalIgnoreCase))
                return "invalid signature";

            if (Get("merchant_id") != _options.MerchantId) return "wrong merchant";

            // 2. Always ask PayFast's server to confirm it really sent this notification.
            if (!await ConfirmWithPayFastAsync(posted, ct)) return "not confirmed by PayFast";

            if (Get("payment_status") != "COMPLETE") return $"status {Get("payment_status")}";

            if (!int.TryParse(Get("m_payment_id"), out var invoiceId)) return "unknown invoice";
            var invoice = await _db.Invoices.Include(i => i.Client).ThenInclude(c => c.User).FirstOrDefaultAsync(i => i.Id == invoiceId, ct);
            if (invoice == null) return "unknown invoice";

            // 3. The amount paid must match the invoice.
            if (!decimal.TryParse(Get("amount_gross"), NumberStyles.Number, CultureInfo.InvariantCulture, out var paid)
                || Math.Abs(paid - invoice.Amount) > 0.01m)
                return "amount mismatch";

            if (invoice.Status == InvoiceStatuses.Paid) return "already paid";

            invoice.Status = InvoiceStatuses.Paid;
            invoice.PaidAt = DateTime.UtcNow;
            var reference = Get("pf_payment_id");
            await _notifications.NotifyAsync(invoice.Client, NotificationTypes.Invoice, $"Payment received for invoice #{invoice.Number}",
                $"Thank you, we've received your online payment of {PricingCalculator.FormatRand(invoice.Amount)}. PayFast reference: {reference}.");
            await _db.SaveChangesAsync(ct);

            await TellTheFirmAsync(invoice, reference);
            _logger.LogInformation("Invoice #{Number} paid online (PayFast {Reference}).", invoice.Number, reference);
            return "paid";
        }

        private async Task<bool> ConfirmWithPayFastAsync(IReadOnlyList<KeyValuePair<string, string>> posted, CancellationToken ct)
        {
            try
            {
                var body = Encode(posted.Where(p => p.Key != "signature"));
                using var response = await _http.CreateClient("payfast").PostAsync(_options.ValidateUrl,
                    new StringContent(body, Encoding.UTF8, "application/x-www-form-urlencoded"), ct);
                var text = await response.Content.ReadAsStringAsync(ct);
                return text.Trim().Equals("VALID", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not confirm a PayFast notification with PayFast.");
                return false;
            }
        }

        private async Task TellTheFirmAsync(Invoice invoice, string reference)
        {
            if (!_email.IsConfigured || string.IsNullOrWhiteSpace(_smtp.ContactRecipient)) return;
            try
            {
                var client = WebUtility.HtmlEncode(invoice.Client.User.FullName);
                await _email.SendAsync(_smtp.ContactRecipient, $"Invoice #{invoice.Number} paid online",
                    $"<p><strong>{client}</strong> paid invoice #{invoice.Number} ({WebUtility.HtmlEncode(invoice.Description)}) " +
                    $"for {PricingCalculator.FormatRand(invoice.Amount)} through PayFast.</p>" +
                    $"<p>PayFast reference: {WebUtility.HtmlEncode(reference)}. The invoice has been marked paid automatically.</p>");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Payment email to the firm for invoice #{Number} failed.", invoice.Number);
            }
        }
    }
}
