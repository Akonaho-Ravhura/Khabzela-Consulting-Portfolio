using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

// South African formatting with "," thousands separators (e.g. R 1,450) to match the designs.
var culture = (CultureInfo)CultureInfo.GetCultureInfo("en-ZA").Clone();
culture.NumberFormat.NumberGroupSeparator = ",";
culture.NumberFormat.NumberDecimalSeparator = ".";
culture.NumberFormat.CurrencyGroupSeparator = ",";
culture.NumberFormat.CurrencyDecimalSeparator = ".";
CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;

// Add services to the container.
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
