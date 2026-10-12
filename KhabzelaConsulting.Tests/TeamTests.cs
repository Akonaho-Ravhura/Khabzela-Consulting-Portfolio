using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace KhabzelaConsulting.Tests
{
    public class TeamTests : IClassFixture<AppFactory>
    {
        private readonly AppFactory _factory;
        public TeamTests(AppFactory factory) => _factory = factory;

        private HttpClient NewClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        private static string FormToken(string html) =>
            Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        private static string MetaToken(string html) =>
            Regex.Match(html, "name=\"csrf-token\" content=\"([^\"]+)\"").Groups[1].Value;

        private static async Task<HttpResponseMessage> Login(HttpClient c, string email, string password)
        {
            var page = await c.GetStringAsync("/Account/Login?portal=team");
            return await c.PostAsync("/Account/Login?portal=team", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["email"] = email, ["password"] = password, ["__RequestVerificationToken"] = FormToken(page)
            }));
        }

        private static async Task<HttpResponseMessage> PostJson(HttpClient c, string pageUrl, string url, Dictionary<string, string> data)
        {
            var token = MetaToken(await c.GetStringAsync(pageUrl));
            var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = new FormUrlEncodedContent(data) };
            request.Headers.Add("RequestVerificationToken", token);
            return await c.SendAsync(request);
        }

        [Fact]
        public async Task Invited_team_member_sets_a_password_and_signs_in()
        {
            var admin = NewClient();
            await Login(admin, "admin@demo.khabzela.co.za", "Demo@2026");

            var invite = await PostJson(admin, "/Admin/Team", "/Admin/InviteTeamMember", new()
            {
                ["fullName"] = "New Accountant", ["email"] = "new.accountant@example.com", ["speciality"] = "Payroll"
            });
            Assert.Equal(HttpStatusCode.OK, invite.StatusCode);
            var link = JsonDocument.Parse(await invite.Content.ReadAsStringAsync()).RootElement.GetProperty("link").GetString()!;
            var path = new Uri(link).PathAndQuery;

            // Before accepting, they can't sign in.
            Assert.Equal(HttpStatusCode.OK, (await Login(NewClient(), "new.accountant@example.com", "Welcome@2026")).StatusCode);

            var guest = NewClient();
            var setup = await guest.GetStringAsync(path);
            Assert.Contains("Welcome to the team", setup);
            var token = Regex.Match(setup, "name=\"token\" value=\"([^\"]+)\"").Groups[1].Value;
            var accept = await guest.PostAsync("/Account/ResetPassword", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["email"] = "new.accountant@example.com", ["token"] = token, ["invite"] = "true",
                ["password"] = "Welcome@2026", ["confirmPassword"] = "Welcome@2026",
                ["__RequestVerificationToken"] = FormToken(setup)
            }));
            Assert.Equal(HttpStatusCode.Redirect, accept.StatusCode);

            var signedIn = await Login(NewClient(), "new.accountant@example.com", "Welcome@2026");
            Assert.Equal("/Admin", signedIn.Headers.Location!.ToString());

            // The link only works once.
            var again = await guest.PostAsync("/Account/ResetPassword", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["email"] = "new.accountant@example.com", ["token"] = token, ["invite"] = "true",
                ["password"] = "Other@2026x", ["confirmPassword"] = "Other@2026x",
                ["__RequestVerificationToken"] = FormToken(await guest.GetStringAsync(path))
            }));
            Assert.Contains("invitation has expired or was already used", await again.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task Client_email_cannot_be_invited_as_admin()
        {
            var admin = NewClient();
            await Login(admin, "admin@demo.khabzela.co.za", "Demo@2026");
            var response = await PostJson(admin, "/Admin/Team", "/Admin/InviteTeamMember", new()
            {
                ["fullName"] = "Demo Client", ["email"] = "client@demo.khabzela.co.za"
            });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Clients_cannot_open_the_team_page()
        {
            var client = NewClient();
            var page = await client.GetStringAsync("/Account/Login");
            await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["email"] = "client@demo.khabzela.co.za", ["password"] = "Demo@2026", ["__RequestVerificationToken"] = FormToken(page)
            }));
            var team = await client.GetAsync("/Admin/Team");
            Assert.Contains("/Account/AccessDenied", team.Headers.Location!.ToString());
        }
    }
}
