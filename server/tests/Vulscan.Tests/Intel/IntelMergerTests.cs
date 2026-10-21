using Vulscan.Application.DTOs.Intel;
using Vulscan.Infrastructure.Clients;
using Vulscan.Infrastructure.Intel;

namespace Vulscan.Tests.Intel;

public class IntelMergerTests
{
    private static readonly DateTime Now = new(2026, 10, 21, 8, 0, 0, DateTimeKind.Utc);

    private static OsvVulnerability Osv(string file) => Fixtures.Json<OsvVulnerability>(file);
    private static NvdCve Nvd(string file) => Fixtures.Json<NvdResponse>(file).Vulnerabilities[0].Cve!;

    private static KevCatalog Catalog() => new(
        System.Text.Json.JsonSerializer.Deserialize<KevFileShape>(Fixtures.Text("kev-sample.json"), new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!.Vulnerabilities
            .ToDictionary(v => v.CveID, v => new KevEntry(
                v.CveID, v.VendorProject, v.Product, v.VulnerabilityName, v.ShortDescription,
                DateTime.Parse(v.DateAdded), DateTime.Parse(v.DueDate), v.RequiredAction, v.KnownRansomwareCampaignUse, v.Notes)),
        null, DateTimeOffset.UtcNow);

    // The shape of the CISA file (property names as published).
    private sealed record KevFileShape(List<KevRowShape> Vulnerabilities);
    private sealed record KevRowShape(
        string CveID, string VendorProject, string Product, string VulnerabilityName, string ShortDescription,
        string DateAdded, string DueDate, string RequiredAction, string KnownRansomwareCampaignUse, string Notes);

    private static SourceResult<IReadOnlyDictionary<string, EpssScore>> Epss(params (string Cve, double Score, double Pct)[] rows) =>
        SourceResult<IReadOnlyDictionary<string, EpssScore>>.Ok(
            rows.ToDictionary(r => r.Cve, r => new EpssScore(r.Score, r.Pct, new DateTime(2026, 10, 20))));

    private static IntelInputs Uglify(
        SourceResult<NvdCve>? nvd = null, SourceResult<OsvVulnerability>? osv = null,
        SourceResult<IReadOnlyDictionary<string, EpssScore>>? epss = null) =>
        new("CVE-2015-8857", "CVE-2015-8857",
            osv ?? SourceResult<OsvVulnerability>.Ok(Osv("osv-CVE-2015-8857.json")),
            [Osv("osv-GHSA-34r7-q49f-h37c.json")],
            nvd ?? SourceResult<NvdCve>.Ok(Nvd("nvd-CVE-2015-8857.json")),
            epss ?? Epss(("CVE-2015-8857", 0.03559, 0.89202)),
            SourceResult<KevCatalog>.Ok(Catalog()),
            Now);

    // ── a real CVE, all four sources ───────────────────────────────────────────

    [Fact]
    public void Merges_title_description_dates_and_aliases()
    {
        var r = IntelMerger.Merge(Uglify());
        var dto = r.Dto;

        Assert.True(r.HasData);
        Assert.False(r.NeedsRetry);
        Assert.Equal("Incorrect Handling of Non-Boolean Comparisons During Minification in uglify-js", dto.Title);

        // GitHub's write-up (Markdown, with upgrade advice) beats NVD's bare one-liner.
        Assert.Equal("markdown", dto.DescriptionFormat);
        Assert.Equal("GitHub Advisory", dto.DescriptionSource);
        Assert.Contains("Upgrade UglifyJS to version >= 2.4.24", dto.Description);

        Assert.Equal(new DateTime(2017, 1, 23, 21, 59, 0, 580, DateTimeKind.Utc), dto.Published);    // NVD's publication date
        Assert.Equal(new DateTime(2026, 6, 17, 0, 35, 22, 707, DateTimeKind.Utc), dto.Modified);     // the latest change anywhere
        Assert.Equal(["GHSA-34r7-q49f-h37c"], dto.Aliases);
        Assert.Equal(Now, dto.FetchedAt);
    }

    [Fact]
    public void Scores_come_from_the_vectors_with_nvds_own_analysis_first()
    {
        var dto = IntelMerger.Merge(Uglify()).Dto;

        Assert.Equal("Critical", dto.Severity);
        Assert.Equal(9.8, dto.Score);
        Assert.Equal("Critical", dto.VendorSeverity);                       // GitHub's own label

        var v31 = dto.Cvss[0];
        Assert.True(v31.IsPrimary);
        Assert.Equal("3.1", v31.Version);
        Assert.Equal("CVSS:3.1/AV:N/AC:L/PR:N/UI:N/S:U/C:H/I:H/A:H", v31.Vector);
        Assert.Equal(9.8, v31.Score);
        Assert.Contains("NVD", v31.Source);
        Assert.Contains("CVE record (via OSV)", v31.Source);                // the identical vector from two places is one rating
        Assert.Equal(8, v31.Metrics.Count);

        var v2 = Assert.Single(dto.Cvss, c => c.Version == "2.0");
        Assert.False(v2.IsPrimary);
        Assert.Equal(7.5, v2.Score);
        Assert.Equal("High", v2.Severity);
        Assert.Equal(2, dto.Cvss.Count);
    }

    [Fact]
    public void Collects_weaknesses_from_every_source_with_links_to_cwe()
    {
        var dto = IntelMerger.Merge(Uglify()).Dto;

        Assert.Equal(["CWE-1254", "CWE-254", "CWE-670"], dto.Weaknesses.Select(w => w.Id).OrderBy(x => x, StringComparer.Ordinal));
        var nvd = dto.Weaknesses.Single(w => w.Id == "CWE-254");
        Assert.Equal("https://cwe.mitre.org/data/definitions/254.html", nvd.Url);
        Assert.Equal(["NVD"], nvd.Sources);
        Assert.Equal(["GitHub"], dto.Weaknesses.Single(w => w.Id == "CWE-670").Sources);
    }

    [Fact]
    public void References_are_deduplicated_across_sources_and_classified()
    {
        var dto = IntelMerger.Merge(Uglify()).Dto;

        var urls = dto.References.Select(r => r.Url).ToList();
        Assert.Equal(urls.Count, urls.Distinct(StringComparer.OrdinalIgnoreCase).Count());

        // OSV calls nodesecurity.io ADVISORY/EVIDENCE/FIX, NVD tags it Patch/Exploit/Vendor Advisory: one entry, all of it.
        var nodesec = Assert.Single(dto.References, r => r.Url == "https://nodesecurity.io/advisories/39");
        Assert.Equal("fix", nodesec.Kinds[0]);                              // the most useful kind leads
        Assert.Contains("advisory", nodesec.Kinds);
        Assert.Contains("exploit", nodesec.Kinds);
        Assert.Equal(["OSV", "NVD"], nodesec.Sources);
        Assert.Contains("Patch", nodesec.Labels);
        Assert.Equal("nodesecurity.io", nodesec.Host);

        // Fixes are listed before everything else.
        Assert.Equal("fix", dto.References[0].Kinds[0]);
        Assert.Contains(dto.References, r => r.Kinds.Contains("package") && r.Url == "https://github.com/mishoo/UglifyJS2");
    }

    [Fact]
    public void Lists_affected_packages_with_ranges_and_nvd_products()
    {
        var dto = IntelMerger.Merge(Uglify()).Dto;

        var npm = Assert.Single(dto.Affected, a => a.Ecosystem == "npm");
        Assert.Equal("uglify-js", npm.Name);
        Assert.Equal("pkg:npm/uglify-js", npm.Purl);
        Assert.Equal(new AffectedRangeDto { Introduced = "0", Fixed = "2.4.24" }, Assert.Single(npm.Ranges));

        var gem = Assert.Single(dto.Affected, a => a.Ecosystem == "RubyGems");
        Assert.Equal("2.7.2", gem.Ranges[0].Fixed);
        Assert.Equal(44, gem.VersionsListed);                                // the versions list is counted, not returned

        var product = Assert.Single(dto.Products);
        Assert.Equal("uglifyjs_project", product.Vendor);
        Assert.Equal("uglifyjs", product.Product);
        Assert.Equal("2.4.24", product.EndExcluding);
    }

    [Fact]
    public void Includes_epss_and_records_that_the_cve_is_not_in_kev()
    {
        var dto = IntelMerger.Merge(Uglify()).Dto;

        Assert.Equal(0.03559, dto.Epss!.Score);
        Assert.Equal(0.89202, dto.Epss.Percentile);
        Assert.Null(dto.Kev);

        Assert.Equal(["OSV", "NVD", "EPSS", "CISA KEV"], dto.Sources.Select(s => s.Name));
        Assert.All(dto.Sources, s => Assert.Equal("ok", s.Status));
        Assert.Equal("https://osv.dev/vulnerability/CVE-2015-8857", dto.Sources[0].Url);
        Assert.Equal("https://nvd.nist.gov/vuln/detail/CVE-2015-8857", dto.Sources[1].Url);
        Assert.Equal("Not in the catalog.", dto.Sources[3].Message);
    }

    // ── Log4Shell: requested by its GitHub id, KEV-listed ─────────────────────

    [Fact]
    public void A_github_id_is_resolved_to_its_cve_for_nvd_epss_and_kev()
    {
        var input = new IntelInputs(
            "GHSA-jfh8-c2jp-5v3q", "CVE-2021-44228",
            SourceResult<OsvVulnerability>.Ok(Osv("osv-GHSA-jfh8-c2jp-5v3q.json")), [],
            SourceResult<NvdCve>.Ok(Nvd("nvd-CVE-2021-44228.json")),
            Epss(("CVE-2021-44228", 0.99999, 1.0)),
            SourceResult<KevCatalog>.Ok(Catalog()), Now);

        var dto = IntelMerger.Merge(input).Dto;

        Assert.Equal("GHSA-jfh8-c2jp-5v3q", dto.Id);
        Assert.Contains("CVE-2021-44228", dto.Aliases);
        Assert.Equal(10.0, dto.Score);

        var kev = dto.Kev!;
        Assert.Equal("Apache", kev.VendorProject);
        Assert.Equal("Log4j2", kev.Product);
        Assert.Equal(new DateTime(2021, 12, 10), kev.DateAdded);
        Assert.Equal(new DateTime(2021, 12, 24), kev.DueDate);
        Assert.Equal("Known", kev.RansomwareUse);
        Assert.Contains("remove affected assets", kev.RequiredAction);

        Assert.Equal(0.99999, dto.Epss!.Score);

        // Three separate affected ranges for log4j-core are one package with three ranges.
        var core = Assert.Single(dto.Affected, a => a.Name == "org.apache.logging.log4j:log4j-core");
        Assert.Equal(3, core.Ranges.Count);
        Assert.Contains(core.Ranges, r => r is { Introduced: "2.13.0", Fixed: "2.15.0" });
    }

    // ── sources failing ────────────────────────────────────────────────────────

    [Fact]
    public void A_failed_source_is_reported_without_losing_what_the_others_returned()
    {
        var r = IntelMerger.Merge(Uglify(nvd: SourceResult<NvdCve>.Error("NVD unreachable: HttpRequestException.")));

        Assert.True(r.HasData);
        Assert.True(r.NeedsRetry);                                          // retried soon
        Assert.Contains("uglify-js", r.Dto.Title);                          // OSV's content is all there
        var nvd = Assert.Single(r.Dto.Sources, s => s.Name == "NVD");
        Assert.Equal("error", nvd.Status);
        Assert.Contains("unreachable", nvd.Message);

        // The vector still arrives via the OSV copy of the CVE record, so there is a score even without NVD.
        Assert.Equal(9.8, r.Dto.Score);
        Assert.DoesNotContain(r.Dto.Cvss, c => c.Version == "2.0");         // the v2 rating was NVD-only
    }

    [Fact]
    public void An_nvd_rate_limit_skip_also_schedules_a_retry()
    {
        var r = IntelMerger.Merge(Uglify(nvd: SourceResult<NvdCve>.Skipped("NVD rate limit reached; will be fetched in the background.")));

        Assert.True(r.NeedsRetry);
        Assert.Equal("skipped", r.Dto.Sources.Single(s => s.Name == "NVD").Status);
    }

    [Fact]
    public void An_id_no_source_knows_has_no_data_and_is_not_a_failure()
    {
        var r = IntelMerger.Merge(new IntelInputs(
            "CVE-2099-0001", "CVE-2099-0001",
            SourceResult<OsvVulnerability>.NotFound(), [],
            SourceResult<NvdCve>.NotFound(), Epss(), SourceResult<KevCatalog>.Ok(Catalog()), Now));

        Assert.False(r.HasData);
        Assert.False(r.NeedsRetry);
    }

    [Fact]
    public void A_non_cve_advisory_without_a_cve_alias_skips_the_cve_only_sources()
    {
        var osv = new OsvVulnerability { Id = "GO-2022-0001", Summary = "A Go advisory", Details = "Details." };
        var r = IntelMerger.Merge(new IntelInputs(
            "GO-2022-0001", null, SourceResult<OsvVulnerability>.Ok(osv), [],
            SourceResult<NvdCve>.Skipped("Not a CVE."), Epss(), SourceResult<KevCatalog>.Skipped("Not a CVE."), Now));

        Assert.True(r.HasData);
        Assert.False(r.NeedsRetry);                                         // "not a CVE" is not a failure to retry
        Assert.Equal(["OSV", "NVD"], r.Dto.Sources.Select(s => s.Name));
        Assert.Equal("not_applicable", r.Dto.Sources[1].Status);
        Assert.Contains("do not apply", r.Dto.Sources[1].Message);
    }

    // ── untrusted data ─────────────────────────────────────────────────────────

    [Fact]
    public void Only_plain_http_and_https_links_survive()
    {
        var osv = new OsvVulnerability
        {
            Id = "GHSA-aaaa-bbbb-cccc",
            Summary = "x",
            References =
            [
                new() { Type = "WEB", Url = "javascript:alert(document.cookie)" },
                new() { Type = "WEB", Url = "JaVaScRiPt:alert(1)" },
                new() { Type = "WEB", Url = "data:text/html,<script>alert(1)</script>" },
                new() { Type = "WEB", Url = "vbscript:msgbox(1)" },
                new() { Type = "WEB", Url = "file:///etc/passwd" },
                new() { Type = "WEB", Url = "ftp://example.org/x" },
                new() { Type = "WEB", Url = "//evil.example/x" },
                new() { Type = "WEB", Url = "/relative/path" },
                new() { Type = "WEB", Url = "https://user:password@example.org/secret" },
                new() { Type = "WEB", Url = "https://example.org/has space" },
                new() { Type = "WEB", Url = "https://example.org/new\nline" },
                new() { Type = "WEB", Url = "https://example.org/" + new string('a', 2100) },
                new() { Type = "WEB", Url = "   " },
                new() { Type = "WEB", Url = null },
                new() { Type = "FIX", Url = "https://github.com/org/repo/commit/abc123" },
                new() { Type = "ADVISORY", Url = "http://example.org/advisory" },
            ],
        };

        var dto = IntelMerger.Merge(new IntelInputs(
            "GHSA-aaaa-bbbb-cccc", null, SourceResult<OsvVulnerability>.Ok(osv), [],
            SourceResult<NvdCve>.Skipped("-"), Epss(), SourceResult<KevCatalog>.Skipped("-"), Now)).Dto;

        Assert.Equal(["https://github.com/org/repo/commit/abc123", "http://example.org/advisory"], dto.References.Select(r => r.Url));
    }

    [Fact]
    public void Urls_that_differ_only_by_trailing_slash_or_fragment_are_one_reference()
    {
        var osv = new OsvVulnerability
        {
            Id = "GHSA-aaaa-bbbb-cccc",
            References =
            [
                new() { Type = "WEB", Url = "https://Example.org/path/" },
                new() { Type = "ADVISORY", Url = "https://example.org/path" },
                new() { Type = "WEB", Url = "https://example.org/path?id=1" },     // a different page
            ],
        };

        var dto = IntelMerger.Merge(new IntelInputs(
            "GHSA-aaaa-bbbb-cccc", null, SourceResult<OsvVulnerability>.Ok(osv), [],
            SourceResult<NvdCve>.Skipped("-"), Epss(), SourceResult<KevCatalog>.Skipped("-"), Now)).Dto;

        Assert.Equal(2, dto.References.Count);
    }

    [Fact]
    public void A_cvss_4_vector_is_listed_but_does_not_become_the_primary_score_over_3_x()
    {
        var osv = new OsvVulnerability
        {
            Id = "GHSA-aaaa-bbbb-cccc",
            Severity =
            [
                new() { Type = "CVSS_V4", Score = "CVSS:4.0/AV:N/AC:L/AT:N/PR:N/UI:N/VC:H/VI:H/VA:H/SC:N/SI:N/SA:N" },
                new() { Type = "CVSS_V3", Score = "CVSS:3.1/AV:N/AC:L/PR:N/UI:N/S:U/C:H/I:H/A:H" },
            ],
        };

        var dto = IntelMerger.Merge(new IntelInputs(
            "GHSA-aaaa-bbbb-cccc", null, SourceResult<OsvVulnerability>.Ok(osv), [],
            SourceResult<NvdCve>.Skipped("-"), Epss(), SourceResult<KevCatalog>.Skipped("-"), Now)).Dto;

        Assert.Equal(["3.1", "4.0"], dto.Cvss.Select(c => c.Version));
        Assert.Equal(9.8, dto.Score);
        Assert.Null(dto.Cvss[1].Score);                                     // not guessed
    }

    [Fact]
    public void Garbage_vectors_are_dropped_rather_than_scored()
    {
        var osv = new OsvVulnerability
        {
            Id = "GHSA-aaaa-bbbb-cccc",
            Severity = [new() { Type = "CVSS_V3", Score = "9.8" }, new() { Type = "CVSS_V3", Score = "CVSS:3.1/AV:N" }],
        };

        var dto = IntelMerger.Merge(new IntelInputs(
            "GHSA-aaaa-bbbb-cccc", null, SourceResult<OsvVulnerability>.Ok(osv), [],
            SourceResult<NvdCve>.Skipped("-"), Epss(), SourceResult<KevCatalog>.Skipped("-"), Now)).Dto;

        Assert.Empty(dto.Cvss);
        Assert.Null(dto.Score);
    }

    [Fact]
    public void With_only_nvd_the_description_is_plain_text_attributed_to_nvd()
    {
        var r = IntelMerger.Merge(new IntelInputs(
            "CVE-2015-8857", "CVE-2015-8857", SourceResult<OsvVulnerability>.NotFound(), [],
            SourceResult<NvdCve>.Ok(Nvd("nvd-CVE-2015-8857.json")), Epss(), SourceResult<KevCatalog>.Ok(Catalog()), Now));

        Assert.Equal("text", r.Dto.DescriptionFormat);
        Assert.Equal("NVD", r.Dto.DescriptionSource);
        Assert.StartsWith("The uglify-js package before 2.4.24", r.Dto.Description);
        Assert.StartsWith("The uglify-js package before 2.4.24 for Node.js does not properly account for non-boolean values", r.Dto.Title);
        Assert.Equal(9.8, r.Dto.Score);
        Assert.True(r.HasData);
    }
}
