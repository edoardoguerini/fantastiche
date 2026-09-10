using Fantastiche.Core.Exceptions;
using Microsoft.VisualBasic.FileIO;
using System.Globalization;

namespace Fantastiche.Infrastructure.Catalog;

public static class FantacalcioCsvParser
{
    private const int MaxExternalIdLength = 32;

    public const int MaxCsvLength = 1_048_576;
    public const int MaxRows = 5_000;

    public static IReadOnlyList<CatalogImportRow> Parse(string csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
        {
            throw InvalidCsv("Il CSV non contiene righe.");
        }

        if (csv.Length > MaxCsvLength)
        {
            throw InvalidCsv($"Il CSV supera il limite di {MaxCsvLength} caratteri.");
        }

        var content = csv[0] == '\uFEFF' ? csv[1..] : csv;
        var rows = new List<CatalogImportRow>();
        var externalIds = new HashSet<string>(StringComparer.Ordinal);

        using var reader = new StringReader(content);
        using var parser = new TextFieldParser(reader)
        {
            TextFieldType = FieldType.Delimited,
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = true
        };
        parser.SetDelimiters(",");

        while (!parser.EndOfData)
        {
            var rowNumber = rows.Count + 1;
            string[] fields;
            try
            {
                fields = parser.ReadFields() ?? [];
            }
            catch (MalformedLineException)
            {
                throw InvalidCsv($"Riga {rowNumber}: formato CSV non valido.");
            }

            if (rows.Count == MaxRows)
            {
                throw InvalidCsv($"Il CSV supera il limite di {MaxRows} righe.");
            }

            if (fields.Length != 19)
            {
                throw InvalidCsv($"Riga {rowNumber}, campo colonne: sono richieste esattamente 19 colonne.");
            }

            var externalId = NormalizeExternalId(fields[0], rowNumber);
            if (!externalIds.Add(externalId))
            {
                throw InvalidCsv($"Riga {rowNumber}, campo ID esterno: valore duplicato.");
            }

            var name = Required(fields[1], 100, rowNumber, "nome breve");
            var fullName = Required(fields[2], 200, rowNumber, "nome completo");
            var role = Required(fields[3], 1, rowNumber, "ruolo");
            if (role is not ("P" or "D" or "C" or "A"))
            {
                throw InvalidCsv($"Riga {rowNumber}, campo ruolo: valore non valido.");
            }

            var clubName = Required(fields[9], 100, rowNumber, "club");
            var preferredFoot = Required(fields[12], 50, rowNumber, "piede preferito");
            var nationality = Required(fields[13], 200, rowNumber, "nazionalità");
            var birthDateText = Required(fields[14], 19, rowNumber, "data di nascita");
            if (!DateTime.TryParseExact(
                    birthDateText,
                    "dd/MM/yyyy HH:mm:ss",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var birthDate))
            {
                throw InvalidCsv($"Riga {rowNumber}, campo data di nascita: formato non valido.");
            }

            rows.Add(new CatalogImportRow(
                externalId,
                name,
                fullName,
                role,
                clubName,
                birthDate.Date,
                nationality,
                preferredFoot,
                Required(fields[4], 50, rowNumber, "ruolo Mantra"),
                NonNegativeInteger(fields[5], rowNumber, "quotazione attuale"),
                NonNegativeInteger(fields[6], rowNumber, "quotazione iniziale"),
                NonNegativeInteger(fields[7], rowNumber, "quotazione attuale Mantra"),
                NonNegativeInteger(fields[8], rowNumber, "quotazione iniziale Mantra"),
                NonNegativeInteger(fields[10], rowNumber, "FVM"),
                NonNegativeInteger(fields[11], rowNumber, "FVM Mantra"),
                Transferred(fields[16], rowNumber)));
        }

        if (rows.Count == 0)
        {
            throw InvalidCsv("Il CSV non contiene righe.");
        }

        return rows;
    }

    private static int NonNegativeInteger(string value, int row, string field)
    {
        if (!int.TryParse(value.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var number))
            throw InvalidCsv($"Riga {row}, campo {field}: è richiesto un intero non negativo.");
        return number;
    }

    private static bool Transferred(string value, int row) => value.Trim() switch
    {
        "0" => false,
        "1" => true,
        _ => throw InvalidCsv($"Riga {row}, campo ceduto: sono ammessi soltanto 0 e 1.")
    };

    private static string NormalizeExternalId(string value, int rowNumber)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0 || trimmed.Any(character => character is < '0' or > '9'))
        {
            throw InvalidCsv($"Riga {rowNumber}, campo ID esterno: deve essere numerico e positivo.");
        }

        var normalized = trimmed.TrimStart('0');
        if (normalized.Length == 0)
        {
            throw InvalidCsv($"Riga {rowNumber}, campo ID esterno: deve essere numerico e positivo.");
        }

        if (normalized.Length > MaxExternalIdLength)
        {
            throw InvalidCsv($"Riga {rowNumber}, campo ID esterno: valore troppo lungo.");
        }

        return normalized;
    }

    private static string Required(string value, int maxLength, int rowNumber, string fieldName)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0 || trimmed.Length > maxLength)
        {
            throw InvalidCsv($"Riga {rowNumber}, campo {fieldName}: valore obbligatorio o troppo lungo.");
        }

        return trimmed;
    }

    private static DomainException InvalidCsv(string message)
    {
        return new DomainException("catalog.invalid_csv", message, 400);
    }
}
