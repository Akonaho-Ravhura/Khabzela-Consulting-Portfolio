using KhabzelaConsulting.Models;
using KhabzelaConsulting.Services;

namespace KhabzelaConsulting.Tests
{
    public class PricingCalculatorTests
    {
        private static readonly Service Payroll = new() { Id = 1, Key = "payroll", Name = "Payroll" };
        private static readonly Service Accounting = new() { Id = 2, Key = "accounting", Name = "Accounting" };
        private static readonly Service Tax = new() { Id = 3, Key = "tax", Name = "Tax" };

        private static List<PricingRule> DefaultRules() => new()
        {
            new() { Service = Payroll, Key = "base", MinValue = 250, MaxValue = 250 },
            new() { Service = Payroll, Key = "per-employee", MinValue = 50, MaxValue = 100 },
            new() { Service = Payroll, Key = "weekly-surcharge", MinValue = 200, MaxValue = 200 },
            new() { Service = Accounting, Key = "turnover-lt1m", MinValue = 900, MaxValue = 1400 },
            new() { Service = Accounting, Key = "turnover-1to5m", MinValue = 1400, MaxValue = 2200 },
            new() { Service = Tax, Key = "turnover-5to20m", MinValue = 1500, MaxValue = 2400 },
        };

        [Fact]
        public void Payroll_monthly_uses_base_plus_per_employee()
        {
            var estimate = PricingCalculator.Calculate(new QuoteInput("payroll", 12, "monthly"), DefaultRules());

            Assert.Equal(850m, estimate.Low);   // 250 + 50 × 12
            Assert.Equal(1450m, estimate.High); // 250 + 100 × 12
            Assert.Equal("12 employees · monthly", estimate.Summary);
        }

        [Theory]
        [InlineData("weekly")]
        [InlineData("both")]
        public void Payroll_weekly_runs_add_the_surcharge(string frequency)
        {
            var estimate = PricingCalculator.Calculate(new QuoteInput("payroll", 12, frequency), DefaultRules());

            Assert.Equal(1050m, estimate.Low);
            Assert.Equal(1650m, estimate.High);
        }

        [Fact]
        public void Single_employee_summary_is_singular()
        {
            var estimate = PricingCalculator.Calculate(new QuoteInput("payroll", 1), DefaultRules());
            Assert.Equal("1 employee · monthly", estimate.Summary);
        }

        [Theory]
        [InlineData("accounting", "lt1m", 900, 1400)]
        [InlineData("accounting", "1to5m", 1400, 2200)]
        [InlineData("tax", "5to20m", 1500, 2400)]
        public void Turnover_services_use_the_matching_bracket(string service, string bracket, decimal low, decimal high)
        {
            var estimate = PricingCalculator.Calculate(new QuoteInput(service, Bracket: bracket), DefaultRules());

            Assert.Equal(low, estimate.Low);
            Assert.Equal(high, estimate.High);
        }

        [Fact]
        public void Draft_rule_means_priced_on_consultation()
        {
            var rules = DefaultRules();
            rules.Single(r => r.Key == "turnover-lt1m").Status = RuleStatuses.Draft;

            var estimate = PricingCalculator.Calculate(new QuoteInput("accounting", Bracket: "lt1m"), rules);

            Assert.False(estimate.HasPrice);
            Assert.Equal("Priced on consultation", PricingCalculator.FormatRange(estimate.Low, estimate.High));
        }

        [Fact]
        public void Draft_per_employee_rule_disables_payroll_estimate()
        {
            var rules = DefaultRules();
            rules.Single(r => r.Key == "per-employee").Status = RuleStatuses.Draft;

            Assert.False(PricingCalculator.Calculate(new QuoteInput("payroll", 5), rules).HasPrice);
        }

        [Fact]
        public void Draft_surcharge_is_simply_left_out()
        {
            var rules = DefaultRules();
            rules.Single(r => r.Key == "weekly-surcharge").Status = RuleStatuses.Draft;

            var estimate = PricingCalculator.Calculate(new QuoteInput("payroll", 12, "weekly"), rules);
            Assert.Equal(850m, estimate.Low);
        }

        [Fact]
        public void Rules_for_other_services_are_ignored()
        {
            var rules = DefaultRules();
            rules.Add(new PricingRule { Service = Tax, Key = "per-employee", MinValue = 9999, MaxValue = 9999 });

            Assert.Equal(850m, PricingCalculator.Calculate(new QuoteInput("payroll", 12), rules).Low);
        }

        [Theory]
        [InlineData("insurance", 1, "monthly", "lt1m", "Choose a service.")]
        [InlineData("payroll", 0, "monthly", "lt1m", "Number of employees must be between 1 and 200.")]
        [InlineData("payroll", 201, "monthly", "lt1m", "Number of employees must be between 1 and 200.")]
        [InlineData("payroll", 5, "daily", "lt1m", "Choose a pay frequency.")]
        [InlineData("tax", 1, "monthly", "50m+", "Choose an annual turnover bracket.")]
        public void Invalid_input_is_rejected(string service, int employees, string frequency, string bracket, string message)
        {
            var input = new QuoteInput(service, employees, frequency, bracket);

            Assert.Equal(message, PricingCalculator.Validate(input));
            Assert.Throws<ArgumentException>(() => PricingCalculator.Calculate(input, DefaultRules()));
        }

        [Fact]
        public void Rands_are_formatted_with_comma_separators()
        {
            Assert.Equal("R 1,450", PricingCalculator.FormatRand(1450m));
            Assert.Equal("R 850 – R 1,450", PricingCalculator.FormatRange(850m, 1450m));
        }
    }
}
