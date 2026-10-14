using System.Net;
using System.Text;
using KhabzelaConsulting.Data;
using KhabzelaConsulting.Models;
using KhabzelaConsulting.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using QRCoder;

namespace KhabzelaConsulting.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _users;
        private readonly SignInManager<ApplicationUser> _signIn;
        private readonly AppDbContext _db;
        private readonly IEmailSender _email;
        private readonly ILogger<AccountController> _logger;

        public AccountController(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn, AppDbContext db,
            IEmailSender email, ILogger<AccountController> logger)
        {
            _users = users;
            _signIn = signIn;
            _db = db;
            _email = email;
            _logger = logger;
        }

        // portal=team shows the staff sign-in; it's also used when someone is sent here from an /Admin page.
        public IActionResult Login(string? returnUrl, string? portal)
        {
            var team = IsTeamPortal(portal, returnUrl);
            if (User.Identity?.IsAuthenticated == true)
            {
                if (team && !User.IsInRole(Roles.Admin)) return RedirectToAction(nameof(AccessDenied));
                return RedirectByRole(User.IsInRole(Roles.Admin), returnUrl);
            }
            ViewData["ReturnUrl"] = returnUrl;
            ViewData["Team"] = team;
            return View(new LoginInput());
        }

        [HttpPost]
        [EnableRateLimiting("forms")]
        public async Task<IActionResult> Login(LoginInput input, string? returnUrl, string? portal)
        {
            var team = IsTeamPortal(portal, returnUrl);
            ViewData["ReturnUrl"] = returnUrl;
            ViewData["Team"] = team;
            if (!ModelState.IsValid) return View(input);

            var user = await _users.FindByEmailAsync(input.Email.Trim());
            if (user == null)
            {
                ModelState.AddModelError("", "Incorrect email or password.");
                return View(input);
            }

            var result = await _signIn.PasswordSignInAsync(user, input.Password, input.RememberMe, lockoutOnFailure: true);
            if (result.RequiresTwoFactor)
                return RedirectToAction(nameof(LoginWith2fa), new { returnUrl, rememberMe = input.RememberMe });
            if (result.IsLockedOut)
            {
                _logger.LogWarning("Account {UserId} locked out after failed sign-ins.", user.Id);
                ModelState.AddModelError("", "Too many failed attempts. Your account is locked for 15 minutes.");
                return View(input);
            }
            if (!result.Succeeded)
            {
                ModelState.AddModelError("", "Incorrect email or password.");
                return View(input);
            }

            var isAdmin = await _users.IsInRoleAsync(user, Roles.Admin);
            if (team && !isAdmin)
            {
                await _signIn.SignOutAsync();
                ModelState.AddModelError("", "This is a client account. Use the client sign-in instead.");
                return View(input);
            }
            return RedirectByRole(isAdmin, returnUrl);
        }

        private static bool IsTeamPortal(string? portal, string? returnUrl) =>
            string.Equals(portal, "team", StringComparison.OrdinalIgnoreCase)
            || (returnUrl?.StartsWith("/Admin", StringComparison.OrdinalIgnoreCase) ?? false);

        public async Task<IActionResult> LoginWith2fa(string? returnUrl, bool rememberMe)
        {
            if (await _signIn.GetTwoFactorAuthenticationUserAsync() == null) return RedirectToAction(nameof(Login));
            ViewData["ReturnUrl"] = returnUrl;
            return View(new TwoFactorLoginInput { RememberMe = rememberMe });
        }

        [HttpPost]
        [EnableRateLimiting("forms")]
        public async Task<IActionResult> LoginWith2fa(TwoFactorLoginInput input, string? returnUrl)
        {
            ViewData["ReturnUrl"] = returnUrl;
            var user = await _signIn.GetTwoFactorAuthenticationUserAsync();
            if (user == null) return RedirectToAction(nameof(Login));
            if (!ModelState.IsValid) return View(input);

            var code = input.Code.Replace(" ", "").Trim();
            var result = await _signIn.TwoFactorAuthenticatorSignInAsync(code, input.RememberMe, input.RememberDevice);
            if (result.IsLockedOut)
            {
                ModelState.AddModelError("", "Too many failed attempts. Your account is locked for 15 minutes.");
                return View(input);
            }
            if (!result.Succeeded)
            {
                ModelState.AddModelError(nameof(input.Code), "That code didn't work. Check your authenticator app and try again.");
                return View(input);
            }
            return RedirectByRole(await _users.IsInRoleAsync(user, Roles.Admin), returnUrl);
        }

        public IActionResult Register()
        {
            if (User.Identity?.IsAuthenticated == true) return RedirectByRole(User.IsInRole(Roles.Admin), null);
            return View(new RegisterInput());
        }

        [HttpPost]
        [EnableRateLimiting("forms")]
        public async Task<IActionResult> Register(RegisterInput input)
        {
            if (!ModelState.IsValid) return View(input);

            var user = new ApplicationUser
            {
                UserName = input.Email.Trim(),
                Email = input.Email.Trim(),
                FullName = input.FullName.Trim(),
                PhoneNumber = input.Phone.Trim(),
            };

            var result = await _users.CreateAsync(user, input.Password);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    var key = error.Code.StartsWith("Password") ? nameof(input.Password)
                        : error.Code.Contains("Email") || error.Code.Contains("UserName") ? nameof(input.Email) : "";
                    var message = error.Code is "DuplicateEmail" or "DuplicateUserName"
                        ? "An account with this email already exists. Try signing in instead."
                        : error.Description;
                    if (!ModelState[key]?.Errors.Any(e => e.ErrorMessage == message) ?? true)
                        ModelState.AddModelError(key, message);
                }
                return View(input);
            }

            await _users.AddToRoleAsync(user, Roles.Client);

            // New clients are assigned to the firm's first accountant until the owner reassigns them.
            var accountant = (await _users.GetUsersInRoleAsync(Roles.Admin)).OrderBy(u => u.CreatedAt).FirstOrDefault();
            _db.Clients.Add(new Client { UserId = user.Id, AccountantId = accountant?.Id });
            await _db.SaveChangesAsync();

            await _signIn.SignInAsync(user, isPersistent: false);
            TempData["Toast"] = $"Welcome, {user.FirstName}! Your account is ready.";
            return RedirectToAction("Index", "Client");
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await _signIn.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        public IActionResult ForgotPassword() => View();

        [HttpPost]
        [EnableRateLimiting("forms")]
        public async Task<IActionResult> ForgotPassword([FromForm] string? email)
        {
            if (string.IsNullOrWhiteSpace(email) || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email))
            {
                ModelState.AddModelError("email", "Enter a valid email address.");
                return View();
            }

            // Same response whether or not the account exists, so emails can't be enumerated.
            var user = await _users.FindByEmailAsync(email.Trim());
            if (user != null)
            {
                var token = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(await _users.GeneratePasswordResetTokenAsync(user)));
                var link = Url.Action(nameof(ResetPassword), "Account", new { email = user.Email, token }, Request.Scheme)!;
                try
                {
                    await _email.SendAsync(user.Email!, "Reset your Khabzela Consulting password",
                        $"<p>Hi {WebUtility.HtmlEncode(user.FirstName)},</p><p>Use the link below to choose a new password. It expires in 30 minutes.</p>" +
                        $"<p><a href=\"{WebUtility.HtmlEncode(link)}\">Reset my password</a></p><p>If you didn't ask for this, you can ignore this email.</p>");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Password reset email to user {UserId} failed.", user.Id);
                }
            }

            ViewData["SentTo"] = email.Trim();
            return View();
        }

        public IActionResult ResetPassword(string? email, string? token, bool invite = false)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(token)) return RedirectToAction(nameof(ForgotPassword));
            return View(new ResetPasswordInput { Email = email, Token = token, Invite = invite });
        }

        [HttpPost]
        [EnableRateLimiting("forms")]
        public async Task<IActionResult> ResetPassword(ResetPasswordInput input)
        {
            if (!ModelState.IsValid) return View(input);

            var user = await _users.FindByEmailAsync(input.Email);
            if (user != null)
            {
                string token;
                try { token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(input.Token)); }
                catch (FormatException) { token = ""; }

                IdentityResult result;
                if (input.Invite)
                {
                    // Team invitation: the account has no password yet.
                    var valid = !await _users.HasPasswordAsync(user)
                        && await _users.VerifyUserTokenAsync(user, InviteTokenProvider.ProviderName, InviteTokenProvider.Purpose, token);
                    result = valid
                        ? await _users.AddPasswordAsync(user, input.Password)
                        : IdentityResult.Failed(new IdentityError { Code = "InvalidToken", Description = "Invalid token." });
                    if (result.Succeeded)
                    {
                        user.EmailConfirmed = true;
                        await _users.UpdateAsync(user);
                    }
                }
                else
                {
                    result = await _users.ResetPasswordAsync(user, token, input.Password);
                }
                if (!result.Succeeded)
                {
                    var expired = result.Errors.Any(e => e.Code == "InvalidToken");
                    foreach (var e in result.Errors)
                        ModelState.AddModelError(expired ? "" : nameof(input.Password),
                            expired ? (input.Invite ? "This invitation has expired or was already used. Ask an administrator to send a new one."
                                               : "This reset link has expired or was already used. Request a new one.") : e.Description);
                    return View(input);
                }
                await _users.ResetAccessFailedCountAsync(user);
            }

            TempData["Toast"] = input.Invite ? "Password set — sign in to the admin portal." : "Password updated — please sign in.";
            return input.Invite ? RedirectToAction(nameof(Login), new { portal = "team" }) : RedirectToAction(nameof(Login));
        }

        public IActionResult AccessDenied() => View();

        // ----- Two-factor authentication (authenticator app) -----

        [Authorize]
        public async Task<IActionResult> TwoFactor()
        {
            var user = await _users.GetUserAsync(User);
            if (user == null) return Challenge();

            ViewData["Enabled"] = user.TwoFactorEnabled;
            if (!user.TwoFactorEnabled)
            {
                var key = await _users.GetAuthenticatorKeyAsync(user);
                if (string.IsNullOrEmpty(key))
                {
                    await _users.ResetAuthenticatorKeyAsync(user);
                    key = await _users.GetAuthenticatorKeyAsync(user);
                }
                var uri = $"otpauth://totp/{Uri.EscapeDataString("Khabzela Consulting")}:{Uri.EscapeDataString(user.Email!)}?secret={key}&issuer={Uri.EscapeDataString("Khabzela Consulting")}&digits=6";
                using var qr = QRCodeGenerator.GenerateQrCode(uri, QRCodeGenerator.ECCLevel.Q);
                ViewData["QrSvg"] = new SvgQRCode(qr).GetGraphic(4);
                ViewData["Key"] = string.Join(" ", key!.Chunk(4).Select(c => new string(c))).ToLowerInvariant();
            }
            ViewData["BackUrl"] = User.IsInRole(Roles.Admin) ? Url.Action("Index", "Admin") : Url.Action("Profile", "Client");
            return View();
        }

        [Authorize, HttpPost]
        [EnableRateLimiting("forms")]
        public async Task<IActionResult> EnableTwoFactor([FromForm] string? code)
        {
            var user = await _users.GetUserAsync(User);
            if (user == null) return Challenge();

            var clean = (code ?? "").Replace(" ", "").Trim();
            var valid = clean.Length == 6 && await _users.VerifyTwoFactorTokenAsync(user, _users.Options.Tokens.AuthenticatorTokenProvider, clean);
            if (!valid)
            {
                TempData["Error"] = "That code didn't match. Check the time on your phone and try again.";
                return RedirectToAction(nameof(TwoFactor));
            }

            await _users.SetTwoFactorEnabledAsync(user, true);
            TempData["Toast"] = "Two-factor authentication is on.";
            return RedirectToAction(nameof(TwoFactor));
        }

        [Authorize, HttpPost]
        public async Task<IActionResult> DisableTwoFactor()
        {
            var user = await _users.GetUserAsync(User);
            if (user == null) return Challenge();

            await _users.SetTwoFactorEnabledAsync(user, false);
            await _users.ResetAuthenticatorKeyAsync(user);
            await _signIn.RefreshSignInAsync(user);
            TempData["Toast"] = "Two-factor authentication is off.";
            return RedirectToAction(nameof(TwoFactor));
        }

        private IActionResult RedirectByRole(bool isAdmin, string? returnUrl)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) return LocalRedirect(returnUrl);
            return isAdmin ? RedirectToAction("Index", "Admin") : RedirectToAction("Index", "Client");
        }
    }
}
