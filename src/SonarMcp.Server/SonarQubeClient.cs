using System.Text.Json.Nodes;

namespace SonarMcp.Server;

public sealed record SonarIssue(
    string Key,
    string Rule,
    string? Severity,
    string? Type,
    string? Status,
    string Component,
    int? Line,
    string? Message,
    string? Effort,
    IReadOnlyList<string> Tags);

public sealed record SonarIssueSearchResult(int Total, IReadOnlyList<SonarIssue> Issues);

public sealed record SonarQualityGateCondition(
    string Metric,
    string Comparator,
    string? ErrorThreshold,
    string? ActualValue,
    string Status);

public sealed record SonarQualityGateStatus(string Status, IReadOnlyList<SonarQualityGateCondition> Conditions);

/// <summary>
/// Thin wrapper over the SonarQube Web API (issues + quality gate), authenticated via a PAT set as the
/// HttpClient's Basic auth header (see Program.cs). Parses just the fields the MCP tools need — no full
/// response model, given how small the surface is.
/// </summary>
public sealed class SonarQubeClient(HttpClient http, SonarQubeConfig config)
{
    /// <summary>Resolves the effective project key, falling back to the server's configured default.</summary>
    public string ResolveProjectKey(string? projectKey)
    {
        if (!string.IsNullOrWhiteSpace(projectKey))
            return projectKey;
        if (!string.IsNullOrWhiteSpace(config.DefaultProjectKey))
            return config.DefaultProjectKey;
        throw new InvalidOperationException(
            "No project key given and SONAR_PROJECT_KEY is not set. Pass 'projectKey' explicitly.");
    }

    public async Task<SonarIssueSearchResult> SearchIssuesAsync(
        string? projectKey,
        string? branch,
        string statuses,
        bool sinceLeakPeriod,
        string? severities,
        string? types,
        string? assignees = null,
        CancellationToken ct = default)
    {
        var key = ResolveProjectKey(projectKey);
        var query = new List<string>
        {
            $"componentKeys={Uri.EscapeDataString(key)}",
            $"issueStatuses={Uri.EscapeDataString(statuses)}",
            $"sinceLeakPeriod={(sinceLeakPeriod ? "true" : "false")}",
            "ps=500",
        };
        if (!string.IsNullOrWhiteSpace(branch))
            query.Add($"branch={Uri.EscapeDataString(branch)}");
        if (!string.IsNullOrWhiteSpace(config.Organization))
            query.Add($"organization={Uri.EscapeDataString(config.Organization)}");
        if (!string.IsNullOrWhiteSpace(severities))
            query.Add($"severities={Uri.EscapeDataString(severities)}");
        if (!string.IsNullOrWhiteSpace(types))
            query.Add($"types={Uri.EscapeDataString(types)}");
        if (!string.IsNullOrWhiteSpace(assignees))
            query.Add($"assignees={Uri.EscapeDataString(assignees)}");

        var json = await GetJsonAsync($"/api/issues/search?{string.Join('&', query)}", ct);

        var total = (int?)json["total"] ?? 0;
        var issues = new List<SonarIssue>();
        if (json["issues"] is JsonArray issuesArray)
        {
            foreach (var node in issuesArray)
            {
                if (node is null)
                {
                    continue;
                }

                var tags = new List<string>();
                if (node["tags"] is JsonArray tagsArray)
                {
                    foreach (var tag in tagsArray)
                    {
                        tags.Add((string?)tag ?? "");
                    }
                }

                issues.Add(new SonarIssue(
                    Key: (string?)node["key"] ?? "",
                    Rule: (string?)node["rule"] ?? "",
                    Severity: (string?)node["severity"],
                    Type: (string?)node["type"],
                    Status: (string?)node["status"],
                    Component: (string?)node["component"] ?? "",
                    Line: (int?)node["line"],
                    Message: (string?)node["message"],
                    Effort: (string?)node["effort"],
                    Tags: tags));
            }
        }

        return new SonarIssueSearchResult(total, issues);
    }

    public async Task<SonarQualityGateStatus> GetQualityGateStatusAsync(
        string? projectKey, string branch, CancellationToken ct)
    {
        var key = ResolveProjectKey(projectKey);
        var query = $"projectKey={Uri.EscapeDataString(key)}&branch={Uri.EscapeDataString(branch)}";

        var json = await GetJsonAsync($"/api/qualitygates/project_status?{query}", ct);
        var projectStatus = json["projectStatus"]
            ?? throw new InvalidOperationException("SonarQube response did not include 'projectStatus'.");

        var status = (string?)projectStatus["status"] ?? "UNKNOWN";
        var conditions = new List<SonarQualityGateCondition>();
        if (projectStatus["conditions"] is JsonArray conditionsArray)
        {
            foreach (var node in conditionsArray)
            {
                if (node is null)
                {
                    continue;
                }

                conditions.Add(new SonarQualityGateCondition(
                    Metric: (string?)node["metricKey"] ?? "",
                    Comparator: (string?)node["comparator"] ?? "",
                    ErrorThreshold: (string?)node["errorThreshold"],
                    ActualValue: (string?)node["actualValue"],
                    Status: (string?)node["status"] ?? ""));
            }
        }

        return new SonarQualityGateStatus(status, conditions);
    }

    /// <summary>Login of the user identified by the configured PAT — used to scope "assigned to me" issue searches.</summary>
    public async Task<string> GetCurrentUserLoginAsync(CancellationToken ct)
    {
        var json = await GetJsonAsync("/api/users/current", ct);
        return (string?)json["login"]
            ?? throw new InvalidOperationException("SonarQube response did not include 'login'.");
    }

    private async Task<JsonNode> GetJsonAsync(string relativeUrl, CancellationToken ct)
    {
        using var response = await http.GetAsync(relativeUrl, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"SonarQube API returned {(int)response.StatusCode} {response.ReasonPhrase} for {relativeUrl}: {body}");
        }

        return JsonNode.Parse(body) ?? throw new InvalidOperationException($"Empty response from {relativeUrl}.");
    }
}
