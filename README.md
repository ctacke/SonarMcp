# SonarMcp

<p align="center">
  <img src="https://raw.githubusercontent.com/ctacke/SonarMcp/main/assets/icon.png" width="128" height="128" alt="SonarMcp icon" />
</p>

A production-quality .NET 10 MCP (Model Context Protocol) server that reads issues and quality gate
status from the SonarQube Web API. Connect Claude Code or any MCP-compatible AI assistant to any
SonarQube instance — SonarQube Cloud, SonarQube Server, or a self-hosted deployment — authenticated with
a Personal Access Token (PAT).

## What is SonarMcp?

SonarMcp exposes SonarQube's issues and quality-gate APIs as MCP tools, allowing an AI assistant to:
- List open issues for a branch (e.g. a PR branch), mirroring the SonarQube dashboard's issue list
- List issues assigned to the authenticated user
- Check whether a branch's quality gate passed, and see which conditions failed

Not tied to any specific project — it's meant to be pointed at any project on any SonarQube server via
configuration, so when a PR's SonarQube check fails or adds new issues, Claude Code can read the same
information you'd otherwise have to open the SonarQube dashboard for.

## Prerequisites

- A SonarQube instance (SonarQube Cloud, SonarQube Server, or self-hosted) and a Personal Access Token
  for it
- [.NET 10 runtime/SDK](https://dotnet.microsoft.com/download/dotnet/10.0) — **only** if installing via
  `dotnet tool install` or building from source. The self-contained release binary needs nothing else.

## Installation

### Recommended: install as a global tool

```bash
dotnet tool install -g SonarMcp.Server
```

Published to [nuget.org](https://www.nuget.org/packages/SonarMcp.Server), so no source configuration or
authentication is needed — it's the .NET SDK's default package source. This installs a `sonar-mcp`
command on your `PATH`. Upgrade later with `dotnet tool update -g SonarMcp.Server`.

### Alternative: download a release binary

No .NET install required at all. Grab the archive for your OS from the [Releases page](../../releases)
and extract it.

## Configure Claude Code

Register SonarMcp with the `claude mcp add` CLI — the supported way to add a server at user scope
(available across all projects). Pass the SonarQube connection details as `--env` flags:

**If installed as a dotnet tool:**

```
claude mcp add --scope user sonar-mcp --env SONAR_URL=https://sonarcloud.io --env SONAR_TOKEN=<your PAT> --env SONAR_PROJECT_KEY=<your default project key> -- sonar-mcp
```

**If using the self-contained binary:**

```
claude mcp add --scope user sonar-mcp --env SONAR_URL=https://sonarcloud.io --env SONAR_TOKEN=<your PAT> --env SONAR_PROJECT_KEY=<your default project key> -- C:/tools/SonarMcp.Server.exe
```

**If running from source:**

```
claude mcp add --scope user sonar-mcp --env SONAR_URL=https://sonarcloud.io --env SONAR_TOKEN=<your PAT> --env SONAR_PROJECT_KEY=<your default project key> -- dotnet run --project src/SonarMcp.Server/SonarMcp.Server.csproj -c Release
```

**After running `claude mcp add`, restart your `claude` session** (exit and relaunch) — a server added
while a session is already running won't appear in `/mcp` until you do.

`SONAR_PROJECT_KEY` is just a convenience default — pass a different `projectKey` on a tool call to
point at another project on the same SonarQube instance.

## Configuration (environment variables)

| Variable | Required | Notes |
|---|---|---|
| `SONAR_TOKEN` | Yes | Your SonarQube PAT. Sent as HTTP Basic auth username with an empty password. |
| `SONAR_URL` | Yes | Base URL of your SonarQube instance, e.g. `https://sonarcloud.io` or your self-hosted server's URL. |
| `SONAR_ORGANIZATION` | No | Organization key, if your instance uses organizations (e.g. SonarQube Cloud). |
| `SONAR_PROJECT_KEY` | No | Convenience default project key. If unset, `projectKey` must be passed on every tool call. |

There is no baked-in default for any of these — SonarMcp is meant to work against any SonarQube server,
project, and token, entirely via this configuration.

## Available MCP Tools

| Tool | Description |
|---|---|
| `list_issues(branch, projectKey?, statuses="OPEN,CONFIRMED", sinceLeakPeriod=true, severities?, types?)` | Lists issues for a branch (e.g. a PR branch), same filters as the dashboard's default PR view. |
| `list_my_issues(branch?, projectKey?, statuses="OPEN,CONFIRMED", severities?, types?)` | Lists issues assigned to the user identified by `SONAR_TOKEN`. `branch` is optional; omit it to search the project's main branch. |
| `get_quality_gate_status(branch, projectKey?)` | Reports the quality gate's overall status (OK/ERROR) and any failing conditions, i.e. whether the PR's SonarQube check passed. |

Example: "what SonarQube issues are on my current branch?", "what SonarQube issues are assigned to me?",
or "did the SonarQube quality gate pass on my feature branch?"

## Security Notes

- **Never commit a `.mcp.json` containing a real token** — prefer `claude mcp add --scope user` (as
  shown above) so your PAT passed via `--env` never ends up in git history. `.mcp.json` is gitignored by
  default in this repo as a safety net.
- Use a token scoped to read-only access where your SonarQube instance supports it.

## Running Tests

```
dotnet test tests/SonarMcp.Tests/SonarMcp.Tests.csproj
```

## Publishing

To build and release SonarMcp as a downloadable binary or NuGet tool, see
[docs/publishing.md](docs/publishing.md). Pushing a `vX.X` or `vX.X.X` tag (e.g. `v1.0`, `v0.9.1`)
triggers an automated GitHub Actions release.
