using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using KhabzelaConsulting.Data;
using KhabzelaConsulting.Models;
using KhabzelaConsulting.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// South African formatting with "," thousands separators (e.g. R 1,450) to match the designs.
var culture = (CultureInfo)CultureInfo.GetCultureInfo("en-ZA").Clone();
culture.NumberFormat.NumberGroupSeparator = ",";
culture.NumberFormat.NumberDecimalSeparator = ".";
culture.NumberFormat.CurrencyGroupSeparator = ",";
culture.NumberFormat.CurrencyDecimalSeparator = ".";
CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;

// Database: Azure SQL in production (EF Core migrations); a local SQLite file for development.
var provider = builder.Configuration["Database:Provider"] ?? "SqlServer";
var useSqlite = provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase);
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");
builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (useSqlite) options.UseSqlite(connectionString);
    else options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(5));
});

// Authentication: ASP.NET Core Identity (hashed passwords, lockout, optional authenticator-app 2FA).
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = false;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders()
    .AddTokenProvider<InviteTokenProvider>(InviteTokenProvider.ProviderName)
    .AddClaimsPrincipalFactory<AppClaimsPrincipalFactory>();

builder.Services.Configure<DataProtectionTokenProviderOptions>(o => o.TokenLifespan = TimeSpan.FromMinutes(30));

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.SlidingExpiration = true;
});

// Every POST/PUT/DELETE must carry an anti-forgery token (forms and fetch calls).
builder.Services.AddControllersWithViews(o => o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddAntiforgery(o => o.HeaderName = "RequestVerificationToken");

// Slow down password guessing and contact-form spam.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("forms", http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(5) }));
});

builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));
builder.Services.Configure<SeedOptions>(builder.Configuration.GetSection("Seed"));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<SaClock>();
builder.Services.Configure<AcsEmailOptions>(builder.Configuration.GetSection("AzureEmail"));
builder.Services.Configure<SiteOptions>(builder.Configuration.GetSection("Site"));
// Email: Azure Communication Services when configured (Azure deployment), otherwise SMTP (Afrihost mailbox).
if (!string.IsNullOrWhiteSpace(builder.Configuration["AzureEmail:ConnectionString"]))
    builder.Services.AddScoped<IEmailSender, AcsEmailSender>();
else
    builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<DeadlineReminderService>();
if (!builder.Configuration.GetValue<bool>("Reminders:Disabled"))
    builder.Services.AddHostedService<DeadlineReminderJob>();

var app = builder.Build();

// Create / migrate the database and seed roles, services, pricing rules and (optionally) demo data.
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var seed = scope.ServiceProvider.GetRequiredService<IOptions<SeedOptions>>().Value;
    await DbSeeder.SeedAsync(scope.ServiceProvider, seed, useMigrations: !useSqlite, logger);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/Home/Status/{0}");

app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    await next();
});

app.UseHttpsRedirection();
app.UseRouting();
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();

// Adds the user's full name as a claim so layouts can greet them without a database call.
public class AppClaimsPrincipalFactory : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>
{
    public AppClaimsPrincipalFactory(UserManager<ApplicationUser> users, RoleManager<IdentityRole> roles, IOptions<IdentityOptions> options)
        : base(users, roles, options) { }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        identity.AddClaim(new Claim("full_name", user.FullName));
        return identity;
    }
}

public partial class Program { }
