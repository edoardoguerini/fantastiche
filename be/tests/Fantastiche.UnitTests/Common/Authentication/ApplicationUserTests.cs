using Fantastiche.Infrastructure.Common.Authentication;

namespace Fantastiche.UnitTests.Common.Authentication;

public sealed class ApplicationUserTests
{
    [Fact]
    public void Constructor_AssegnaUuidVersioneSette()
    {
        var user = new ApplicationUser();

        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Equal('7', user.Id.ToString("N")[12]);
    }
}
