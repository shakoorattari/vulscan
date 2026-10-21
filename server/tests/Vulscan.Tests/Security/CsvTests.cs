using Vulscan.Application.Common;

namespace Vulscan.Tests.Security;

public class CsvTests
{
    [Theory]
    [InlineData("=HYPERLINK(\"http://evil\")", "\"'=HYPERLINK(\"\"http://evil\"\")\"")]
    [InlineData("+cmd|' /C calc'!A0", "'+cmd|' /C calc'!A0")]
    [InlineData("-2+3", "'-2+3")]
    [InlineData("@SUM(A1)", "'@SUM(A1)")]
    public void Formula_prefixes_are_neutralised(string input, string expected)
        => Assert.Equal(expected, Csv.Escape(input));

    [Theory]
    [InlineData("plain", "plain")]
    [InlineData("a,b", "\"a,b\"")]
    [InlineData("say \"hi\"", "\"say \"\"hi\"\"\"")]
    [InlineData("line1\nline2", "\"line1\nline2\"")]
    [InlineData(null, "")]
    [InlineData("", "")]
    public void Ordinary_values_are_quoted_only_when_required(string? input, string expected)
        => Assert.Equal(expected, Csv.Escape(input));

    [Fact]
    public void Row_formats_dates_invariantly_and_handles_nulls()
    {
        var row = Csv.Row(new DateTime(2026, 10, 20, 18, 5, 9, DateTimeKind.Utc), null, 3.5, "x");
        Assert.Equal("2026-10-20 18:05:09Z,,3.5,x", row);
    }
}
