using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace KhabzelaConsulting.Tests
{
    // Boots the real app against a throwaway SQLite database with demo data.
    public class AppFactory : WebApplicationFactory<Program>
    {
        private readonly string _db = Path.Combine(Path.GetTempPath(), $"khabzela-test-{Guid.NewGuid():N}.db");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("Database:Provider", "Sqlite");
            builder.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={_db}");
            builder.UseSetting("Seed:DemoData", "true");
            builder.UseSetting("Seed:DemoPassword", "Demo@2026");
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            try { File.Delete(_db); } catch (IOException) { }
        }
    }

    public class WebAppTests : IClassFixture<AppFactory>
    {
        private readonly AppFactory _factory;

        public WebAppTests(AppFactory factory) => _factory = factory;

        private HttpClient NewClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        private static string Token(string html) =>
            Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;

        private static async Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password, string url = "/Account/Login")
        {
            var page = await client.GetStringAsync(url);
            return await client.PostAsync(url, new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["email"] = email, ["password"] = password, ["__RequestVerificationToken"] = Token(page)
            }));
        }

        [Theory]
        [InlineData("/")]
        [InlineData("/Home/About")]
        [InlineData("/Home/Contact")]
        [InlineData("/Home/Privacy")]
        [InlineData("/Account/Login")]
        [InlineData("/Account/Register")]
        [InlineData("/manifest.webmanifest")]
        public async Task Public_pages_load(string url)
        {
            var response = await NewClient().GetAsync(url);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task Public_site_shows_owner_details_and_no_insurance()
        {
            var html = await NewClient().GetStringAsync("/");
            Assert.Contains("072 879 2715", html);
            Assert.Contains("vukani@khabzelaconsulting.co.za", html);
            Assert.DoesNotContain("Insurance", html);
            Assert.Contains("Know your tax status before it becomes a problem", html);
        }

        [Theory]
        [InlineData("/Client")]
        [InlineData("/Admin")]
        [InlineData("/Admin/PricingRules")]
        public async Task Portals_require_sign_in(string url)
        {
            var response = await NewClient().GetAsync(url);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("/Account/Login", response.Headers.Location!.ToString());
        }

        [Fact]
        public async Task Posts_without_antiforgery_token_are_rejected()
        {
            var response = await NewClient().PostAsync("/Home/Contact", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["name"] = "Bot", ["email"] = "bot@example.com", ["message"] = "Hello there friend", ["consent"] = "true"
            }));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Wrong_password_does_not_sign_in()
        {
            var response = await LoginAsync(NewClient(), "client@demo.khabzela.co.za", "not-the-password");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode); // form shown again
            Assert.Contains("Incorrect email or password", await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task Client_signs_in_and_cannot_open_admin()
        {
            var client = NewClient();
            var login = await LoginAsync(client, "client@demo.khabzela.co.za", "Demo@2026");
            Assert.Equal("/Client", login.Headers.Location!.ToString());

            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Client/Notifications")).StatusCode);
            var admin = await client.GetAsync("/Admin");
            Assert.Contains("/Account/AccessDenied", admin.Headers.Location!.ToString());
        }

        [Fact]
        public async Task Admin_signs_in_to_the_admin_portal()
        {
            var client = NewClient();
            var login = await LoginAsync(client, "admin@demo.khabzela.co.za", "Demo@2026");
            Assert.Equal("/Admin", login.Headers.Location!.ToString());
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Admin/Clients")).StatusCode);
        }

        [Fact]
        public async Task Admin_portal_button_opens_team_sign_in()
        {
            var html = await NewClient().GetStringAsync("/Account/Login?portal=team");
            Assert.Contains("Team sign in", html);
            Assert.DoesNotContain("Create an account", html);
        }

        [Fact]
        public async Task Team_sign_in_admits_admins()
        {
            var login = await LoginAsync(NewClient(), "admin@demo.khabzela.co.za", "Demo@2026", "/Account/Login?portal=team");
            Assert.Equal("/Admin", login.Headers.Location!.ToString());
        }

        [Fact]
        public async Task Team_sign_in_refuses_client_accounts()
        {
            var client = NewClient();
            var login = await LoginAsync(client, "client@demo.khabzela.co.za", "Demo@2026", "/Account/Login?portal=team");

            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            Assert.Contains("This is a client account", await login.Content.ReadAsStringAsync());
            Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Client")).StatusCode); // not left signed in
        }

        [Fact]
        public async Task Contact_form_validates_on_the_server()
        {
            var client = NewClient();
            var page = await client.GetStringAsync("/Home/Contact");
            var response = await client.PostAsync("/Home/Contact", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["name"] = "", ["email"] = "not-an-email", ["message"] = "short", ["consent"] = "false",
                ["__RequestVerificationToken"] = Token(page)
            }));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            Assert.Contains("\"email\"", body);
            Assert.Contains("\"consent\"", body);
        }
    }
}
