using KhabzelaConsulting.Models;
using KhabzelaConsulting.Services;

namespace KhabzelaConsulting.Tests
{
    public class TaxCalendarTests
    {
        private static TaxProfile Profile(bool employer = false, bool vat = false, string vatCategory = "A",
            bool provisional = false, bool company = false, int yearEnd = 2) =>
            new(employer, vat, vatCategory, provisional, company, yearEnd);

        private static List<TaxDeadline> Upcoming(TaxProfile p, string today, int days = 400) =>
            TaxCalendar.Upcoming(p, DateTime.Parse(today), days);

        [Fact]
        public void Emp201_is_due_on_the_7th_or_the_business_day_before()
        {
            var emp201 = Upcoming(Profile(employer: true), "2026-10-11").Where(d => d.Code == "EMP201").ToList();

            Assert.Equal("EMP201:2026-10", emp201[0].Key);
            Assert.Equal(new DateTime(2026, 11, 6), emp201[0].Due); // 7 Nov 2026 is a Saturday
            Assert.Equal(new DateTime(2026, 12, 7), emp201[1].Due);  // November period, a Monday
        }

        [Fact]
        public void Vat201_category_A_follows_odd_month_periods()
        {
            var vat = Upcoming(Profile(vat: true, vatCategory: "A"), "2026-10-11", 90).Where(d => d.Code == "VAT201").ToList();

            Assert.Equal("VAT201:2026-09", vat[0].Key);
            Assert.Equal(new DateTime(2026, 10, 30), vat[0].Due); // 31 Oct 2026 is a Saturday
            Assert.Equal("VAT201:2026-11", vat[1].Key);
        }

        [Theory]
        [InlineData("B", "VAT201:2026-10")]
        [InlineData("C", "VAT201:2026-09")]
        public void Other_vat_categories(string category, string firstKey)
        {
            var first = Upcoming(Profile(vat: true, vatCategory: category), "2026-10-11", 90).First(d => d.Code == "VAT201");
            Assert.Equal(firstKey, first.Key);
        }

        [Fact]
        public void Irp6_follows_the_financial_year_end()
        {
            var irp6 = Upcoming(Profile(provisional: true, yearEnd: 2), "2026-10-11").Where(d => d.Code == "IRP6").ToList();

            Assert.Equal(new DateTime(2027, 2, 26), irp6[0].Due); // 28 Feb 2027 is a Sunday
            Assert.StartsWith("Second", irp6[0].Title);
            Assert.Equal(new DateTime(2027, 8, 31), irp6[1].Due);
            Assert.StartsWith("First", irp6[1].Title);
        }

        [Fact]
        public void Itr14_is_due_twelve_months_after_year_end()
        {
            var itr14 = Upcoming(Profile(company: true, yearEnd: 6), "2026-10-11").First(d => d.Code == "ITR14");

            Assert.Equal("ITR14:2026-06", itr14.Key);
            Assert.Equal(new DateTime(2027, 6, 30), itr14.Due);
        }

        [Fact]
        public void First_irp6_for_a_june_year_end_is_due_in_december()
        {
            var irp6 = Upcoming(Profile(provisional: true, yearEnd: 6), "2026-10-11").First(d => d.Code == "IRP6");

            Assert.StartsWith("First", irp6.Title);
            Assert.Equal(new DateTime(2026, 12, 31), irp6.Due);
        }

        [Fact]
        public void Emp501_reconciliations()
        {
            var emp501 = Upcoming(Profile(employer: true), "2026-10-11").Where(d => d.Code == "EMP501").ToList();

            Assert.Equal(new DateTime(2026, 10, 30), emp501[0].Due); // interim; 31 Oct is a Saturday
            Assert.Equal(new DateTime(2027, 5, 31), emp501[1].Due);  // annual
        }

        [Fact]
        public void A_client_with_an_empty_profile_has_no_deadlines()
        {
            Assert.Empty(Upcoming(Profile(), "2026-10-11"));
        }

        [Fact]
        public void Deadlines_due_today_are_included_but_not_past_ones()
        {
            var list = Upcoming(Profile(employer: true), "2026-11-06", 0);
            Assert.Single(list);
            Assert.Equal("EMP201:2026-10", list[0].Key);
        }

        [Theory]
        [InlineData("2027-03-26", true)]  // Good Friday 2027
        [InlineData("2027-03-29", true)]  // Family Day 2027
        [InlineData("2027-03-22", true)]  // Human Rights Day on a Sunday moves to Monday
        [InlineData("2026-12-16", true)]
        [InlineData("2026-12-17", false)]
        public void South_african_public_holidays(string date, bool expected)
        {
            Assert.Equal(expected, TaxCalendar.IsPublicHoliday(DateTime.Parse(date)));
        }

        [Fact]
        public void Due_date_on_a_holiday_moves_to_the_previous_business_day()
        {
            // 16 Dec 2026 (Wednesday) is a public holiday.
            Assert.Equal(new DateTime(2026, 12, 15), TaxCalendar.BusinessDayOnOrBefore(new DateTime(2026, 12, 16)));
        }
    }

    public class BookingSlotTests
    {
        [Fact]
        public void Offers_the_next_five_working_days_starting_tomorrow()
        {
            var friday = new DateTime(2026, 10, 9);
            var days = BookingSlots.Days(friday);

            Assert.Equal(5, days.Count);
            Assert.Equal(new DateTime(2026, 10, 12), days[0]); // skips the weekend to Monday
            Assert.DoesNotContain(days, d => d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday);
        }

        [Fact]
        public void Only_listed_times_on_offered_days_are_valid()
        {
            var today = new DateTime(2026, 10, 9);

            Assert.True(BookingSlots.IsValidSlot(new DateTime(2026, 10, 12, 10, 30, 0), today));
            Assert.False(BookingSlots.IsValidSlot(new DateTime(2026, 10, 12, 10, 45, 0), today)); // not a slot
            Assert.False(BookingSlots.IsValidSlot(new DateTime(2026, 10, 9, 10, 30, 0), today));  // today
            Assert.False(BookingSlots.IsValidSlot(new DateTime(2026, 10, 10, 10, 30, 0), today)); // Saturday
            Assert.False(BookingSlots.IsValidSlot(new DateTime(2026, 11, 30, 10, 30, 0), today)); // too far ahead
        }
    }

    public class InvoiceStatusTests
    {
        [Fact]
        public void Unpaid_invoice_past_due_date_shows_as_overdue()
        {
            var invoice = new Invoice { DueDate = new DateTime(2026, 10, 1), Status = InvoiceStatuses.Due };

            Assert.Equal(InvoiceStatuses.Overdue, invoice.DisplayStatus(new DateTime(2026, 10, 2)));
            Assert.Equal(InvoiceStatuses.Due, invoice.DisplayStatus(new DateTime(2026, 10, 1)));
        }

        [Fact]
        public void Paid_invoice_is_never_overdue()
        {
            var invoice = new Invoice { DueDate = new DateTime(2026, 1, 1), Status = InvoiceStatuses.Paid };
            Assert.Equal(InvoiceStatuses.Paid, invoice.DisplayStatus(new DateTime(2026, 10, 2)));
        }

        [Theory]
        [InlineData("Vukani", "V")]
        [InlineData("Eugene Khoza", "EK")]
        [InlineData("  thabo  van der merwe ", "TM")]
        [InlineData("", "?")]
        public void Initials(string name, string expected) => Assert.Equal(expected, Text.Initials(name));
    }
}
