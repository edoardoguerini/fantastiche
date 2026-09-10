using Fantastiche.Core.Exceptions;
using Fantastiche.Infrastructure.Catalog;
using System.Globalization;
using System.Text;

namespace Fantastiche.UnitTests.Catalog;

public sealed class FantacalcioCsvParserTests
{
    [Fact]
    public void Parse_WithValidRowMapsFieldsAndMarketValues()
    {
        var csv = BuildRow(
            externalId: "42",
            name: "ROSSI",
            fullName: "Mario Rossi",
            role: "D",
            clubName: "Como",
            birthDate: "03/02/1999 14:25:59",
            nationality: "Italia",
            preferredFoot: "Destro",
            ignoredValue: "dato-da-ignorare");

        var row = Assert.Single(FantacalcioCsvParser.Parse(csv));

        Assert.Equal("42", row.ExternalId);
        Assert.Equal("ROSSI", row.Name);
        Assert.Equal("Mario Rossi", row.FullName);
        Assert.Equal("D", row.Role);
        Assert.Equal("Como", row.ClubName);
        Assert.Equal(new DateTime(1999, 2, 3), row.BirthDate);
        Assert.Equal("Italia", row.Nationality);
        Assert.Equal("Destro", row.PreferredFoot);
        Assert.Equal("Dc", row.MantraRole);
        Assert.Equal(17, row.CurrentQuotation);
        Assert.Equal(16, row.InitialQuotation);
        Assert.Equal(18, row.CurrentMantraQuotation);
        Assert.Equal(15, row.InitialMantraQuotation);
        Assert.Equal(57, row.Fvm);
        Assert.Equal(60, row.MantraFvm);
        Assert.False(row.IsTransferred);
    }

    [Fact]
    public void Parse_HandlesBomWhitespaceQuotedEscapesAndCommas()
    {
        var csv = "\uFEFF" + BuildRow(
            externalId: " 00042 ",
            name: " ROSSI, JR ",
            fullName: " Mario \"Il Mago\"\nRossi ",
            role: " D ",
            clubName: " Como, 1907 ",
            birthDate: " 03/02/1999 14:25:59 ",
            nationality: " Italia ",
            preferredFoot: " Destro ");

        var row = Assert.Single(FantacalcioCsvParser.Parse(csv));

        Assert.Equal("42", row.ExternalId);
        Assert.Equal("ROSSI, JR", row.Name);
        Assert.Equal("Mario \"Il Mago\"\nRossi", row.FullName);
        Assert.Equal("D", row.Role);
        Assert.Equal("Como, 1907", row.ClubName);
        Assert.Equal(new DateTime(1999, 2, 3), row.BirthDate);
        Assert.Equal("Italia", row.Nationality);
        Assert.Equal("Destro", row.PreferredFoot);
    }

    [Theory]
    [InlineData("P")]
    [InlineData("D")]
    [InlineData("C")]
    [InlineData("A")]
    public void Parse_AcceptsEveryClassicRole(string role)
    {
        var row = Assert.Single(FantacalcioCsvParser.Parse(BuildRow(role: role)));

        Assert.Equal(role, row.Role);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1.5")]
    [InlineData("abc")]
    [InlineData("")]
    public void Parse_RejectsExternalIdThatIsNotAPositiveInteger(string externalId)
    {
        AssertInvalidCsv(BuildRow(externalId: externalId), "riga 1", "ID esterno");
    }

    [Fact]
    public void Parse_RejectsDuplicateExternalIdsAfterNormalization()
    {
        var csv = BuildRow(externalId: "42") + "\n" + BuildRow(externalId: "00042");

        AssertInvalidCsv(csv, "riga 2", "ID esterno");
    }

    [Theory]
    [InlineData("X")]
    [InlineData("p")]
    [InlineData("")]
    public void Parse_RejectsNonClassicRole(string role)
    {
        AssertInvalidCsv(BuildRow(role: role), "riga 1", "ruolo");
    }

    [Theory]
    [InlineData("name", 101, "nome breve")]
    [InlineData("fullName", 201, "nome completo")]
    [InlineData("clubName", 101, "club")]
    [InlineData("nationality", 201, "nazionalità")]
    [InlineData("preferredFoot", 51, "piede preferito")]
    public void Parse_RejectsRequiredFieldsOverTheirMaximumLength(string field, int length, string expectedField)
    {
        var value = new string('x', length);
        var csv = field switch
        {
            "name" => BuildRow(name: value),
            "fullName" => BuildRow(fullName: value),
            "clubName" => BuildRow(clubName: value),
            "nationality" => BuildRow(nationality: value),
            "preferredFoot" => BuildRow(preferredFoot: value),
            _ => throw new InvalidOperationException()
        };

        AssertInvalidCsv(csv, "riga 1", expectedField);
    }

    [Theory]
    [InlineData("name", "nome breve")]
    [InlineData("fullName", "nome completo")]
    [InlineData("clubName", "club")]
    [InlineData("nationality", "nazionalità")]
    [InlineData("preferredFoot", "piede preferito")]
    public void Parse_RejectsEmptyRequiredFields(string field, string expectedField)
    {
        var csv = field switch
        {
            "name" => BuildRow(name: "  "),
            "fullName" => BuildRow(fullName: "  "),
            "clubName" => BuildRow(clubName: "  "),
            "nationality" => BuildRow(nationality: "  "),
            "preferredFoot" => BuildRow(preferredFoot: "  "),
            _ => throw new InvalidOperationException()
        };

        AssertInvalidCsv(csv, "riga 1", expectedField);
    }

    [Theory]
    [InlineData("03/02/1999")]
    [InlineData("3/02/1999 14:25:59")]
    [InlineData("03/02/1999 14:25")]
    [InlineData("1999-02-03 14:25:59")]
    [InlineData("31/02/1999 14:25:59")]
    [InlineData("")]
    public void Parse_RejectsBirthDateOutsideTheExactFormat(string birthDate)
    {
        AssertInvalidCsv(BuildRow(birthDate: birthDate), "riga 1", "data di nascita");
    }

    [Theory]
    [InlineData(18)]
    [InlineData(20)]
    public void Parse_RejectsColumnCountOtherThanNineteen(int columnCount)
    {
        var csv = string.Join(',', Enumerable.Repeat("x", columnCount));

        AssertInvalidCsv(csv, "riga 1", "colonne");
    }

    [Fact]
    public void Parse_RejectsUnclosedQuotesWithoutDisclosingCsvContent()
    {
        const string secret = "dato-segreto";
        var csv = "1,ROSSI,\"dato-segreto,D,5,6,7,8,9,Como,11,12,Destro,Italia,03/02/1999 14:25:59,16,17,18,19";

        var error = Assert.Throws<DomainException>(() => FantacalcioCsvParser.Parse(csv));

        Assert.Equal("catalog.invalid_csv", error.Code);
        Assert.Equal(400, error.StatusCode);
        Assert.Contains("riga", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(secret, error.Message);
    }

    [Fact]
    public void Parse_DoesNotReturnPartialRowsWhenALaterRowIsInvalid()
    {
        var csv = BuildRow(externalId: "1") + "\n" + BuildRow(externalId: "2", role: "X");

        Assert.Throws<DomainException>(() => FantacalcioCsvParser.Parse(csv));
    }

    [Fact]
    public void Parse_RejectsEmptyWhitespaceOrNullCsv()
    {
        AssertInvalidCsv(string.Empty, "CSV");
        AssertInvalidCsv("   \r\n", "CSV");
        AssertInvalidCsv(null!, "CSV");
    }

    [Fact]
    public void Parse_RejectsCsvOverTheCharacterLimit()
    {
        var csv = new string('x', FantacalcioCsvParser.MaxCsvLength + 1);

        AssertInvalidCsv(csv, "limite", "caratteri");
    }

    [Fact]
    public void Parse_AcceptsExactlyTheMaximumRowCount()
    {
        var csv = BuildRows(FantacalcioCsvParser.MaxRows);

        var rows = FantacalcioCsvParser.Parse(csv);

        Assert.Equal(FantacalcioCsvParser.MaxRows, rows.Count);
        Assert.Equal("1", rows[0].ExternalId);
        Assert.Equal("5000", rows[^1].ExternalId);
    }

    [Fact]
    public void Parse_RejectsCsvOverTheMaximumRowCount()
    {
        var csv = BuildRows(FantacalcioCsvParser.MaxRows + 1);

        AssertInvalidCsv(csv, "limite", "righe");
    }

    [Fact]
    public void Parse_AcceptsExternalIdAtTheMaximumNormalizedLength()
    {
        var externalId = new string('9', 32);

        var row = Assert.Single(FantacalcioCsvParser.Parse(BuildRow(externalId: externalId)));

        Assert.Equal(externalId, row.ExternalId);
    }

    [Fact]
    public void Parse_RejectsExternalIdOverTheMaximumNormalizedLength()
    {
        var externalId = new string('9', 33);

        AssertInvalidCsv(BuildRow(externalId: externalId), "riga 1", "ID esterno");
    }

    [Theory]
    [InlineData(5, "-1")]
    [InlineData(6, "abc")]
    [InlineData(10, "1.5")]
    [InlineData(11, "")]
    [InlineData(16, "2")]
    public void ParseRejectsInvalidMarketValues(int column, string value)
    {
        var fields = BuildRow().Split(',');
        fields[column] = value;
        AssertInvalidCsv(string.Join(',', fields), "riga 1");
    }

    [Fact]
    public void ParsePreservesZeroQuotationAndTransferredFlag()
    {
        var fields = BuildRow().Split(',');
        fields[5] = "0";
        fields[16] = "1";
        var row = Assert.Single(FantacalcioCsvParser.Parse(string.Join(',', fields)));
        Assert.Equal(0, row.CurrentQuotation);
        Assert.True(row.IsTransferred);
    }

    private static void AssertInvalidCsv(string csv, params string[] expectedMessageParts)
    {
        var error = Assert.Throws<DomainException>(() => FantacalcioCsvParser.Parse(csv));

        Assert.Equal("catalog.invalid_csv", error.Code);
        Assert.Equal(400, error.StatusCode);
        foreach (var part in expectedMessageParts)
        {
            Assert.Contains(part, error.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string BuildRows(int count)
    {
        var csv = new StringBuilder(count * 100);
        for (var index = 1; index <= count; index++)
        {
            if (index > 1)
            {
                csv.Append('\n');
            }

            csv.Append(BuildRow(externalId: index.ToString(CultureInfo.InvariantCulture)));
        }

        return csv.ToString();
    }

    private static string BuildRow(
        string externalId = "1",
        string name = "ROSSI",
        string fullName = "Mario Rossi",
        string role = "D",
        string clubName = "Como",
        string birthDate = "03/02/1999 14:25:59",
        string nationality = "Italia",
        string preferredFoot = "Destro",
        string ignoredValue = "x")
    {
        var fields = Enumerable.Repeat(ignoredValue, 19).ToArray();
        fields[0] = externalId;
        fields[1] = name;
        fields[2] = fullName;
        fields[3] = role;
        fields[4] = "Dc";
        fields[5] = "17";
        fields[6] = "16";
        fields[7] = "18";
        fields[8] = "15";
        fields[10] = "57";
        fields[11] = "60";
        fields[16] = "0";
        fields[9] = clubName;
        fields[12] = preferredFoot;
        fields[13] = nationality;
        fields[14] = birthDate;
        return string.Join(',', fields.Select(Escape));
    }

    private static string Escape(string value)
    {
        return value.IndexOfAny([',', '"', '\r', '\n']) < 0
            ? value
            : '"' + value.Replace("\"", "\"\"") + '"';
    }
}
