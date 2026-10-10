using Microsoft.AspNetCore.Mvc;

namespace KhabzelaConsulting.Controllers
{
    public class AccountController : Controller
    {
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Login(string email, string password, string? returnUrl)
        {
            // TODO (backend): authenticate and route by role (client vs staff).
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }
            return RedirectToAction("Index", "Client");
        }

        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Register(string fullName, string email, string phone, string password)
        {
            // TODO (backend): create the client account.
            return RedirectToAction("Index", "Client");
        }

        public IActionResult ForgotPassword()
        {
            return View();
        }
    }
}
