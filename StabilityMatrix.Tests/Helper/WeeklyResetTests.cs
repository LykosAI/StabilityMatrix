using StabilityMatrix.Core.Helper;

namespace StabilityMatrix.Tests.Helper;

[TestClass]
public class WeeklyResetTests
{
    // 2026-10-14 is a Wednesday.
    [DataTestMethod]
    [DataRow("2026-10-14T12:00:00Z", "4 days")]
    [DataRow("2026-10-18T12:00:00Z", "12 hours")]
    [DataRow("2026-10-18T23:30:00Z", "less than an hour")]
    [DataRow("2026-10-18T22:30:00Z", "1 hour")]
    [DataRow("2026-10-18T00:00:00Z", "1 day")]
    [DataRow("2026-10-19T00:00:00Z", "7 days")]
    [DataRow("2026-10-19T00:00:01Z", "6 days")]
    public void FormatTimeUntilNextWeeklyReset_CountsToMondayMidnightUtc(string now, string expected)
    {
        var result = Utilities.FormatTimeUntilNextWeeklyReset(DateTimeOffset.Parse(now));

        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void GetTimeUntilNextWeeklyReset_UsesUtcRegardlessOfOffset()
    {
        // Sunday 20:00 at -05:00 is Monday 01:00 UTC, just after a reset.
        var now = new DateTimeOffset(2026, 10, 18, 20, 0, 0, TimeSpan.FromHours(-5));

        var result = Utilities.GetTimeUntilNextWeeklyReset(now);

        Assert.AreEqual(TimeSpan.FromDays(7) - TimeSpan.FromHours(1), result);
    }
}
