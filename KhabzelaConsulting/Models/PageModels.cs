namespace KhabzelaConsulting.Models
{
    // Read-only models for the client and admin pages.

    public record ActivityItem(string Title, string Meta, string Tone, string Href);

    public record ClientHomePage(string FirstName, string Initial, DateTime Today, List<KhabzelaConsulting.Services.TaxDeadline> Deadlines, List<ActivityItem> Activity);

    public record ClientDeadlineRow(int ClientId, string Client, string Return, string Detail, DateTime Due, int DaysLeft);

    public record ConsultantOption(string Id, string Initials, string Name, string Speciality);

    public record BookPage(List<DateTime> Days, string[] Times, List<ConsultantOption> Consultants, Dictionary<string, Dictionary<string, List<string>>> Booked);

    public record InvoiceRow(int Id, string Number, string Client, int ClientId, string Description, DateTime Issued, DateTime Due, decimal Amount, string Status);

    public record NotificationRow(int Id, string Icon, string Title, string? Body, string When, string Type, bool Unread);

    public record ProfilePage(string FullName, string Initial, string Email, string? Phone, string? Company, string Summary,
        bool NotifyTax, bool NotifyDeadlines, bool NotifyEmail, bool TwoFactorEnabled, string? AccountantPhone);

    public record DashboardStat(string Label, string Value, string Trend, string Href);

    public record QuoteRow(int Id, string Client, string Email, string Service, string Detail, string Estimate, string Appointment, string Status, DateTime Date);

    public record ClientRow(int Id, string Name, string Initials, string Email, string Services, string TaxStatus, int Since);

    public record HistoryItem(string Title, string Meta);

    public record ClientDetailPage(int Id, string Name, string Initials, string Email, string? Phone, string? Company, string Summary, string TaxStatus, List<HistoryItem> History,
        KhabzelaConsulting.Services.TaxProfile Profile, List<KhabzelaConsulting.Services.TaxDeadline> Deadlines);

    public record RuleRow(int Id, string Label, string Formula, decimal Min, decimal Max, string Unit, string Status);

    public record TeamRow(string Id, string Name, string Initials, string Email, string? Speciality, bool PasswordSet, bool IsYou);

    public record RuleGroup(string Name, List<RuleRow> Rules);
}
