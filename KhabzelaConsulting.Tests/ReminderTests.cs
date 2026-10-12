using KhabzelaConsulting.Data;
using KhabzelaConsulting.Models;
using KhabzelaConsulting.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KhabzelaConsulting.Tests
{
    public class FixedTime : TimeProvider
    {
        private readonly DateTimeOffset _now;
        public FixedTime(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
    }

    // The demo client is an employer, so on 1 Nov 2026 the EMP201 due 6 Nov is inside the 7-day window.
    public class ReminderFactory : AppFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("Reminders:Disabled", "true");
            builder.ConfigureServices(s =>
            {
                s.RemoveAll<TimeProvider>();
                s.AddSingleton<TimeProvider>(new FixedTime(new DateTimeOffset(2026, 11, 1, 8, 0, 0, TimeSpan.Zero)));
            });
        }
    }

    public class ReminderTests : IClassFixture<ReminderFactory>
    {
        private readonly ReminderFactory _factory;
        public ReminderTests(ReminderFactory factory) => _factory = factory;

        [Fact]
        public async Task Reminders_are_sent_once_per_deadline()
        {
            using var scope = _factory.Services.CreateScope();
            var reminders = scope.ServiceProvider.GetRequiredService<DeadlineReminderService>();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var first = await reminders.SendDueRemindersAsync();
            var second = await reminders.SendDueRemindersAsync();

            Assert.True(first > 0);
            Assert.Equal(0, second);

            var demo = await db.Clients.Include(c => c.User).SingleAsync(c => c.User.Email == DbSeeder.DemoClientEmail);
            var emp201 = await db.Notifications.Where(n => n.ClientId == demo.Id && n.DedupKey == "deadline:EMP201:2026-10").ToListAsync();
            Assert.Single(emp201);
            Assert.Equal(NotificationTypes.Deadline, emp201[0].Type);
            Assert.Contains("due in 5 days", emp201[0].Title);
        }

        [Fact]
        public async Task Clients_without_matching_returns_get_no_reminders()
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var naledi = await db.Clients.Include(c => c.User).SingleAsync(c => c.User.Email == "naledi@demo.khabzela.co.za");

            await scope.ServiceProvider.GetRequiredService<DeadlineReminderService>().SendDueRemindersAsync(naledi.Id);

            Assert.False(await db.Notifications.AnyAsync(n => n.ClientId == naledi.Id && n.DedupKey != null));
        }
    }
}
