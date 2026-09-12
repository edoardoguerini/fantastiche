using Fantastiche.Infrastructure.Emails;
using Fantastiche.Infrastructure.Leagues;

namespace Fantastiche.UnitTests.Emails;

public sealed class InvitationEmailTemplateTests
{
    private const string Link = "https://fantastiche.test/invito#token=0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF";
    // 16:30 UTC del 12 settembre 2026: ora legale a Roma, quindi 18:30.
    private static readonly DateTimeOffset Expires = new(2026, 9, 12, 16, 30, 0, TimeSpan.Zero);

    private static Core.Email.RenderedEmail Render(InvitationKind kind, string league = "Lega Amici", string? logo = null) =>
        InvitationEmailTemplate.Render("Marta", "marta@example.test", "Edoardo", league, logo, "2026/27", 500, kind, Expires, Link);

    [Fact]
    public void ParticipantEmailCarriesLinkSeasonAndItalianAbsoluteDate()
    {
        var email = Render(InvitationKind.Participant);
        Assert.Equal("marta@example.test", email.ToAddress); Assert.Equal("Marta", email.ToName);
        Assert.Equal("Edoardo ti ha invitato a Lega Amici", email.Subject);
        Assert.Contains("Ciao Marta, c&#x27;è posto per te.", email.HtmlBody);
        Assert.Contains("Edoardo ti ha invitato a partecipare come allenatore alla lega:", email.HtmlBody);
        Assert.Contains("Stagione 2026/27 · budget 500 crediti", email.HtmlBody);
        Assert.Contains("Accetta l&#x27;invito", email.HtmlBody);
        Assert.Contains("scegliere il nome della tua squadra e una password", email.HtmlBody);
        Assert.Contains("Il link è personale e vale fino a sabato 12 settembre alle 18:30", email.HtmlBody);
        Assert.Contains($"href=\"{Link}\"", email.HtmlBody);
        Assert.Contains("background-color:#1a1125;background-image:linear-gradient(180deg, #30243e 0%, #1a1125 55%, #0c0c0e 100%)", email.HtmlBody);
        Assert.Contains(">LA</div>", email.HtmlBody);
        Assert.DoesNotContain("<img", email.HtmlBody);
        Assert.DoesNotContain("sei stato", email.HtmlBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Hai ricevuto questa email perché Edoardo ha inserito il tuo indirizzo nella sua lega.", email.HtmlBody);
    }

    [Fact]
    public void OrganizerEmailUsesOrganizerWordingWithoutTeamStep()
    {
        var email = Render(InvitationKind.Organizer);
        Assert.Equal("Organizza Lega Amici su Fantastiche", email.Subject);
        Assert.Contains("Ciao Marta, la tua lega ti aspetta.", email.HtmlBody);
        Assert.Contains("Edoardo ti ha affidato l&#x27;organizzazione della lega:", email.HtmlBody);
        Assert.Contains("Organizza la lega", email.HtmlBody);
        Assert.Contains("Ti verrà chiesto di scegliere una password per il tuo account.", email.HtmlBody);
        Assert.DoesNotContain("nome della tua squadra", email.HtmlBody);
        Assert.DoesNotContain("nome della tua squadra", email.TextBody);
    }

    [Fact]
    public void LeagueNameIsHtmlEncodedAndInitialsFollowFirstTwoWords()
    {
        var email = Render(InvitationKind.Participant, "Tom & Jerry <FC>");
        Assert.Contains("Tom &amp; Jerry &lt;FC&gt;", email.HtmlBody);
        Assert.DoesNotContain("<FC>", email.HtmlBody);
        Assert.Contains(">T&amp;</div>", email.HtmlBody);
        Assert.Equal("Edoardo ti ha invitato a Tom & Jerry <FC>", email.Subject);
        Assert.Contains("Tom & Jerry <FC>", email.TextBody);
    }

    [Fact]
    public void LogoUrlRendersRoundImageWithInitialsAsAlt()
    {
        var email = Render(InvitationKind.Participant, logo: "https://cdn.example.test/logos/a b.png?x=1&y=2");
        Assert.Contains("<img src=\"https://cdn.example.test/logos/a b.png?x=1&amp;y=2\" alt=\"LA\" width=\"40\" height=\"40\"", email.HtmlBody);
    }

    [Fact]
    public void TextBodyEndsWithTheLinkAndCarriesTheSameInformation()
    {
        var text = Render(InvitationKind.Participant).TextBody!;
        Assert.EndsWith("\n" + Link, text);
        Assert.Equal(Link, text.Split('\n')[^1]);
        Assert.Contains("Ciao Marta, c'è posto per te.", text);
        Assert.Contains("Lega Amici\nStagione 2026/27 · budget 500 crediti", text);
        Assert.Contains("vale fino a sabato 12 settembre alle 18:30", text);
        Assert.Contains("Fantastiche · Il fantacalcio, insieme.", text);
    }

    [Theory]
    [InlineData("2026-01-10T23:30:00Z", "domenica 11 gennaio alle 00:30")]
    [InlineData("2026-07-01T10:05:00+02:00", "mercoledì 1 luglio alle 10:05")]
    public void ExpiryIsFormattedInRomeTimeZone(string iso, string expected) =>
        Assert.Equal(expected, InvitationEmailTemplate.FormatExpiry(DateTimeOffset.Parse(iso, System.Globalization.CultureInfo.InvariantCulture)));

    [Theory]
    [InlineData("Lega Amici", "LA")]
    [InlineData("  serie   a  ", "SA")]
    [InlineData("Solo", "S")]
    [InlineData("Una Lega Con Tante Parole", "UL")]
    public void InitialsUseFirstTwoWords(string name, string expected) => Assert.Equal(expected, InvitationEmailTemplate.Initials(name));
}
