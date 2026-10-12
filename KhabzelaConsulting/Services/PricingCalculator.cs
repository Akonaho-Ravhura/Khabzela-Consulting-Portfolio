using KhabzelaConsulting.Models;

namespace KhabzelaConsulting.Services
{
    public record QuoteInput(string Service, int Employees = 1, string Frequency = "monthly", string Bracket = "lt1m");

    // Low/High are null when the service can't be estimated (e.g. a required rule is in Draft).
    public record Estimate(decimal? Low, decimal? High, string Summary)
    {
        public bool HasPrice => Low.HasValue && High.HasValue;
    }

    // Turns a client's answers into an estimated monthly range using the pricing rules the owner
    // maintains in the admin portal. Pure and stateless so it can be unit tested; the server always
    // re-calculates on submit and never trusts an estimate sent from the browser.
    public static class PricingCalculator
    {
        public static readonly string[] ServiceKeys = { "accounting", "payroll", "tax" };
        public static readonly string[] Frequencies = { "weekly", "monthly", "both" };

        public static readonly IReadOnlyDictionary<string, string> Brackets = new Dictionary<string, string>
        {
            ["lt1m"] = "turnover under R1m",
            ["1to5m"] = "turnover R1m – R5m",
            ["5to20m"] = "turnover R5m – R20m",
        };

        public const int MaxEmployees = 200;

        // Returns an error message, or null when the input is valid.
        public static string? Validate(QuoteInput input)
        {
            if (!ServiceKeys.Contains(input.Service)) return "Choose a service.";
            if (input.Service == "payroll")
            {
                if (input.Employees < 1 || input.Employees > MaxEmployees) return $"Number of employees must be between 1 and {MaxEmployees}.";
                if (!Frequencies.Contains(input.Frequency)) return "Choose a pay frequency.";
            }
            else if (!Brackets.ContainsKey(input.Bracket))
            {
                return "Choose an annual turnover bracket.";
            }
            return null;
        }

        public static Estimate Calculate(QuoteInput input, IEnumerable<PricingRule> rules)
        {
            var error = Validate(input);
            if (error != null) throw new ArgumentException(error, nameof(input));

            var active = rules
                .Where(r => r.Status == RuleStatuses.Active && r.Service?.Key == input.Service)
                .ToDictionary(r => r.Key);

            if (input.Service == "payroll")
            {
                var summary = $"{input.Employees} employee{(input.Employees == 1 ? "" : "s")} · {(input.Frequency == "both" ? "weekly & monthly" : input.Frequency)}";
                if (!active.TryGetValue("per-employee", out var perEmployee)) return new Estimate(null, null, summary);

                active.TryGetValue("base", out var baseFee);
                var surcharge = input.Frequency == "monthly" ? null : active.GetValueOrDefault("weekly-surcharge");

                var low = (baseFee?.MinValue ?? 0) + perEmployee.MinValue * input.Employees + (surcharge?.MinValue ?? 0);
                var high = (baseFee?.MaxValue ?? 0) + perEmployee.MaxValue * input.Employees + (surcharge?.MaxValue ?? 0);
                return new Estimate(low, Math.Max(low, high), summary);
            }

            var bracketSummary = Brackets[input.Bracket];
            return active.TryGetValue("turnover-" + input.Bracket, out var bracket)
                ? new Estimate(bracket.MinValue, Math.Max(bracket.MinValue, bracket.MaxValue), bracketSummary)
                : new Estimate(null, null, bracketSummary);
        }

        // Shape consumed by wwwroot/js/pages/client/quote.js for the live preview.
        // Missing (draft) rules are sent as null so the preview shows "Priced on consultation".
        public static object ToClientModel(IEnumerable<PricingRule> rules)
        {
            var active = rules.Where(r => r.Status == RuleStatuses.Active).ToList();
            decimal[]? Range(string service, string key)
            {
                var r = active.FirstOrDefault(x => x.Service.Key == service && x.Key == key);
                return r == null ? null : new[] { r.MinValue, r.MaxValue };
            }
            object BracketsFor(string service) => Brackets.Keys.ToDictionary(k => k, k => Range(service, "turnover-" + k));

            return new
            {
                payroll = new
                {
                    @base = Range("payroll", "base"),
                    perEmployee = Range("payroll", "per-employee"),
                    weeklySurcharge = Range("payroll", "weekly-surcharge"),
                },
                accounting = new { brackets = BracketsFor("accounting") },
                tax = new { brackets = BracketsFor("tax") },
            };
        }

        public static string FormatRand(decimal value) => "R " + Math.Round(value).ToString("#,0", System.Globalization.CultureInfo.InvariantCulture);

        public static string FormatRange(decimal? low, decimal? high) =>
            low.HasValue && high.HasValue ? $"{FormatRand(low.Value)} – {FormatRand(high.Value)}" : "Priced on consultation";

        public static string FormatRule(PricingRule r) =>
            r.MinValue == r.MaxValue ? $"{FormatRand(r.MinValue)} {r.Unit}" : $"{FormatRand(r.MinValue)} – {FormatRand(r.MaxValue)} {r.Unit}";
    }
}
