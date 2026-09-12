using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Fantastiche.Core.Email;
using Fantastiche.Infrastructure.Leagues;
namespace Fantastiche.Infrastructure.Emails;

public static class InvitationEmailTemplate
{
    private const string Font = "'Sora', 'Segoe UI', Arial, sans-serif";
    private static readonly CultureInfo Italian = CultureInfo.GetCultureInfo("it-IT");
    private static readonly TimeZoneInfo Rome = FindRome();
    // Codifica solo i caratteri significativi per HTML: gli accenti restano leggibili nel sorgente dell’email.
    private static readonly HtmlEncoder Encoder = HtmlEncoder.Create(UnicodeRanges.All);

    public static RenderedEmail Render(string recipientName, string recipientEmail, string inviterName, string leagueName, string? leagueLogoUrl,
     string seasonName, int budget, InvitationKind kind, DateTimeOffset expiresAt, string link)
    {
        var organizer = kind == InvitationKind.Organizer;
        var subject = organizer ? $"Organizza {leagueName} su Fantastiche" : $"{inviterName} ti ha invitato a {leagueName}";
        var title = organizer ? $"Ciao {recipientName}, la tua lega ti aspetta." : $"Ciao {recipientName}, c'è posto per te.";
        var intro = organizer ? $"{inviterName} ti ha affidato l'organizzazione della lega:" : $"{inviterName} ti ha invitato a partecipare come allenatore alla lega:";
        var action = organizer ? "Organizza la lega" : "Accetta l'invito";
        var next = organizer ? "Ti verrà chiesto di scegliere una password per il tuo account."
         : "Ti verrà chiesto di scegliere il nome della tua squadra e una password per il tuo account.";
        var season = $"Stagione {seasonName} · budget {budget} crediti";
        var validity = $"Il link è personale e vale fino a {FormatExpiry(expiresAt)}";
        const string fallback = "Se il bottone non funziona, copia questo indirizzo nel browser:";
        const string ignore = "Non aspettavi questa email? Puoi ignorarla: nessun account verrà creato senza la tua conferma.";
        const string footer = "Fantastiche · Il fantacalcio, insieme.";
        var reason = $"Hai ricevuto questa email perché {inviterName} ha inserito il tuo indirizzo nella sua lega.";
        var html = Html(title, intro, leagueName, leagueLogoUrl, season, action, next, validity, fallback, ignore, footer, reason, link);
        // Il link resta l’ultima riga del testo, senza nulla dopo: i test lo estraggono da lì.
        var text = string.Join('\n', [title, "", intro, leagueName, season, "", next, validity, "", ignore, "", footer, reason, "", $"{action}:", link]);
        return new RenderedEmail(recipientEmail, recipientName, subject, html, text);
    }

    private static string Html(string title, string intro, string leagueName, string? logoUrl, string season, string action, string next,
     string validity, string fallback, string ignore, string footer, string reason, string link)
    {
        Func<string, string> e = Encoder.Encode;
        var initials = e(Initials(leagueName));
        // Nessuna immagine remota del brand: il logo della lega compare solo quando ha un URL pubblico.
        var badge = string.IsNullOrWhiteSpace(logoUrl)
         ? $"<div style=\"width:40px;height:40px;line-height:40px;border-radius:20px;background-color:#30283e;color:#c4a1ff;font-weight:700;font-size:15px;text-align:center;\">{initials}</div>"
         : $"<img src=\"{e(logoUrl)}\" alt=\"{initials}\" width=\"40\" height=\"40\" style=\"display:block;width:40px;height:40px;border-radius:20px;background-color:#30283e;\">";
        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html lang=\"it\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"><title>").Append(e(title)).Append("</title></head>");
        sb.Append("<body style=\"margin:0;padding:0;background-color:#1a1125;\">");
        sb.Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"background-color:#1a1125;background-image:linear-gradient(180deg, #30243e 0%, #1a1125 55%, #0c0c0e 100%);\"><tr><td align=\"center\" style=\"padding:32px 16px;\">");
        sb.Append($"<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"max-width:520px;background-color:#141218;border:1px solid #2a2530;border-radius:14px;color:#f4f4f5;font-family:{Font};\">");
        sb.Append("<tr><td style=\"padding:24px 28px 8px;font-size:18px;font-weight:700;letter-spacing:0.02em;color:#f4f4f5;\"><strong>Fantastiche</strong></td></tr>");
        sb.Append($"<tr><td style=\"padding:8px 28px 0;\"><h2 style=\"margin:0 0 12px;font-size:22px;line-height:1.3;font-weight:700;color:#f4f4f5;\">{e(title)}</h2>");
        sb.Append($"<p style=\"margin:0 0 16px;font-size:15px;line-height:1.5;color:#aaaab3;\">{e(intro)}</p></td></tr>");
        sb.Append("<tr><td style=\"padding:0 28px 20px;\"><table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"background-color:#1b1820;border:1px solid #2a2530;border-radius:12px;\"><tr>");
        sb.Append($"<td width=\"40\" style=\"padding:14px 0 14px 14px;vertical-align:middle;\">{badge}</td>");
        sb.Append($"<td style=\"padding:14px;vertical-align:middle;\"><div style=\"font-size:16px;font-weight:700;color:#f4f4f5;\"><strong>{e(leagueName)}</strong></div><div style=\"font-size:13px;color:#aaaab3;margin-top:2px;\">{e(season)}</div></td>");
        sb.Append("</tr></table></td></tr>");
        sb.Append($"<tr><td style=\"padding:0 28px 12px;\"><a href=\"{e(link)}\" style=\"display:block;text-align:center;background-color:#c4a1ff;color:#181020;font-weight:700;font-size:15px;text-decoration:none;padding:14px;border-radius:10px;\">{e(action)}</a></td></tr>");
        sb.Append($"<tr><td style=\"padding:0 28px 6px;font-size:13px;line-height:1.5;color:#aaaab3;\">{e(next)}</td></tr>");
        sb.Append($"<tr><td style=\"padding:0 28px 20px;font-size:13px;line-height:1.5;color:#aaaab3;\">{e(validity)}</td></tr>");
        sb.Append("<tr><td style=\"padding:0 28px;\"><div style=\"border-top:1px solid #2a2530;font-size:0;line-height:0;\">&nbsp;</div></td></tr>");
        sb.Append($"<tr><td style=\"padding:20px 28px 6px;font-size:12px;line-height:1.5;color:#aaaab3;\">{e(fallback)}<br><a href=\"{e(link)}\" style=\"color:#c4a1ff;word-break:break-all;\">{e(link)}</a></td></tr>");
        sb.Append($"<tr><td style=\"padding:6px 28px 24px;font-size:12px;line-height:1.5;color:#aaaab3;\">{e(ignore)}</td></tr>");
        sb.Append("</table>");
        sb.Append($"<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"max-width:520px;\"><tr><td align=\"center\" style=\"padding:20px 12px 0;font-family:{Font};font-size:12px;line-height:1.6;color:#8a8a95;\">{e(footer)}<br>{e(reason)}</td></tr></table>");
        sb.Append("</td></tr></table></body></html>");
        return sb.ToString();
    }

    public static string Initials(string leagueName)
    {
        var words = leagueName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(2);
        return string.Concat(words.Select(w => char.ToUpperInvariant(w[0])));
    }

    // Data assoluta nel fuso italiano: chi legge l’email non conosce il fuso in cui è stata generata.
    public static string FormatExpiry(DateTimeOffset expiresAt)
    {
        var local = TimeZoneInfo.ConvertTime(expiresAt, Rome);
        return $"{local.ToString("dddd d MMMM", Italian)} alle {local:HH:mm}".ToLower(Italian);
    }

    private static TimeZoneInfo FindRome()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome"); }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException) { return TimeZoneInfo.Utc; }
    }
}
