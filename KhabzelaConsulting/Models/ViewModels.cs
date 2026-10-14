using System.ComponentModel.DataAnnotations;

namespace KhabzelaConsulting.Models
{
    // Form models. Validation attributes run server-side even when the browser's JS validation is bypassed.

    public class LoginInput
    {
        [Required(ErrorMessage = "Enter your email address."), EmailAddress(ErrorMessage = "Enter a valid email address."), MaxLength(254)]
        public string Email { get; set; } = "";

        [Required(ErrorMessage = "Enter your password."), MaxLength(128)]
        public string Password { get; set; } = "";

        public bool RememberMe { get; set; }
    }

    public class TwoFactorLoginInput
    {
        [Required(ErrorMessage = "Enter the 6-digit code."), RegularExpression(@"^\s*\d{3}\s?\d{3}\s*$", ErrorMessage = "Enter the 6-digit code from your authenticator app.")]
        public string Code { get; set; } = "";
        public bool RememberMe { get; set; }
        public bool RememberDevice { get; set; }
    }

    public class RegisterInput
    {
        [Required(ErrorMessage = "Enter your full name."), StringLength(120, MinimumLength = 2)]
        public string FullName { get; set; } = "";

        [Required(ErrorMessage = "Enter your email address."), EmailAddress(ErrorMessage = "Enter a valid email address."), MaxLength(254)]
        public string Email { get; set; } = "";

        [Required(ErrorMessage = "Enter your mobile number."), RegularExpression(@"^(\+27|0)[\d\s]{9,12}$", ErrorMessage = "Enter a valid South African mobile number.")]
        public string Phone { get; set; } = "";

        [Required(ErrorMessage = "Choose a password."), StringLength(128, MinimumLength = 8, ErrorMessage = "Use at least 8 characters.")]
        public string Password { get; set; } = "";

        [Required(ErrorMessage = "Confirm your password."), Compare(nameof(Password), ErrorMessage = "Passwords don't match.")]
        public string ConfirmPassword { get; set; } = "";

        [Range(typeof(bool), "true", "true", ErrorMessage = "Please accept the terms to continue.")]
        public bool AcceptTerms { get; set; }
    }

    public class TeamMemberInput
    {
        [Required(ErrorMessage = "Enter their full name."), StringLength(120, MinimumLength = 2)]
        public string FullName { get; set; } = "";

        [Required(ErrorMessage = "Enter their email address."), EmailAddress(ErrorMessage = "Enter a valid email address."), MaxLength(254)]
        public string Email { get; set; } = "";

        [MaxLength(80)]
        public string? Speciality { get; set; }
    }

    public class ResetPasswordInput
    {
        // True when the link came from a team invitation rather than "Forgot password".
        public bool Invite { get; set; }

        [Required, EmailAddress]
        public string Email { get; set; } = "";

        [Required]
        public string Token { get; set; } = "";

        [Required(ErrorMessage = "Choose a password."), StringLength(128, MinimumLength = 8, ErrorMessage = "Use at least 8 characters.")]
        public string Password { get; set; } = "";

        [Required(ErrorMessage = "Confirm your password."), Compare(nameof(Password), ErrorMessage = "Passwords don't match.")]
        public string ConfirmPassword { get; set; } = "";
    }

    public class ContactInput
    {
        [Required(ErrorMessage = "This field is required."), StringLength(120, MinimumLength = 2)]
        public string Name { get; set; } = "";

        [Required(ErrorMessage = "This field is required."), EmailAddress(ErrorMessage = "Enter a valid email address."), MaxLength(254)]
        public string Email { get; set; } = "";

        [RegularExpression(@"^[+\d][\d\s()-]{8,}$", ErrorMessage = "Enter a valid phone number."), MaxLength(30)]
        public string? Phone { get; set; }

        [MaxLength(40)]
        public string? Service { get; set; }

        [Required(ErrorMessage = "This field is required."), StringLength(1000, MinimumLength = 10, ErrorMessage = "Please write at least 10 characters.")]
        public string Message { get; set; } = "";

        [Range(typeof(bool), "true", "true", ErrorMessage = "Please give consent so we can reply.")]
        public bool Consent { get; set; }

        // Honeypot: hidden from people, filled in by bots.
        public string? Website { get; set; }
    }

    public class ProfileInput
    {
        [Required(ErrorMessage = "Enter your full name."), StringLength(120, MinimumLength = 2)]
        public string FullName { get; set; } = "";

        [Required(ErrorMessage = "Enter your email address."), EmailAddress(ErrorMessage = "Enter a valid email address."), MaxLength(254)]
        public string Email { get; set; } = "";

        [RegularExpression(@"^(\+27|0)[\d\s]{9,12}$", ErrorMessage = "Enter a valid South African mobile number.")]
        public string? Phone { get; set; }

        [MaxLength(120)]
        public string? Company { get; set; }
    }

    public class PreferenceInput
    {
        [Required, RegularExpression("^(tax|deadlines|email)$")]
        public string Pref { get; set; } = "";
        public bool Enabled { get; set; }
    }

    public class BookingInput
    {
        [Required]
        public string Service { get; set; } = "";

        [Range(1, 200)]
        public int Employees { get; set; } = 1;

        [MaxLength(10)]
        public string Frequency { get; set; } = "monthly";

        [MaxLength(10)]
        public string Bracket { get; set; } = "lt1m";

        [Required, RegularExpression(@"^\d{4}-\d{2}-\d{2}$")]
        public string Day { get; set; } = "";

        [Required, RegularExpression(@"^\d{2}:\d{2}$")]
        public string Time { get; set; } = "";

        [Required]
        public string ConsultantId { get; set; } = "";

        [MaxLength(500)]
        public string? Notes { get; set; }
    }

    public class TaxStatusInput
    {
        [Required]
        public string Status { get; set; } = "";

        [MaxLength(280)]
        public string? Note { get; set; }
    }

    public class PricingRuleInput
    {
        [Required, StringLength(80, MinimumLength = 2)]
        public string Label { get; set; } = "";

        [Range(0, 1_000_000, ErrorMessage = "Enter an amount between R0 and R1,000,000.")]
        public decimal MinValue { get; set; }

        [Range(0, 1_000_000, ErrorMessage = "Enter an amount between R0 and R1,000,000.")]
        public decimal MaxValue { get; set; }

        [Required]
        public string Status { get; set; } = RuleStatuses.Active;
    }

    public class InvoiceInput
    {
        [Range(1, int.MaxValue, ErrorMessage = "Choose a client.")]
        public int ClientId { get; set; }

        [Required(ErrorMessage = "Enter a description."), StringLength(120, MinimumLength = 2)]
        public string Description { get; set; } = "";

        [Range(1, 10_000_000, ErrorMessage = "Enter an amount of at least R1.")]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "Choose a due date.")]
        public DateTime? DueDate { get; set; }
    }

    public class TaxProfileInput
    {
        public bool IsEmployer { get; set; }
        public bool IsVatRegistered { get; set; }

        [Required, RegularExpression("^[ABC]$", ErrorMessage = "Choose VAT category A, B or C.")]
        public string VatCategory { get; set; } = "A";

        public bool IsProvisionalTaxpayer { get; set; }
        public bool IsCompany { get; set; }

        [Range(1, 12, ErrorMessage = "Choose the financial year-end month.")]
        public int YearEndMonth { get; set; } = 2;
    }

    public class QuoteStatusInput
    {
        [Required]
        public string Status { get; set; } = "";
    }
}
