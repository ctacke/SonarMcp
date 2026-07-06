using SonarMcp.Server;
using Xunit;

namespace SonarMcp.Tests;

public class SonarQubeConfigTests
{
    private static readonly string[] EnvVars =
        ["SONAR_TOKEN", "SONAR_URL", "SONAR_ORGANIZATION", "SONAR_PROJECT_KEY"];

    private static T WithEnv<T>(IReadOnlyDictionary<string, string?> vars, Func<T> action)
    {
        var previous = EnvVars.ToDictionary(v => v, Environment.GetEnvironmentVariable);
        try
        {
            foreach (var name in EnvVars)
            {
                Environment.SetEnvironmentVariable(name, vars.GetValueOrDefault(name));
            }

            return action();
        }
        finally
        {
            foreach (var (name, value) in previous)
            {
                Environment.SetEnvironmentVariable(name, value);
            }
        }
    }

    [Fact]
    public void FromEnvironment_AllVarsSet_BindsCorrectly()
    {
        var config = WithEnv(new Dictionary<string, string?>
        {
            ["SONAR_TOKEN"] = "my-token",
            ["SONAR_URL"] = "https://sonar.example.com",
            ["SONAR_ORGANIZATION"] = "my-org",
            ["SONAR_PROJECT_KEY"] = "my-project",
        }, SonarQubeConfig.FromEnvironment);

        Assert.Equal("my-token", config.Token);
        Assert.Equal("https://sonar.example.com", config.Url);
        Assert.Equal("my-org", config.Organization);
        Assert.Equal("my-project", config.DefaultProjectKey);
    }

    [Fact]
    public void FromEnvironment_OptionalVarsUnset_AreNull()
    {
        var config = WithEnv(new Dictionary<string, string?>
        {
            ["SONAR_TOKEN"] = "my-token",
            ["SONAR_URL"] = "https://sonar.example.com",
        }, SonarQubeConfig.FromEnvironment);

        Assert.Null(config.Organization);
        Assert.Null(config.DefaultProjectKey);
    }

    [Fact]
    public void FromEnvironment_MissingToken_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => WithEnv(new Dictionary<string, string?>
        {
            ["SONAR_URL"] = "https://sonar.example.com",
        }, SonarQubeConfig.FromEnvironment));

        Assert.Contains("SONAR_TOKEN", ex.Message);
    }

    [Fact]
    public void FromEnvironment_MissingUrl_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => WithEnv(new Dictionary<string, string?>
        {
            ["SONAR_TOKEN"] = "my-token",
        }, SonarQubeConfig.FromEnvironment));

        Assert.Contains("SONAR_URL", ex.Message);
    }

    [Fact]
    public void FromEnvironment_HasNoBakedInDefaults()
    {
        // SonarMcp must work against any SonarQube server/project/token purely via
        // configuration — verifies no hardcoded fallback URL or organization sneaks back in.
        var config = WithEnv(new Dictionary<string, string?>
        {
            ["SONAR_TOKEN"] = "t",
            ["SONAR_URL"] = "https://sonar.example.com",
        }, SonarQubeConfig.FromEnvironment);

        Assert.Equal("https://sonar.example.com", config.Url);
        Assert.Null(config.Organization);
    }
}
