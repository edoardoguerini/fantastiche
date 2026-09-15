using System.Globalization;
using System.Text;
using Fantastiche.Core.Exceptions;

namespace Fantastiche.Infrastructure.Auctions;

public sealed record FantacalcioRosterRow(Guid TeamId, string TeamName, string Source, string ExternalId, int Price);

public static class FantacalcioRosterCsv
{
    public static string Write(IReadOnlyList<FantacalcioRosterRow> rows)
    {
        if (rows.Count == 0)
            throw new DomainException("auction.export_empty", "Non ci sono acquisti da esportare.", 409);

        var csv = new StringBuilder();
        foreach (var team in rows.GroupBy(row => row.TeamId))
        {
            var name = team.First().TeamName;
            // L'importatore ufficiale divide su virgola e LF: non gestisce le virgolette CSV.
            if (string.IsNullOrWhiteSpace(name) || name.Any(char.IsControl)
                || name.Contains(',') || name.Contains('"') || "=$+-@".Contains(name.TrimStart()[0]))
                throw new DomainException("auction.export_invalid_team_name",
                    $"Il nome squadra «{name}» non è compatibile con il CSV Fantacalcio. Rimuovi virgole, virgolette, caratteri di controllo e i simboli iniziali =, $, +, - o @.", 409);

            csv.Append("$,$,$\n");
            foreach (var row in team)
            {
                if (row.Source != "FantacalcioCsv"
                    || !int.TryParse(row.ExternalId, NumberStyles.None, CultureInfo.InvariantCulture, out var playerId)
                    || playerId <= 0)
                    throw new DomainException("auction.export_invalid_player",
                        "Una o più assegnazioni non hanno un ID Fantacalcio valido. Verifica la provenienza del listone.", 409);
                if (row.Price < 0)
                    throw new DomainException("auction.export_invalid_price", "Un acquisto ha un prezzo non valido.", 409);

                csv.Append(name.Trim()).Append(',').Append(playerId.ToString(CultureInfo.InvariantCulture))
                    .Append(',').Append(row.Price.ToString(CultureInfo.InvariantCulture)).Append('\n');
            }
        }
        return csv.ToString();
    }
}
