using System.Net;
using System.Text.Json;

namespace Vulscan.Tests.Http;

public class RateLimitAndStartupTests
{
    [Fact]
    public async Task Credential_endpoints_are_rate_limited_per_client_with_a_json_429()
    {
        using var f = new ApiFactory(authRateLimitPerMinute: 3);
        var statuses = new List<HttpStatusCode>();
        HttpResponseMessage? last = null;

        for (var i = 0; i < 5; i++)
        {
            var (res, _) = await f.PostAsync(f.CreateClient(), "/api/v1/auth/login", new { username = "nobody", password = "Wrong-Password-1!" });
            statuses.Add(res.StatusCode);
            last = res;
        }

        Assert.Equal([HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized,
                      HttpStatusCode.TooManyRequests, HttpStatusCode.TooManyRequests], statuses);
        Assert.Equal("60", last!.Headers.RetryAfter!.Delta!.Value.TotalSeconds.ToString());
        var body = JsonDocument.Parse(await last.Content.ReadAsStringAsync()).RootElement;
        Assert.False(body.GetProperty("success").GetBoolean());
        Assert.Contains("Too many attempts", body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Other_endpoints_are_not_affected_by_the_auth_rate_limit()
    {
        using var f = new ApiFactory(authRateLimitPerMinute: 1);
        await f.PostAsync(f.CreateClient(), "/api/v1/auth/login", new { username = "x", password = "y-Wrong-Password-1!" });
        await f.PostAsync(f.CreateClient(), "/api/v1/auth/login", new { username = "x", password = "y-Wrong-Password-1!" });   // now limited

        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.OK, (await f.CreateClient().GetAsync("/api/v1/health")).StatusCode);
    }

    [Theory]
    [InlineData("${JWT_SECRET_KEY}")]                 // the literal placeholder shipped in appsettings.json
    [InlineData("too-short-key")]
    public void The_api_refuses_to_start_with_a_placeholder_or_weak_signing_key(string key)
    {
        using var f = new ApiFactory(1000, key);
        try
        {
            // Program.cs logs the fatal error and sets a non-zero exit code instead of serving requests.
            Assert.ThrowsAny<Exception>(() => f.CreateClient());
        }
        finally
        {
            Environment.ExitCode = 0;   // Program.cs signals failure through the exit code; don't let it leak into the test host
        }
    }
}
