using System.Diagnostics;
using System.Net;
using KhabzelaConsulting.Data;
using KhabzelaConsulting.Models;
using KhabzelaConsulting.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace KhabzelaConsulting.Controllers
{
    // Public (unauthenticated) marketing website.
    public class HomeController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IEmailSender _email;
        private readonly SmtpOptions _smtp;
        private readonly SiteOptions _site;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<HomeController> _logger;

        public HomeController(AppDbContext db, IEmailSender email, IOptions<SmtpOptions> smtp, IOptions<SiteOptions> site, IWebHostEnvironment env, ILogger<HomeController> logger)
        {
            _db = db;
            _email = email;
            _smtp = smtp.Value;
            _site = site.Value;
            _env = env;
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Offering()
        {
            return View();
        }

        public IActionResult About()
        {
            return View();
        }

        public IActionResult Contact()
        {
            return View();
        }

        // Contact form: saved to the database, then emailed to the owner with Reply-To set to the sender.
        [HttpPost]
        [EnableRateLimiting("forms")]
        public async Task<IActionResult> Contact(ContactInput input)
        {
            // Bots fill in the hidden "website" field; pretend it worked.
            if (!string.IsNullOrEmpty(input.Website)) return Json(new { ok = true });

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Where(e => e.Value!.Errors.Count > 0)
                    .ToDictionary(e => char.ToLowerInvariant(e.Key[0]) + e.Key[1..], e => e.Value!.Errors[0].ErrorMessage);
                return BadRequest(new { errors });
            }

            var message = new ContactMessage
            {
                Name = input.Name.Trim(),
                Email = input.Email.Trim(),
                Phone = input.Phone?.Trim(),
                Service = input.Service?.Trim(),
                Message = input.Message.Trim()
            };
            _db.ContactMessages.Add(message);
            await _db.SaveChangesAsync();

            try
            {
                string E(string? s) => WebUtility.HtmlEncode(s ?? "—");
                await _email.SendAsync(_smtp.ContactRecipient, $"Website enquiry: {message.Service ?? "General enquiry"} — {message.Name}",
                    $"<p><strong>New message from the website contact form</strong></p>" +
                    $"<p><strong>Name:</strong> {E(message.Name)}<br><strong>Email:</strong> {E(message.Email)}<br>" +
                    $"<strong>Phone:</strong> {E(message.Phone)}<br><strong>Interested in:</strong> {E(message.Service)}</p>" +
                    $"<p>{E(message.Message).Replace("\n", "<br>")}</p><p style=\"color:#777\">Reply to this email to answer {E(message.Name)} directly.</p>",
                    replyTo: message.Email);
                message.EmailSent = _email.IsConfigured;
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Contact message {MessageId} saved but the email to the owner failed.", message.Id);
            }

            // The message is already saved (staff read it under Admin → Enquiries); only report a failure
            // when email delivery is set up but didn't work.
            if (_email.IsConfigured && !message.EmailSent && !_env.IsDevelopment())
                return StatusCode(503, new { error = $"Sorry — we couldn't send your message right now. Please call us on 072 879 2715 or email {_site.ContactEmail}." });

            return Json(new { ok = true });
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [Route("Home/Status/{code:int}")]
        public IActionResult Status(int code)
        {
            ViewData["Code"] = code;
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        // Offline fallback page for the installable app (served by the service worker).
        public IActionResult Offline()
        {
            return View();
        }
    }
}
