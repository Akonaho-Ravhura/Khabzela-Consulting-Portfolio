using System.Net;
using System.Text.RegularExpressions;
using KhabzelaConsulting.Data;
using KhabzelaConsulting.Models;
using KhabzelaConsulting.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KhabzelaConsulting.Tests
{
    // Pretends to be PayFast's "validate" endpoint so payment notifications can be tested offline.
    public class FakePayFastHandler : HttpMessageHandler
    {
        public string Answer { get; set; } = "VALID";

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Answer) });
    }

    public class PaymentAppFactory : AppFactory
    {
        public FakePayFastHandler PayFast { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("PayFast:MerchantId", "10000100");
            builder.UseSetting("PayFast:MerchantKey", "46f0cd694581a");
            builder.UseSetting("PayFast:Passphrase", "test-passphrase");
            builder.UseSetting("Reminders:Disabled", "true");
            builder.ConfigureServices(s => s.AddHttpClient("payfast").ConfigurePrimaryHttpMessageHandler(() => PayFast));
        }
    }

    public class PaymentTests : IClassFixture<PaymentAppFactory>
    {
        private const string Passphrase = "test-passphrase";
        private readonly PaymentAppFactory _factory;

        public PaymentTests(PaymentAppFactory factory) => _factory = factory;

        private async Task<Invoice> NewInvoiceAsync(decimal amount = 1150m)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var client = await db.Clients.Include(c => c.User).FirstAsync(c => c.User.Email == "client@demo.khabzela.co.za");
            var invoice = new Invoice
            {
                Number = (await db.Invoices.MaxAsync(i => (int?)i.Number) ?? 1000) + 1,
                ClientId = client.Id, Description = "Payroll", Amount = amount, DueDate = DateTime.UtcNow.Date.AddDays(10)
            };
            db.Invoices.Add(invoice);
            await db.SaveChangesAsync();
            return invoice;
        }

        private async Task<Invoice> ReloadAsync(int id)
        {
            using var scope = _factory.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Invoices.AsNoTracking().FirstAsync(i => i.Id == id);
        }

        private static List<KeyValuePair<string, string>> Notification(int invoiceId, string amount, string status = "COMPLETE")
        {
            var fields = new List<KeyValuePair<string, string>>
            {
                new("m_payment_id", invoiceId.ToString()), new("pf_payment_id", "1234567"), new("payment_status", status),
                new("item_name", "Khabzela Consulting invoice"), new("item_description", ""), new("amount_gross", amount),
                new("amount_fee", "-26.45"), new("amount_net", "1123.55"), new("name_first", "Demo"), new("name_last", "Client"),
                new("email_address", "client@demo.khabzela.co.za"), new("merchant_id", "10000100"),
            };
            fields.Add(new("signature", PayFastService.Signature(fields, Passphrase)));
            return fields;
        }

        private Task<HttpResponseMessage> PostNotificationAsync(IEnumerable<KeyValuePair<string, string>> fields) =>
            _factory.CreateClient().PostAsync("/Payments/PayFastNotify", new FormUrlEncodedContent(fields));

        [Fact]
        public void Signature_matches_the_payfast_algorithm()
        {
            // md5("merchant_id=10000100&amount=100.00&item_name=Test+Item&passphrase=abc")
            var fields = new[] { new KeyValuePair<string, string>("merchant_id", "10000100"), new("amount", "100.00"), new("item_name", "Test Item") };
            var expected = Convert.ToHexString(System.Security.Cryptography.MD5.HashData(
                System.Text.Encoding.UTF8.GetBytes("merchant_id=10000100&amount=100.00&item_name=Test+Item&passphrase=abc"))).ToLowerInvariant();
            Assert.Equal(expected, PayFastService.Signature(fields, "abc"));
        }

        [Fact]
        public async Task Verified_notification_marks_the_invoice_paid_and_notifies_the_client()
        {
            _factory.PayFast.Answer = "VALID";
            var invoice = await NewInvoiceAsync();

            var response = await PostNotificationAsync(Notification(invoice.Id, "1150.00"));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var saved = await ReloadAsync(invoice.Id);
            Assert.Equal(InvoiceStatuses.Paid, saved.Status);
            Assert.NotNull(saved.PaidAt);

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.True(await db.Notifications.AnyAsync(n => n.Title == $"Payment received for invoice #{invoice.Number}"));
        }

        [Fact]
        public async Task Tampered_signature_is_ignored()
        {
            _factory.PayFast.Answer = "VALID";
            var invoice = await NewInvoiceAsync();
            var fields = Notification(invoice.Id, "1150.00");
            fields[^1] = new("signature", "0000");

            await PostNotificationAsync(fields);

            Assert.Equal(InvoiceStatuses.Due, (await ReloadAsync(invoice.Id)).Status);
        }

        [Fact]
        public async Task Notification_payfast_does_not_confirm_is_ignored()
        {
            _factory.PayFast.Answer = "INVALID";
            var invoice = await NewInvoiceAsync();

            await PostNotificationAsync(Notification(invoice.Id, "1150.00"));

            Assert.Equal(InvoiceStatuses.Due, (await ReloadAsync(invoice.Id)).Status);
            _factory.PayFast.Answer = "VALID";
        }

        [Fact]
        public async Task Underpayment_is_ignored()
        {
            _factory.PayFast.Answer = "VALID";
            var invoice = await NewInvoiceAsync();

            await PostNotificationAsync(Notification(invoice.Id, "1.00"));

            Assert.Equal(InvoiceStatuses.Due, (await ReloadAsync(invoice.Id)).Status);
        }

        [Fact]
        public async Task Overdue_invoice_gets_one_automatic_reminder()
        {
            int id, number;
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var client = await db.Clients.Include(c => c.User).FirstAsync(c => c.User.Email == "client@demo.khabzela.co.za");
                var invoice = new Invoice
                {
                    Number = (await db.Invoices.MaxAsync(i => (int?)i.Number) ?? 1000) + 1,
                    ClientId = client.Id, Description = "Tax return", Amount = 900m, DueDate = DateTime.UtcNow.Date.AddDays(-5)
                };
                db.Invoices.Add(invoice);
                await db.SaveChangesAsync();
                (id, number) = (invoice.Id, invoice.Number);
            }

            for (var run = 0; run < 2; run++)
            {
                using var scope = _factory.Services.CreateScope();
                await scope.ServiceProvider.GetRequiredService<DeadlineReminderService>().SendDueRemindersAsync();
            }

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                Assert.Equal(1, await db.Notifications.CountAsync(n => n.DedupKey == $"invoice-overdue:{id}"));
                Assert.True(await db.Notifications.AnyAsync(n => n.Title == $"Invoice #{number} is overdue"));
            }
        }

        [Fact]
        public async Task Client_sees_pay_now_and_a_signed_payfast_form()
        {
            var invoice = await NewInvoiceAsync();
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            var login = await client.GetStringAsync("/Account/Login");
            var token = Regex.Match(login, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
            await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["email"] = "client@demo.khabzela.co.za", ["password"] = "Demo@2026", ["__RequestVerificationToken"] = token
            }));

            Assert.Contains($"/Client/Pay/{invoice.Id}", await client.GetStringAsync("/Client/Invoices"));
            var pay = await client.GetStringAsync($"/Client/Pay/{invoice.Id}");
            Assert.Contains("https://sandbox.payfast.co.za/eng/process", pay);
            Assert.Contains("name=\"signature\"", pay);
            Assert.Contains("value=\"1150.00\"", pay);
        }
    }
}
