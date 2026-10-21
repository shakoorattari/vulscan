using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Vulscan.Infrastructure.Clients;

/// <summary>
/// Outbound calls to third-party services (OSV, NVD, EPSS, CISA, Microsoft sign-in metadata) from a network that
/// only reaches the internet through a proxy. Set <c>Network:ProxyUrl</c> (credentials may be given as
/// <c>http://user:pass@host:3128</c>). Unset, the machine's default proxy settings apply.
/// </summary>
public static class OutboundHttp
{
    /// <summary>One unit of retry back-off (attempt n waits n × 2 units). Tests shrink it.</summary>
    internal static TimeSpan RetryDelayUnit { get; set; } = TimeSpan.FromSeconds(1);

    public static IHttpClientBuilder UseOutboundProxy(this IHttpClientBuilder builder, IConfiguration configuration)
    {
        var proxyUrl = configuration["Network:ProxyUrl"];
        if (string.IsNullOrWhiteSpace(proxyUrl)) return builder;

        var proxy = CreateProxy(proxyUrl);
        return builder.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            Proxy = proxy,
            UseProxy = true,
            PooledConnectionLifetime = TimeSpan.FromMinutes(15),
        });
    }

    public static IWebProxy CreateProxy(string proxyUrl)
    {
        var uri = new Uri(proxyUrl);
        var proxy = new WebProxy(new Uri($"{uri.Scheme}://{uri.Host}:{uri.Port}"));
        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            var parts = uri.UserInfo.Split(':', 2);
            proxy.Credentials = new NetworkCredential(
                Uri.UnescapeDataString(parts[0]),
                parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty);
        }
        return proxy;
    }

    /// <summary>
    /// Sends a request, retrying transient failures (connection errors, timeouts, 5xx and 429) a couple of times with
    /// a short back-off. The final response is returned as-is so the caller can decide what a 4xx means.
    /// </summary>
    public static async Task<HttpResponseMessage> SendWithRetryAsync(
        HttpClient client, Func<HttpRequestMessage> request, int attempts, bool retryOnTooManyRequests, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var response = await client.SendAsync(request(), HttpCompletionOption.ResponseHeadersRead, ct);
                var transient = (int)response.StatusCode >= 500 || (retryOnTooManyRequests && response.StatusCode == HttpStatusCode.TooManyRequests);
                if (!transient || attempt >= attempts) return response;

                var wait = response.Headers.RetryAfter?.Delta ?? RetryDelayUnit * (attempt * 2);
                response.Dispose();
                await Task.Delay(wait > TimeSpan.FromSeconds(10) ? TimeSpan.FromSeconds(10) : wait, ct);
            }
            catch (HttpRequestException) when (attempt < attempts)
            {
                await Task.Delay(RetryDelayUnit * (attempt * 2), ct);
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested && attempt < attempts)
            {
                // The client's own timeout, not the caller cancelling.
                await Task.Delay(RetryDelayUnit * attempt, ct);
            }
        }
    }
}
