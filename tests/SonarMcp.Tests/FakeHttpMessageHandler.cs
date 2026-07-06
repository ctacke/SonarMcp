using System.Net;

namespace SonarMcp.Tests;

/// <summary>
/// Routes HTTP requests to a canned response by matching against the request's relative path
/// (ignoring query string), so <see cref="SonarMcp.Server.SonarQubeClient"/> can be tested against
/// realistic JSON payloads without hitting a real SonarQube server.
/// </summary>
internal sealed class FakeHttpMessageHandler(HttpStatusCode statusCode, string responseBody) : HttpMessageHandler
{
    public HttpRequestMessage? LastRequest { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        LastRequest = request;
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(responseBody),
        };
        return Task.FromResult(response);
    }
}
