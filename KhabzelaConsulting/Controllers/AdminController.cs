using Microsoft.AspNetCore.Mvc;

namespace KhabzelaConsulting.Controllers
{
    // Internal admin dashboard. Views use placeholder data until the backend is built.
    public class AdminController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Clients()
        {
            return View();
        }

        public IActionResult ClientDetail(int id)
        {
            ViewData["ClientId"] = id;
            return View();
        }

        public IActionResult PricingRules()
        {
            return View();
        }

        public IActionResult QuoteRequests()
        {
            return View();
        }

        public IActionResult Invoices()
        {
            return View();
        }
    }
}
