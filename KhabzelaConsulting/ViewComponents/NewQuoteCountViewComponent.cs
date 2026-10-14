using KhabzelaConsulting.Data;
using KhabzelaConsulting.Models;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Microsoft.EntityFrameworkCore;

namespace KhabzelaConsulting.ViewComponents
{
    // Badge on the admin "Quote requests" link showing how many are still New.
    public class NewQuoteCountViewComponent : ViewComponent
    {
        private readonly AppDbContext _db;

        public NewQuoteCountViewComponent(AppDbContext db) => _db = db;

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var count = await _db.Quotes.CountAsync(q => q.Status == QuoteStatuses.New);
            return new HtmlContentViewComponentResult(count == 0
                ? HtmlString.Empty
                : new HtmlString($"<span class=\"admin-nav-count\">{count}</span>"));
        }
    }
}
