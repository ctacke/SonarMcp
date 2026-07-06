namespace SonarMcp.Server;

/// <summary>
/// Connection/auth settings for the SonarQube Web API, read from environment variables by the launching
/// MCP client (e.g. a <c>claude mcp add --env</c> flag or a <c>.mcp.json</c> <c>env</c> block).
/// </summary>
public sealed record SonarQubeConfig
{
    /// <summary>Personal access token used as the HTTP Basic auth username (empty password).</summary>
    public required string Token { get; init; }

    /// <summary>Base URL of the SonarQube instance, e.g. https://sonarcloud.io or a self-hosted server.</summary>
    public required string Url { get; init; }

    /// <summary>Organization key, if the instance uses organizations (e.g. SonarQube Cloud).</summary>
    public string? Organization { get; init; }

    /// <summary>
    /// Default project key used when a tool call doesn't specify one. Deliberately has no baked-in
    /// default — this server is meant to be reusable across any project/organization, so a caller
    /// either sets this per-deployment via env, or passes <c>projectKey</c> explicitly on each call.
    /// </summary>
    public string? DefaultProjectKey { get; init; }

    public static SonarQubeConfig FromEnvironment()
    {
        string Req(string name) =>
            Environment.GetEnvironmentVariable(name)
            ?? throw new InvalidOperationException($"Required environment variable '{name}' is not set.");

        return new SonarQubeConfig
        {
            Token = Req("SONAR_TOKEN"),
            Url = Req("SONAR_URL"),
            Organization = Environment.GetEnvironmentVariable("SONAR_ORGANIZATION"),
            DefaultProjectKey = Environment.GetEnvironmentVariable("SONAR_PROJECT_KEY"),
        };
    }
}
