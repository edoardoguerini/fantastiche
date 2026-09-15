using Fantastiche.Core.Exceptions;
using Fantastiche.Infrastructure.Auctions;

namespace Fantastiche.UnitTests;

public sealed class FantacalcioRosterCsvTests
{
    private static readonly Guid FirstTeam = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid SecondTeam = Guid.Parse("10000000-0000-0000-0000-000000000002");

    [Fact]
    public void WritesOfficialTeamBlocksWithExternalIdsAndPaidPrices()
    {
        var csv = FantacalcioRosterCsv.Write([
            new(FirstTeam, "Virtù FC", "FantacalcioCsv", "123", 7),
            new(SecondTeam, "L'Aquila", "FantacalcioCsv", "456", 12),
            new(FirstTeam, "Virtù FC", "FantacalcioCsv", "789", 1)
        ]);

        Assert.Equal("$,$,$\nVirtù FC,123,7\nVirtù FC,789,1\n$,$,$\nL'Aquila,456,12\n", csv);
    }

    [Theory]
    [InlineData("AnotherProvider", "123")]
    [InlineData("FantacalcioCsv", "abc")]
    [InlineData("FantacalcioCsv", "0")]
    [InlineData("FantacalcioCsv", "-1")]
    [InlineData("FantacalcioCsv", "1,2")]
    public void RefusesAnUnusablePlayerIdentity(string source, string externalId)
    {
        var error = Assert.Throws<DomainException>(() => FantacalcioRosterCsv.Write([
            new(FirstTeam, "Prima", source, externalId, 1)
        ]));
        Assert.Equal("auction.export_invalid_player", error.Code);
    }

    [Theory]
    [InlineData("AC, Milano")]
    [InlineData("AC\nMilano")]
    [InlineData("AC\rMilano")]
    [InlineData("\"Milano\"")]
    [InlineData("$Milano")]
    [InlineData(" =1+2")]
    [InlineData("+Milano")]
    [InlineData("@Milano")]
    [InlineData("\tMilano")]
    [InlineData(" ")]
    public void RefusesNamesThatBreakTheOfficialParserOrBecomeSpreadsheetFormulas(string name)
    {
        var error = Assert.Throws<DomainException>(() => FantacalcioRosterCsv.Write([
            new(FirstTeam, name, "FantacalcioCsv", "123", 1)
        ]));
        Assert.Equal("auction.export_invalid_team_name", error.Code);
    }

    [Fact]
    public void RefusesEmptyRostersAndInvalidPrices()
    {
        Assert.Equal("auction.export_empty", Assert.Throws<DomainException>(() => FantacalcioRosterCsv.Write([])).Code);
        Assert.Equal("auction.export_invalid_price", Assert.Throws<DomainException>(() => FantacalcioRosterCsv.Write([
            new(FirstTeam, "Prima", "FantacalcioCsv", "123", -1)
        ])).Code);
    }
}
