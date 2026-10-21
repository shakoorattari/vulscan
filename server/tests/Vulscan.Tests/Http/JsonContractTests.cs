using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Vulscan.Domain.Enums;

namespace Vulscan.Tests.Http;

public class JsonContractTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _f;
    public JsonContractTests(ApiFactory factory) => _f = factory;

    [Fact]
    public async Task Timestamps_are_always_emitted_as_utc_with_a_Z_suffix()
    {
        // EF returns DateTime with Kind=Unspecified; serialised naively that has no offset, and browsers then parse
        // it as LOCAL time — every timestamp in the UI (and every "x hours ago") would be off by the viewer's UTC offset.
        await _f.AddUserAsync("tz-admin", UserRole.Admin);
        var admin = await _f.ClientForAsync("tz-admin");

        var res = await admin.GetAsync("/api/v1/users");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var raw = await res.Content.ReadAsStringAsync();

        var stamps = Regex.Matches(raw, "\"(?:createdAt|lastLoginAt)\":\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToList();
        Assert.NotEmpty(stamps);
        Assert.All(stamps, s => Assert.EndsWith("Z", s));
        Assert.All(stamps, s => Assert.True(DateTime.TryParse(s, null, System.Globalization.DateTimeStyles.RoundtripKind, out var d) && d.Kind == DateTimeKind.Utc));
        _ = JsonDocument.Parse(raw);
    }
}
