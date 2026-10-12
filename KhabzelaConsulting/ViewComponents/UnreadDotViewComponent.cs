using KhabzelaConsulting.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Microsoft.EntityFrameworkCore;
using KhabzelaConsulting.Models;

namespace KhabzelaConsulting.ViewComponents
{
    // Red dot on the Notifications tab when the signed-in client has unread notifications.
    public class UnreadDotViewComponent : ViewComponent
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _users;

        public UnreadDotViewComponent(AppDbContext db, UserManager<ApplicationUser> users)
        {
            _db = db;
            _users = users;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var userId = _users.GetUserId(UserClaimsPrincipal);
            var unread = userId != null && await _db.Notifications.AnyAsync(n => n.Client.UserId == userId && n.ReadAt == null);
            return new HtmlContentViewComponentResult(new Microsoft.AspNetCore.Html.HtmlString(
                $"<span class=\"tab-dot\" data-unread-dot{(unread ? "" : " hidden")}></span>"));
        }
    }
}
