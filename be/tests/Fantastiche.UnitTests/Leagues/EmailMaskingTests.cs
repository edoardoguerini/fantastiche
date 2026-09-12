using Fantastiche.Infrastructure.Leagues;

namespace Fantastiche.UnitTests.Leagues;

public sealed class EmailMaskingTests
{
    [Theory]
    [InlineData("edoardo@mahiz.it", "e•••o@mahiz.it")]
    [InlineData("abc@x.it", "a•••c@x.it")]
    [InlineData("ab@x.it", "a•••@x.it")]
    [InlineData("a@x.it", "a•••@x.it")]
    [InlineData("nome.cognome@sub.example.test", "n•••e@sub.example.test")]
    public void MaskKeepsLocalPartEdgesAndWholeDomain(string email, string expected) => Assert.Equal(expected, EmailMasking.Mask(email));

    [Theory]
    [InlineData("")]
    [InlineData("@x.it")]
    [InlineData("senza-chiocciola")]
    public void MaskNeverRevealsMalformedAddresses(string email) => Assert.Equal("•••", EmailMasking.Mask(email));
}
