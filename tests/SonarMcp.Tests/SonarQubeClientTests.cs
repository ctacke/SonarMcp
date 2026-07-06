using System.Net;
using SonarMcp.Server;
using Xunit;

namespace SonarMcp.Tests;

public class SonarQubeClientTests
{
    private static SonarQubeConfig MakeConfig(string? defaultProjectKey = null, string? organization = null) => new()
    {
        Token = "test-token",
        Url = "https://sonar.example.com",
        Organization = organization,
        DefaultProjectKey = defaultProjectKey,
    };

    private static (SonarQubeClient client, FakeHttpMessageHandler handler) MakeClient(
        SonarQubeConfig config, HttpStatusCode statusCode, string responseBody)
    {
        var handler = new FakeHttpMessageHandler(statusCode, responseBody);
        var http = new HttpClient(handler) { BaseAddress = new Uri(config.Url) };
        return (new SonarQubeClient(http, config), handler);
    }

    [Fact]
    public void ResolveProjectKey_ExplicitKey_TakesPrecedenceOverDefault()
    {
        var (client, _) = MakeClient(MakeConfig(defaultProjectKey: "default-key"), HttpStatusCode.OK, "{}");
        Assert.Equal("explicit-key", client.ResolveProjectKey("explicit-key"));
    }

    [Fact]
    public void ResolveProjectKey_NoExplicitKey_FallsBackToDefault()
    {
        var (client, _) = MakeClient(MakeConfig(defaultProjectKey: "default-key"), HttpStatusCode.OK, "{}");
        Assert.Equal("default-key", client.ResolveProjectKey(null));
    }

    [Fact]
    public void ResolveProjectKey_NoKeyAnywhere_Throws()
    {
        var (client, _) = MakeClient(MakeConfig(), HttpStatusCode.OK, "{}");
        var ex = Assert.Throws<InvalidOperationException>(() => client.ResolveProjectKey(null));
        Assert.Contains("SONAR_PROJECT_KEY", ex.Message);
    }

    [Fact]
    public async Task SearchIssuesAsync_ParsesIssuesAndTotal()
    {
        const string json = """
        {
          "total": 2,
          "issues": [
            {
              "key": "ISSUE-1",
              "rule": "csharpsquid:S1234",
              "severity": "CRITICAL",
              "type": "BUG",
              "status": "OPEN",
              "component": "myproj:src/Foo.cs",
              "line": 42,
              "message": "Fix this bug",
              "effort": "5min",
              "tags": ["bug", "suspicious"]
            },
            {
              "key": "ISSUE-2",
              "rule": "csharpsquid:S5678",
              "severity": "MINOR",
              "type": "CODE_SMELL",
              "status": "CONFIRMED",
              "component": "myproj:src/Bar.cs",
              "message": "Simplify this",
              "tags": []
            }
          ]
        }
        """;

        var (client, handler) = MakeClient(MakeConfig(), HttpStatusCode.OK, json);

        var result = await client.SearchIssuesAsync(
            "myproj", "main", "OPEN,CONFIRMED", sinceLeakPeriod: true, severities: null, types: null);

        Assert.Equal(2, result.Total);
        Assert.Equal(2, result.Issues.Count);

        var first = result.Issues[0];
        Assert.Equal("ISSUE-1", first.Key);
        Assert.Equal("CRITICAL", first.Severity);
        Assert.Equal(42, first.Line);
        Assert.Equal(["bug", "suspicious"], first.Tags);

        var second = result.Issues[1];
        Assert.Null(second.Line);
        Assert.Empty(second.Tags);

        Assert.Contains("componentKeys=myproj", handler.LastRequest!.RequestUri!.Query);
        Assert.Contains("branch=main", handler.LastRequest.RequestUri.Query);
    }

    [Fact]
    public async Task SearchIssuesAsync_IncludesOrganizationWhenConfigured()
    {
        var (client, handler) = MakeClient(
            MakeConfig(organization: "my-org"), HttpStatusCode.OK, """{"total":0,"issues":[]}""");

        await client.SearchIssuesAsync("proj", null, "OPEN", sinceLeakPeriod: false, null, null);

        Assert.Contains("organization=my-org", handler.LastRequest!.RequestUri!.Query);
    }

    [Fact]
    public async Task GetQualityGateStatusAsync_ParsesStatusAndConditions()
    {
        const string json = """
        {
          "projectStatus": {
            "status": "ERROR",
            "conditions": [
              { "metricKey": "new_coverage", "comparator": "LT", "errorThreshold": "80", "actualValue": "65", "status": "ERROR" },
              { "metricKey": "new_bugs", "comparator": "GT", "errorThreshold": "0", "actualValue": "0", "status": "OK" }
            ]
          }
        }
        """;

        var (client, _) = MakeClient(MakeConfig(defaultProjectKey: "proj"), HttpStatusCode.OK, json);

        var result = await client.GetQualityGateStatusAsync(null, "main", CancellationToken.None);

        Assert.Equal("ERROR", result.Status);
        Assert.Equal(2, result.Conditions.Count);
        Assert.Equal("new_coverage", result.Conditions[0].Metric);
        Assert.Equal("ERROR", result.Conditions[0].Status);
    }

    [Fact]
    public async Task GetCurrentUserLoginAsync_ReturnsLogin()
    {
        var (client, _) = MakeClient(MakeConfig(), HttpStatusCode.OK, """{"login":"alice"}""");
        var login = await client.GetCurrentUserLoginAsync(CancellationToken.None);
        Assert.Equal("alice", login);
    }

    [Fact]
    public async Task GetJsonAsync_NonSuccessStatus_ThrowsWithDetails()
    {
        var (client, _) = MakeClient(MakeConfig(defaultProjectKey: "proj"), HttpStatusCode.Unauthorized, "invalid token");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.GetQualityGateStatusAsync(null, "main", CancellationToken.None));

        Assert.Contains("401", ex.Message);
        Assert.Contains("invalid token", ex.Message);
    }
}
