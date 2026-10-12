namespace KhabzelaConsulting.Services
{
    // South African local time, independent of the server's time zone (Azure / Afrihost).
    public class SaClock
    {
        private static readonly TimeZoneInfo Zone = FindZone();
        private readonly TimeProvider _time;

        public SaClock(TimeProvider time) => _time = time;

        public DateTime Now => TimeZoneInfo.ConvertTime(_time.GetUtcNow(), Zone).DateTime;
        public DateTime Today => Now.Date;

        public DateTime ToLocal(DateTime utc) =>
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);

        private static TimeZoneInfo FindZone()
        {
            foreach (var id in new[] { "Africa/Johannesburg", "South Africa Standard Time" })
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
                catch (TimeZoneNotFoundException) { }
                catch (InvalidTimeZoneException) { }
            }
            return TimeZoneInfo.CreateCustomTimeZone("SAST", TimeSpan.FromHours(2), "SAST", "SAST");
        }
    }
}
