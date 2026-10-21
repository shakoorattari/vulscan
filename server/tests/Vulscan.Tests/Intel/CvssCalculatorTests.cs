using System.Text.Json;
using Vulscan.Application.Intel;

namespace Vulscan.Tests.Intel;

public class CvssCalculatorTests
{
    [Theory]
    // Well-known scores, checked against the FIRST CVSS v3.1 calculator.
    [InlineData("CVSS:3.1/AV:N/AC:L/PR:N/UI:N/S:U/C:H/I:H/A:H", 9.8, "Critical")]   // typical unauthenticated RCE
    [InlineData("CVSS:3.1/AV:N/AC:L/PR:N/UI:R/S:C/C:L/I:L/A:N", 6.1, "Medium")]     // reflected XSS
    [InlineData("CVSS:3.1/AV:N/AC:L/PR:L/UI:N/S:C/C:H/I:H/A:H", 9.9, "Critical")]   // changed scope uses different PR weights
    [InlineData("CVSS:3.1/AV:L/AC:L/PR:L/UI:N/S:U/C:H/I:H/A:H", 7.8, "High")]
    [InlineData("CVSS:3.1/AV:N/AC:L/PR:N/UI:N/S:U/C:L/I:N/A:N", 5.3, "Medium")]
    [InlineData("CVSS:3.1/AV:P/AC:H/PR:H/UI:R/S:U/C:L/I:N/A:N", 1.6, "Low")]
    [InlineData("CVSS:3.1/AV:N/AC:L/PR:N/UI:N/S:C/C:H/I:H/A:H", 10.0, "Critical")]
    [InlineData("CVSS:3.0/AV:N/AC:L/PR:N/UI:N/S:U/C:H/I:H/A:H", 9.8, "Critical")]
    public void Computes_cvss_3_base_scores(string vector, double score, string severity)
    {
        var result = CvssCalculator.Parse(vector)!;

        Assert.Equal(score, result.Score);
        Assert.Equal(severity, result.Severity);
        Assert.Equal(vector, result.Vector);
    }

    [Fact]
    public void No_impact_scores_zero_regardless_of_exploitability()
    {
        var result = CvssCalculator.Parse("CVSS:3.1/AV:N/AC:L/PR:N/UI:N/S:U/C:N/I:N/A:N")!;

        Assert.Equal(0.0, result.Score);
        Assert.Equal("None", result.Severity);
    }

    [Theory]
    [InlineData("AV:N/AC:L/Au:N/C:P/I:P/A:P", 7.5, "High")]
    [InlineData("AV:N/AC:L/Au:N/C:C/I:C/A:C", 10.0, "High")]
    [InlineData("AV:N/AC:M/Au:N/C:N/I:N/A:P", 4.3, "Medium")]
    [InlineData("AV:L/AC:H/Au:M/C:N/I:N/A:P", 0.8, "Low")]
    public void Computes_cvss_2_base_scores(string vector, double score, string severity)
    {
        var result = CvssCalculator.Parse(vector)!;

        Assert.Equal("2.0", result.Version);
        Assert.Equal(score, result.Score);
        Assert.Equal(severity, result.Severity);
    }

    [Fact]
    public void Decodes_metrics_into_readable_names_and_values()
    {
        var result = CvssCalculator.Parse("CVSS:3.1/AV:N/AC:L/PR:N/UI:R/S:C/C:L/I:L/A:N")!;

        Assert.Equal(8, result.Metrics.Count);
        Assert.Contains(result.Metrics, m => m is { Code: "AV", Name: "Attack Vector", Value: "Network" });
        Assert.Contains(result.Metrics, m => m is { Code: "UI", Value: "Required" });
        Assert.Contains(result.Metrics, m => m is { Code: "S", Value: "Changed" });
    }

    [Fact]
    public void Cvss_4_is_decoded_but_not_scored()
    {
        // The 4.0 score comes from a table of pre-computed macro-vectors, so we report the metrics and leave the
        // score to the source that publishes it, rather than guess.
        var result = CvssCalculator.Parse("CVSS:4.0/AV:N/AC:L/AT:N/PR:N/UI:N/VC:H/VI:H/VA:H/SC:N/SI:N/SA:N")!;

        Assert.Equal("4.0", result.Version);
        Assert.Null(result.Score);
        Assert.Null(result.Severity);
        Assert.Equal(11, result.Metrics.Count);
        Assert.Contains(result.Metrics, m => m is { Code: "VC", Value: "High" });
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a vector")]
    [InlineData("CVSS:3.1/AV:N/AC:L/PR:N/UI:N/S:U/C:H/I:H")]                // missing a metric
    [InlineData("CVSS:3.1/AV:X/AC:L/PR:N/UI:N/S:U/C:H/I:H/A:H")]            // invalid value
    [InlineData("CVSS:3.1/AV:N/AV:L/AC:L/PR:N/UI:N/S:U/C:H/I:H/A:H")]       // repeated metric
    [InlineData("CVSS:5.0/AV:N/AC:L/PR:N/UI:N/S:U/C:H/I:H/A:H")]            // unknown version
    [InlineData("AV:N/AC:L/Au:N/C:P/I:P")]                                  // truncated v2
    public void Rejects_malformed_vectors(string? vector) => Assert.Null(CvssCalculator.Parse(vector));

    [Fact]
    public void Temporal_and_environmental_metrics_do_not_change_the_base_score()
    {
        var plain = CvssCalculator.Parse("CVSS:3.1/AV:N/AC:L/PR:N/UI:N/S:U/C:H/I:H/A:H")!;
        var withTemporal = CvssCalculator.Parse("CVSS:3.1/AV:N/AC:L/PR:N/UI:N/S:U/C:H/I:H/A:H/E:P/RL:O/RC:C")!;

        Assert.Equal(plain.Score, withTemporal.Score);
    }

    private sealed record Sample(string Version, string Vector, double Score);

    /// <summary>
    /// 279 distinct vectors taken from NVD, each with the base score NVD published for it. If the arithmetic or
    /// rounding were off anywhere — including the CVSS 3.1 Roundup edge cases — one of these would fail.
    /// </summary>
    [Fact]
    public void Agrees_with_every_score_NVD_published_for_real_vectors()
    {
        var corpus = JsonSerializer.Deserialize<List<Sample>>(Fixtures.Text("cvss-vectors.json"),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.True(corpus.Count > 200);
        Assert.Contains(corpus, s => s.Version == "3.1");
        Assert.Contains(corpus, s => s.Version == "3.0");
        Assert.Contains(corpus, s => s.Version == "2.0");

        var wrong = corpus
            .Select(s => (Sample: s, Computed: CvssCalculator.Parse(s.Vector)?.Score))
            .Where(x => x.Computed != x.Sample.Score)
            .Select(x => $"{x.Sample.Vector}: NVD {x.Sample.Score}, computed {x.Computed?.ToString() ?? "null"}")
            .ToList();

        Assert.True(wrong.Count == 0, $"{wrong.Count} of {corpus.Count} scores differ from NVD:\n{string.Join('\n', wrong.Take(10))}");
    }
}
