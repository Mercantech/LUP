using Mercantec.Tech.Web.Learning;

namespace Mercantec.Tech.Web.Tests;

public sealed class HoldTimeTests
{
    [Theory]
    [InlineData("2026-11-16T10:30")]
    [InlineData("2026-11-16T10:30:00")]
    public void Calendar_accepts_local_time_from_the_browser_change_event(string input)
    {
        Assert.True(HoldTime.TryUtc(input, out var utc));
        Assert.Equal(new DateTimeOffset(2026, 11, 16, 9, 30, 0, TimeSpan.Zero), utc);
    }

    [Theory]
    [InlineData("2026-03-29T02:30:00")]
    [InlineData("2026-10-25T02:30:00")]
    public void Calendar_rejects_nonexistent_and_ambiguous_times(string input)
        => Assert.False(HoldTime.TryUtc(input, out _));
}
