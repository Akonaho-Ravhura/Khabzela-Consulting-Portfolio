using Microsoft.AspNetCore.Mvc;

namespace KhabzelaConsulting.Controllers
{
    // Customer-facing app (mobile-first). Views use placeholder data until the backend is built.
    public class ClientController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Quote()
        {
            return View();
        }

        public IActionResult Book()
        {
            return View();
        }

        public IActionResult Notifications()
        {
            return View();
        }

        public IActionResult Invoices()
        {
            return View();
        }

        public IActionResult Profile()
        {
            return View();
        }
    }
}
