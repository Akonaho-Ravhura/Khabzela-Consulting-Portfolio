using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace KhabzelaConsulting.Models
{
    // Domain model — follows the Task 1 class / ER diagrams (Client, Service, PricingRule, Quote,
    // QuoteLine, Appointment, Notification, Invoice, User). Client documents are deliberately not stored.

    public class ApplicationUser : IdentityUser
    {
        [Required, MaxLength(120)]
        public string FullName { get; set; } = "";

        // Staff only: shown to clients when they book (e.g. "Payroll & Tax").
        [MaxLength(80)]
        public string? Speciality { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public string FirstName => FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? FullName;
        public string Initials => Text.Initials(FullName);
    }

    public class Client
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = "";
        public ApplicationUser User { get; set; } = null!;

        [MaxLength(120)]
        public string? CompanyName { get; set; }

        [Required, MaxLength(30)]
        public string TaxStatus { get; set; } = TaxStatuses.NotFiled;

        public string? AccountantId { get; set; }
        public ApplicationUser? Accountant { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Tax profile — decides which SARS deadlines the client is reminded about.
        public bool IsEmployer { get; set; }                 // EMP201 monthly, EMP501 reconciliations
        public bool IsVatRegistered { get; set; }            // VAT201
        [Required, MaxLength(1)]
        public string VatCategory { get; set; } = "A";       // A/B two-monthly (odd/even months), C monthly
        public bool IsProvisionalTaxpayer { get; set; } = true; // IRP6
        public bool IsCompany { get; set; }                  // ITR14
        [Range(1, 12)]
        public int YearEndMonth { get; set; } = 2;           // financial year-end month (February by default)

        // Notification preferences
        public bool NotifyTaxStatus { get; set; } = true;
        public bool NotifyDeadlines { get; set; } = true;
        public bool NotifyByEmail { get; set; }

        public List<Quote> Quotes { get; set; } = new();
        public List<Invoice> Invoices { get; set; } = new();
        public List<Notification> Notifications { get; set; } = new();
        public List<Appointment> Appointments { get; set; } = new();
    }

    public class Service
    {
        public int Id { get; set; }

        // Stable key used by the quote calculator: "accounting", "payroll", "tax".
        [Required, MaxLength(30)]
        public string Key { get; set; } = "";

        [Required, MaxLength(60)]
        public string Name { get; set; } = "";

        [MaxLength(400)]
        public string? Description { get; set; }

        public int SortOrder { get; set; }
        public List<PricingRule> PricingRules { get; set; } = new();
    }

    public class PricingRule
    {
        public int Id { get; set; }
        public int ServiceId { get; set; }
        public Service Service { get; set; } = null!;

        // Calculator variable, e.g. "base", "per-employee", "weekly-surcharge", "turnover-lt1m".
        [Required, MaxLength(40)]
        public string Key { get; set; } = "";

        [Required, MaxLength(80)]
        public string Label { get; set; } = "";

        [Range(0, 1_000_000)]
        public decimal MinValue { get; set; }

        [Range(0, 1_000_000)]
        public decimal MaxValue { get; set; }

        // How the amount is described, e.g. "/ month" or "/ employee / month".
        [Required, MaxLength(40)]
        public string Unit { get; set; } = "/ month";

        [Required, MaxLength(10)]
        public string Status { get; set; } = RuleStatuses.Active;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string? UpdatedById { get; set; }
    }

    public class Quote
    {
        public int Id { get; set; }
        public int ClientId { get; set; }
        public Client Client { get; set; } = null!;

        // Null when the service is priced on consultation.
        public decimal? EstimateLow { get; set; }
        public decimal? EstimateHigh { get; set; }

        [Required, MaxLength(20)]
        public string Status { get; set; } = QuoteStatuses.New;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public List<QuoteLine> Lines { get; set; } = new();
        public Appointment? Appointment { get; set; }
    }

    public class QuoteLine
    {
        public int Id { get; set; }
        public int QuoteId { get; set; }
        public Quote Quote { get; set; } = null!;
        public int ServiceId { get; set; }
        public Service Service { get; set; } = null!;

        // The client's answers (employees, frequency, turnover bracket) as JSON.
        [Required, MaxLength(1000)]
        public string AnswersJson { get; set; } = "{}";

        // Human-readable version of the answers, e.g. "12 employees · monthly".
        [MaxLength(200)]
        public string Summary { get; set; } = "";
    }

    public class Appointment
    {
        public int Id { get; set; }
        public int ClientId { get; set; }
        public Client Client { get; set; } = null!;
        public int? QuoteId { get; set; }
        public Quote? Quote { get; set; }

        [Required]
        public string ConsultantId { get; set; } = "";
        public ApplicationUser Consultant { get; set; } = null!;

        // South African local time.
        public DateTime ScheduledAt { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        [Required, MaxLength(20)]
        public string Status { get; set; } = AppointmentStatuses.Booked;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class Notification
    {
        public int Id { get; set; }
        public int ClientId { get; set; }
        public Client Client { get; set; } = null!;

        // tax | deadline | invoice | appointment
        [Required, MaxLength(20)]
        public string Type { get; set; } = NotificationTypes.Tax;

        [Required, MaxLength(120)]
        public string Title { get; set; } = "";

        [MaxLength(500)]
        public string? Message { get; set; }

        // app | app+email
        [Required, MaxLength(20)]
        public string Channel { get; set; } = "app";

        public DateTime SentAt { get; set; } = DateTime.UtcNow;
        public DateTime? ReadAt { get; set; }

        // Set for automatic reminders (e.g. "deadline:EMP201:2026-10") so each is only sent once.
        [MaxLength(80)]
        public string? DedupKey { get; set; }

        public string? SentById { get; set; }
    }

    public class Invoice
    {
        public int Id { get; set; }
        public int Number { get; set; }
        public int ClientId { get; set; }
        public Client Client { get; set; } = null!;

        [Required, MaxLength(120)]
        public string Description { get; set; } = "";

        [Range(0.01, 10_000_000)]
        public decimal Amount { get; set; }

        public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
        public DateTime DueDate { get; set; }

        [Required, MaxLength(10)]
        public string Status { get; set; } = InvoiceStatuses.Due;

        public DateTime? PaidAt { get; set; }

        // "Overdue" is derived, never stored, so it can't drift out of date.
        public string DisplayStatus(DateTime today) =>
            Status == InvoiceStatuses.Paid ? InvoiceStatuses.Paid
            : DueDate.Date < today.Date ? InvoiceStatuses.Overdue
            : InvoiceStatuses.Due;
    }

    public class ContactMessage
    {
        public int Id { get; set; }

        [Required, MaxLength(120)]
        public string Name { get; set; } = "";

        [Required, MaxLength(254)]
        public string Email { get; set; } = "";

        [MaxLength(30)]
        public string? Phone { get; set; }

        [MaxLength(40)]
        public string? Service { get; set; }

        [Required, MaxLength(1000)]
        public string Message { get; set; } = "";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool EmailSent { get; set; }
    }

    public static class Roles
    {
        public const string Admin = "Admin";
        public const string Client = "Client";
    }

    public static class TaxStatuses
    {
        public const string NotFiled = "Not filed";
        public const string Submitted = "Submitted";
        public const string SarsQuery = "SARS query";
        public const string Assessed = "Assessed";
        public const string RefundDue = "Refund due";
        public static readonly string[] All = { NotFiled, Submitted, SarsQuery, Assessed, RefundDue };
    }

    public static class QuoteStatuses
    {
        public const string New = "New";
        public const string Contacted = "Contacted";
        public const string Won = "Won";
        public const string Lost = "Lost";
        public static readonly string[] All = { New, Contacted, Won, Lost };
    }

    public static class InvoiceStatuses
    {
        public const string Due = "Due";
        public const string Overdue = "Overdue";
        public const string Paid = "Paid";
    }

    public static class RuleStatuses
    {
        public const string Active = "Active";
        public const string Draft = "Draft";
        public static readonly string[] All = { Active, Draft };
    }

    public static class AppointmentStatuses
    {
        public const string Booked = "Booked";
        public const string Cancelled = "Cancelled";
    }

    public static class NotificationTypes
    {
        public const string Tax = "tax";
        public const string Deadline = "deadline";
        public const string Invoice = "invoice";
        public const string Appointment = "appointment";
    }

    public static class Text
    {
        public static string Initials(string? name)
        {
            var parts = (name ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length switch
            {
                0 => "?",
                1 => parts[0][..1].ToUpperInvariant(),
                _ => (parts[0][..1] + parts[^1][..1]).ToUpperInvariant()
            };
        }
    }
}
