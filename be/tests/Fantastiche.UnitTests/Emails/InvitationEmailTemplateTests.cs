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

    [Theory]
    [InlineData(InvitationKind.Organizer, "Ciao, la tua lega ti aspetta.")]
    [InlineData(InvitationKind.Participant, "Ciao, c'è posto per te.")]
    public void UnknownRecipientUsesGenericGreeting(InvitationKind kind, string greeting)
    {
        var email = InvitationEmailTemplate.Render("", "new@example.test", "Edoardo", "Lega Amici", null, "2026/27", 500, kind, Expires, Link);
        Assert.Contains(greeting, email.TextBody);
        Assert.DoesNotContain("Ciao ,", email.HtmlBody);
        Assert.Contains("il tuo nome", email.TextBody);
    }

    [Fact]
    public void ExistingAccountEmailKeepsNameAndRequestsLoginInsteadOfActivation()
    {
        var email = InvitationEmailTemplate.Render("Marta", "marta@example.test", "Edoardo", "Lega Amici", null, "2026/27", 500, InvitationKind.Participant, Expires, Link, requiresActivation: false);
        Assert.Contains("Ciao Marta,", email.TextBody);
        Assert.Contains("Accedi con il tuo account", email.TextBody);
        Assert.DoesNotContain("password", email.TextBody);
        Assert.DoesNotContain("scegliere il tuo nome", email.TextBody);
    }

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
        Assert.Contains("scegliere il tuo nome, il nome della tua squadra e una password", email.HtmlBody);
        Assert.Contains("Il link è personale e vale fino a sabato 12 settembre alle 18:30", email.HtmlBody);
        Assert.Contains($"href=\"{Link}\"", email.HtmlBody);
        Assert.Contains("background-color:#1a1125;background-image:linear-gradient(180deg, #30243e 0%, #1a1125 55%, #0c0c0e 100%)", email.HtmlBody);
        // Senza logo il riquadro della lega non mostra nessun badge, nemmeno le iniziali.
        Assert.DoesNotContain("class=\"fx-badge\"", email.HtmlBody);
        Assert.DoesNotContain("<img", email.HtmlBody);
        Assert.DoesNotContain("sei stato", email.HtmlBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Hai ricevuto questa email perché Edoardo ha inserito il tuo indirizzo nella sua lega.", email.HtmlBody);
    }

    [Fact]
    public void HtmlDeclaresItsOwnColorSchemeAndReassertsThePaletteInDarkMode()
    {
        var html = Render(InvitationKind.Participant).HtmlBody;
        Assert.Contains("<meta name=\"color-scheme\" content=\"light\">", html);
        Assert.Contains("<meta name=\"supported-color-schemes\" content=\"light\">", html);
        Assert.Contains(":root{color-scheme:light;supported-color-schemes:light;}", html);
        Assert.Contains("@media (prefers-color-scheme: dark){", html);
        Assert.Contains("[data-ogsb] .fx-card{background-color:#141218 !important;", html);
        Assert.Contains("[data-ogsb] .fx-btn{background-color:#c4a1ff !important;}", html);
        Assert.Contains("[data-ogsc] .fx-btn{color:#181020 !important;}", html);
        Assert.Contains("class=\"fx-card\"", html);
        Assert.Contains("class=\"fx-btn\"", html);
    }

    [Fact]
    public void BrandLogoReplacesTheTextHeaderWhenAvailable()
    {
        const string brand = "https://fantastiche.test/brand/fantastiche-logo-email.png";
        var email = InvitationEmailTemplate.Render("Marta", "marta@example.test", "Edoardo", "Lega Amici", null, "2026/27", 500, InvitationKind.Participant, Expires, Link, brand);
        Assert.Contains($"<img class=\"fx-brand\" src=\"{brand}\" alt=\"Fantastiche\" width=\"56\" height=\"56\"", email.HtmlBody);
        Assert.DoesNotContain("<strong>Fantastiche</strong>", email.HtmlBody);
        var withoutBrand = Render(InvitationKind.Participant);
        Assert.Contains("<strong>Fantastiche</strong>", withoutBrand.HtmlBody);
        Assert.DoesNotContain("fx-brand", withoutBrand.HtmlBody);
    }

    [Fact]
    public void OrganizerEmailUsesOrganizerWordingWithoutTeamStep()
    {
        var email = Render(InvitationKind.Organizer);
        Assert.Equal("Organizza Lega Amici su Fantastiche", email.Subject);
        Assert.Contains("Ciao Marta, la tua lega ti aspetta.", email.HtmlBody);
        Assert.Contains("Edoardo ti ha affidato l&#x27;organizzazione della lega:", email.HtmlBody);
        Assert.Contains("Organizza la lega", email.HtmlBody);
        Assert.Contains("Ti verrà chiesto di scegliere il tuo nome e una password per il tuo account.", email.HtmlBody);
        Assert.DoesNotContain("nome della tua squadra", email.HtmlBody);
        Assert.DoesNotContain("nome della tua squadra", email.TextBody);
    }

    [Fact]
    public void LeagueNameIsHtmlEncodedAndInitialsFollowFirstTwoWords()
    {
        var email = Render(InvitationKind.Participant, "Tom & Jerry <FC>");
        Assert.Contains("Tom &amp; Jerry &lt;FC&gt;", email.HtmlBody);
        Assert.DoesNotContain("<FC>", email.HtmlBody);
        Assert.DoesNotContain("class=\"fx-badge\"", email.HtmlBody);
        Assert.Equal("Edoardo ti ha invitato a Tom & Jerry <FC>", email.Subject);
        Assert.Contains("Tom & Jerry <FC>", email.TextBody);
    }

    [Fact]
    public void LogoUrlRendersRoundImageWithInitialsAsAlt()
    {
        var email = Render(InvitationKind.Participant, logo: "https://cdn.example.test/logos/a b.png?x=1&y=2");
        Assert.Contains("<img class=\"fx-badge\" src=\"https://cdn.example.test/logos/a b.png?x=1&amp;y=2\" alt=\"LA\" width=\"40\" height=\"40\"", email.HtmlBody);
        var encoded = Render(InvitationKind.Participant, "Tom & Jerry <FC>", "https://cdn.example.test/logo.png");
        Assert.Contains("alt=\"T&amp;\"", encoded.HtmlBody);
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

    [Fact]
    public void UtcFallbackIsMarkedExplicitly()
    {
        Assert.Equal("sabato 12 settembre alle 16:30 UTC", InvitationEmailTemplate.FormatExpiry(Expires, TimeZoneInfo.Utc));
        Assert.False(InvitationEmailTemplate.UsesUtcFallback);
    }

    [Theory]
    [InlineData("Lega Amici", "LA")]
    [InlineData("  serie \t a  ", "SA")]
    [InlineData("Serie Amici 2026/27", "SA")]
    [InlineData("Solo", "SO")]
    [InlineData("x", "X")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("Una Lega Con Tante Parole", "UL")]
    public void InitialsFollowTheFrontendRule(string name, string expected) => Assert.Equal(expected, InvitationEmailTemplate.Initials(name));
}
