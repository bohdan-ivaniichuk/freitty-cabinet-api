using Freitty.Domain.Companies;

namespace Freitty.Domain.Tests.Companies;

public sealed class UserTests
{
    [Theory]
    [InlineData("Dana", "D")]
    [InlineData("dana smith", "DS")]
    [InlineData("User 1", "U1")]
    [InlineData("Dana Marie Smith", "DM")]
    public void InitialsAreTheUpperCasedFirstLettersOfTheFirstTwoWords(string name, string expectedInitials)
    {
        var user = new User(Guid.CreateVersion7(), name, UserRole.Admin);

        Assert.Equal(expectedInitials, user.Initials);
    }
}
