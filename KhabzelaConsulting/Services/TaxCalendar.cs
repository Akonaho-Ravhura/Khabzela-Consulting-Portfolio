namespace KhabzelaConsulting.Services
{
    public record TaxProfile(bool IsEmployer, bool IsVatRegistered, string VatCategory, bool IsProvisionalTaxpayer, bool IsCompany, int YearEndMonth);

    // Key is stable per deadline (e.g. "EMP201:2026-10") so reminders are only sent once.
    public record TaxDeadline(string Key, string Code, string Title, DateTime Due, string Detail);

    // Works out a client's upcoming SARS deadlines from their tax profile — no SARS data or client login needed.
    // Due dates that fall on a weekend or public holiday move to the previous business day.
    // The owner should confirm these rules each year: SARS can change filing-season dates.
    public static class TaxCalendar
    {
        public static List<TaxDeadline> Upcoming(TaxProfile p, DateTime today, int daysAhead)
        {
            today = today.Date;
            var until = today.AddDays(daysAhead);
            // Start a year back: ITR14 and the first IRP6 fall due 6–12 months after the year-end that triggers them.
            return All(p, today.AddMonths(-13), until)
                .Where(d => d.Due >= today && d.Due <= until)
                .GroupBy(d => d.Key).Select(g => g.First())
                .OrderBy(d => d.Due).ThenBy(d => d.Code)
                .ToList();
        }

        private static IEnumerable<TaxDeadline> All(TaxProfile p, DateTime from, DateTime to)
        {
            for (var month = new DateTime(from.Year, from.Month, 1); month <= to; month = month.AddMonths(1))
            {
                var label = month.ToString("MMMM yyyy");

                if (p.IsEmployer)
                {
                    // EMP201 (PAYE, UIF, SDL) for a month is due by the 7th of the next month.
                    yield return new TaxDeadline($"EMP201:{month:yyyy-MM}", "EMP201", "EMP201 payroll return",
                        BusinessDayOnOrBefore(month.AddMonths(1).AddDays(6)), $"PAYE, UIF and SDL for {label}.");

                    // EMP501 reconciliations: interim (March–August) by 31 October, annual (March–February) by 31 May.
                    if (month.Month == 10)
                        yield return new TaxDeadline($"EMP501-I:{month.Year}", "EMP501", "EMP501 interim reconciliation",
                            BusinessDayOnOrBefore(new DateTime(month.Year, 10, 31)), $"Payroll reconciliation for March – August {month.Year}.");
                    if (month.Month == 5)
                        yield return new TaxDeadline($"EMP501-A:{month.Year}", "EMP501", "EMP501 annual reconciliation",
                            BusinessDayOnOrBefore(new DateTime(month.Year, 5, 31)), $"Payroll reconciliation and IRP5s for March {month.Year - 1} – February {month.Year}.");
                }

                if (p.IsVatRegistered && IsVatPeriodEnd(p.VatCategory, month.Month))
                {
                    // VAT201 filed on eFiling is due on the last business day of the month after the period ends.
                    yield return new TaxDeadline($"VAT201:{month:yyyy-MM}", "VAT201", "VAT201 return",
                        BusinessDayOnOrBefore(EndOfMonth(month.AddMonths(1))), $"VAT period ending {label}.");
                }

                if (month.Month == p.YearEndMonth)
                {
                    var yearEnd = EndOfMonth(month);

                    if (p.IsProvisionalTaxpayer)
                    {
                        // IRP6: first payment six months into the year, second at year-end.
                        var firstDue = EndOfMonth(month.AddMonths(6));
                        yield return new TaxDeadline($"IRP6-1:{firstDue:yyyy-MM}", "IRP6", "First provisional tax (IRP6)",
                            BusinessDayOnOrBefore(firstDue), $"First IRP6 for the year ending {EndOfMonth(month.AddMonths(12)):d MMMM yyyy}.");
                        yield return new TaxDeadline($"IRP6-2:{yearEnd:yyyy-MM}", "IRP6", "Second provisional tax (IRP6)",
                            BusinessDayOnOrBefore(yearEnd), $"Second IRP6 for the year ending {yearEnd:d MMMM yyyy}.");
                    }

                    if (p.IsCompany)
                    {
                        // ITR14 is due within 12 months of the financial year-end.
                        yield return new TaxDeadline($"ITR14:{yearEnd:yyyy-MM}", "ITR14", "ITR14 company income tax return",
                            BusinessDayOnOrBefore(EndOfMonth(month.AddMonths(12))), $"Return for the year ended {yearEnd:d MMMM yyyy}.");
                    }
                }
            }
        }

        // Category A: two-month periods ending in odd months; B: even months; C: every month.
        public static bool IsVatPeriodEnd(string category, int month) => category switch
        {
            "C" => true,
            "B" => month % 2 == 0,
            _ => month % 2 == 1,
        };

        public static DateTime BusinessDayOnOrBefore(DateTime date)
        {
            date = date.Date;
            while (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday || IsPublicHoliday(date)) date = date.AddDays(-1);
            return date;
        }

        public static bool IsPublicHoliday(DateTime date)
        {
            date = date.Date;
            var y = date.Year;
            var easter = EasterSunday(y);
            var fixedDays = new[]
            {
                new DateTime(y, 1, 1), new DateTime(y, 3, 21), new DateTime(y, 4, 27), new DateTime(y, 5, 1),
                new DateTime(y, 6, 16), new DateTime(y, 8, 9), new DateTime(y, 9, 24), new DateTime(y, 12, 16),
                new DateTime(y, 12, 25), new DateTime(y, 12, 26),
            };
            if (date == easter.AddDays(-2) || date == easter.AddDays(1)) return true; // Good Friday, Family Day
            if (fixedDays.Contains(date)) return true;
            // A public holiday on a Sunday moves to the Monday.
            return date.DayOfWeek == DayOfWeek.Monday && fixedDays.Contains(date.AddDays(-1));
        }

        // Anonymous Gregorian algorithm.
        public static DateTime EasterSunday(int year)
        {
            int a = year % 19, b = year / 100, c = year % 100, d = b / 4, e = b % 4, f = (b + 8) / 25, g = (b - f + 1) / 3;
            int h = (19 * a + b - d - g + 15) % 30, i = c / 4, k = c % 4, l = (32 + 2 * e + 2 * i - h - k) % 7;
            int m = (a + 11 * h + 22 * l) / 451, month = (h + l - 7 * m + 114) / 31, day = (h + l - 7 * m + 114) % 31 + 1;
            return new DateTime(year, month, day);
        }

        private static DateTime EndOfMonth(DateTime d) => new(d.Year, d.Month, DateTime.DaysInMonth(d.Year, d.Month));
    }
}
