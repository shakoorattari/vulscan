using System.Net;
using System.Text;

namespace Vulscan.Tests.Support;

/// <summary>An <see cref="HttpMessageHandler"/> that answers from a function and remembers every request.</summary>
public sealed class FakeHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
{
    private readonly object _gate = new();
    private readonly List<(HttpMethod Method, Uri Uri, Dictionary<string, string> Headers)> _requests = [];

    public IReadOnlyList<(HttpMethod Method, Uri Uri, Dictionary<string, string> Headers)> Requests
    {
        get { lock (_gate) return [.. _requests]; }
    }

    public int Count => Requests.Count;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            _requests.Add((request.Method, request.RequestUri!,
                request.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value), StringComparer.OrdinalIgnoreCase)));
        }
        return Task.FromResult(responder(request));
    }

    public static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    public static HttpResponseMessage Status(HttpStatusCode status) => new(status);

    public HttpClient Client(string baseAddress = "https://example.test/") =>
        new(this, disposeHandler: false) { BaseAddress = new Uri(baseAddress) };
}

public sealed class FakeHttpClientFactory(FakeHttpHandler handler) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => handler.Client();
}
