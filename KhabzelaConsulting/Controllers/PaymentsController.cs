using KhabzelaConsulting.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KhabzelaConsulting.Controllers
{
    // Server-to-server callbacks from the payment gateway. PayFast posts here when a payment completes;
    // every notification is verified (signature, PayFast confirmation, amount) before an invoice is marked paid.
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    public class PaymentsController : Controller
    {
        private readonly PayFastService _payFast;
        private readonly ILogger<PaymentsController> _logger;

        public PaymentsController(PayFastService payFast, ILogger<PaymentsController> logger)
        {
            _payFast = payFast;
            _logger = logger;
        }

        [HttpPost]
        // No action parameters on purpose: model binding would read the form first and lose the field order.
        public async Task<IActionResult> PayFastNotify()
        {
            var ct = HttpContext.RequestAborted;
            if (!Request.HasFormContentType) return BadRequest();
            // Read the raw body: the signature depends on the fields' original order.
            using var reader = new StreamReader(Request.Body);
            var posted = ParseForm(await reader.ReadToEndAsync(ct));

            var result = await _payFast.HandleNotificationAsync(posted, ct);
            _logger.LogInformation("PayFast notification for payment {PaymentId}: {Result}.",
                posted.FirstOrDefault(p => p.Key == "m_payment_id").Value, result);
            // PayFast only needs a 200 to stop retrying; the outcome is in the log.
            return Ok();
        }

        public static List<KeyValuePair<string, string>> ParseForm(string body) =>
            body.Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Select(pair => pair.Split('=', 2))
                .Select(kv => new KeyValuePair<string, string>(Decode(kv[0]), kv.Length > 1 ? Decode(kv[1]) : ""))
                .ToList();

        private static string Decode(string value) => Uri.UnescapeDataString(value.Replace('+', ' '));
    }
}
