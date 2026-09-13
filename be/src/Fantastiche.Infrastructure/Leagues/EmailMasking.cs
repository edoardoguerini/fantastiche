namespace Fantastiche.Infrastructure.Leagues;

public static class EmailMasking
{
    private const string Dots = "•••";
    // Mostra solo gli estremi della parte locale: l’anteprima pubblica non deve rivelare l’indirizzo completo.
    public static string Mask(string email)
    {
        var at = email.IndexOf('@');
        if (at <= 0) return Dots;
        var local = email[..at];
        var last = local.Length >= 3 ? local[^1].ToString() : "";
        return string.Concat(local[0].ToString(), Dots, last, email[at..]);
    }
}
