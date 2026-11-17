using Freitty.Domain.Hubs;

namespace Freitty.Domain.Tests.Hubs;

public sealed class HubTests
{
    [Fact]
    public void HubAcceptsAnIanaTimeZoneId()
    {
        var hub = new Hub("Toronto Central", "TOR", "ON", "America/Toronto");

        Assert.Equal("America/Toronto", hub.TimeZone);
    }

    [Theory]
    [InlineData("Eastern Standard Time")]
    [InlineData("Mars/Olympus_Mons")]
    public void HubRejectsATimeZoneIdThatIsNotAKnownIanaId(string timeZone)
    {
        Assert.ThrowsAny<ArgumentException>(() => new Hub("Toronto Central", "TOR", "ON", timeZone));
    }
}
