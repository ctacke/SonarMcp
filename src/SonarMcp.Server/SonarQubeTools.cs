using System.ComponentModel;
using System.Text;
using ModelContextProtocol.Server;

namespace SonarMcp.Server;

/// <summary>
/// MCP tool surface over <see cref="SonarQubeClient"/>. Failures are caught and returned as detailed
/// "ERROR:" text rather than thrown, so the cause is visible in the calling chat host's tool output.
/// </summary>
[McpServerToolType]
public static class SonarQubeTools
{
    [McpServerTool(Name = "list_issues")]
    [Description("List open SonarQube issues for a branch (e.g. a PR branch). Mirrors the SonarQube dashboard's issue list.")]
    public static async Task<string> ListIssues(
        SonarQubeClient client,
        [Description("Branch name, e.g. feature/my-branch.")] string branch,
        [Description("SonarQube project key. Falls back to the server's configured default (SONAR_PROJECT_KEY) if omitted.")] string? projectKey = null,
        [Description("Comma-separated issue statuses, e.g. OPEN,CONFIRMED.")] string statuses = "OPEN,CONFIRMED",
        [Description("Only include issues introduced since the leak/new-code period (matches the dashboard's default PR view).")] bool sinceLeakPeriod = true,
        [Description("Comma-separated severities to filter by, e.g. BLOCKER,CRITICAL. Omit for all.")] string? severities = null,
        [Description("Comma-separated issue types to filter by, e.g. BUG,VULNERABILITY,CODE_SMELL. Omit for all.")] string? types = null,
        CancellationToken ct = default)
    {
        try
        {
            var result = await client.SearchIssuesAsync(projectKey, branch, statuses, sinceLeakPeriod, severities, types, ct: ct);
            return FormatIssues(result, $"branch '{branch}'", statuses);
        }
        catch (Exception ex)
        {
            return $"ERROR listing issues:\n{Describe(ex)}";
        }
    }

    [McpServerTool(Name = "list_my_issues")]
    [Description("List SonarQube issues assigned to the authenticated user (the owner of the configured SONAR_TOKEN), optionally scoped to a branch. Use this to answer \"what Sonar issues are assigned to me\".")]
    public static async Task<string> ListMyIssues(
        SonarQubeClient client,
        [Description("Branch name to scope the search to, e.g. feature/my-branch. Omit to search the project's main branch.")] string? branch = null,
        [Description("SonarQube project key. Falls back to the server's configured default (SONAR_PROJECT_KEY) if omitted.")] string? projectKey = null,
        [Description("Comma-separated issue statuses, e.g. OPEN,CONFIRMED.")] string statuses = "OPEN,CONFIRMED",
        [Description("Comma-separated severities to filter by, e.g. BLOCKER,CRITICAL. Omit for all.")] string? severities = null,
        [Description("Comma-separated issue types to filter by, e.g. BUG,VULNERABILITY,CODE_SMELL. Omit for all.")] string? types = null,
        CancellationToken ct = default)
    {
        try
        {
            var login = await client.GetCurrentUserLoginAsync(ct);
            var result = await client.SearchIssuesAsync(
                projectKey, branch, statuses, sinceLeakPeriod: false, severities, types, assignees: login, ct: ct);
            var scope = branch is null ? $"'{login}'" : $"'{login}' on branch '{branch}'";
            return FormatIssues(result, scope, statuses);
        }
        catch (Exception ex)
        {
            return $"ERROR listing issues assigned to current user:\n{Describe(ex)}";
        }
    }

    [McpServerTool(Name = "get_quality_gate_status")]
    [Description("Get the SonarQube quality gate status (pass/fail) for a branch, including any failing conditions. Use this to check whether a PR's SonarQube check failed.")]
    public static async Task<string> GetQualityGateStatus(
        SonarQubeClient client,
        [Description("Branch name, e.g. feature/my-branch.")] string branch,
        [Description("SonarQube project key. Falls back to the server's configured default (SONAR_PROJECT_KEY) if omitted.")] string? projectKey = null,
        CancellationToken ct = default)
    {
        try
        {
            var result = await client.GetQualityGateStatusAsync(projectKey, branch, ct);
            var sb = new StringBuilder();
            sb.AppendLine($"Quality gate for branch '{branch}': {result.Status}");
            foreach (var condition in result.Conditions.Where(c => c.Status != "OK"))
            {
                sb.AppendLine($"  FAILING: {condition.Metric} {condition.Comparator} {condition.ErrorThreshold} (actual: {condition.ActualValue})");
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"ERROR getting quality gate status:\n{Describe(ex)}";
        }
    }

    /// <summary>Renders a search result as the "N issue(s) for X" text shared by all issue-listing tools.</summary>
    private static string FormatIssues(SonarIssueSearchResult result, string scopeDescription, string statuses)
    {
        if (result.Issues.Count == 0)
        {
            return $"No issues found for {scopeDescription} (statuses: {statuses}).";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"{result.Total} issue(s) for {scopeDescription} (statuses: {statuses}):");
        foreach (var issue in result.Issues)
        {
            var location = issue.Line is int line ? $"{issue.Component}:{line}" : issue.Component;
            sb.AppendLine($"[{issue.Severity}] [{issue.Type}] {issue.Rule} — {location} — {issue.Message} ({issue.Status})");
        }

        return sb.ToString();
    }

    /// <summary>Formats an exception (and its inner chain) with stack trace for readable tool output.</summary>
    private static string Describe(Exception ex)
    {
        var sb = new StringBuilder();
        for (var e = ex; e is not null; e = e.InnerException)
        {
            sb.AppendLine($"{e.GetType().Name}: {e.Message}");
        }

        sb.AppendLine("---");
        sb.Append(ex.StackTrace);
        return sb.ToString().TrimEnd();
    }
}
