# Publishing SonarMcp

This guide covers how to build, package, and distribute SonarMcp via
[NuGet.org](https://www.nuget.org) so others can install it as a .NET global
tool with zero authentication required to read/install (only publishing
needs an API key).

---

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- A [nuget.org](https://www.nuget.org) account
- A NuGet API key (nuget.org → your account → API Keys → create one scoped
  to push new packages and new versions of `SonarMcp.Server`)
- `gh` CLI (optional — useful for creating GitHub Releases)

---

## Step 1: .NET global tool packaging properties

`src/SonarMcp.Server/SonarMcp.Server.csproj` already has the properties
needed to pack as a tool:

```xml
<PackAsTool>true</PackAsTool>
<ToolCommandName>sonar-mcp</ToolCommandName>
<PackageId>SonarMcp.Server</PackageId>
<Version>1.0.0</Version>
<Authors>Chris Tacke</Authors>
<Description>MCP server providing SonarQube issue and quality gate access for Claude Code.</Description>
<PackageLicenseExpression>MIT</PackageLicenseExpression>
<RepositoryUrl>https://github.com/ctacke/SonarMcp</RepositoryUrl>
<PackageReadmeFile>README.md</PackageReadmeFile>
```

Before publishing for the first time, check that `SonarMcp.Server` isn't
already taken on nuget.org — package IDs there are first-come, first-served
and can't be transferred after the fact.

No `nuget.config` is needed to *publish* to nuget.org — it's the default
NuGet source already, so pushing only needs the `--api-key` on the command
line (or trusted publishing in CI, see Step 5).

---

## Step 2: Build and test

Always run tests before publishing:

```bash
dotnet build SonarMcp.slnx -c Release
dotnet test tests/SonarMcp.Tests/SonarMcp.Tests.csproj -c Release --no-build
```

---

## Step 3: Pack the NuGet tool

```bash
dotnet pack src/SonarMcp.Server/SonarMcp.Server.csproj -c Release -o ./artifacts
```

This produces `artifacts/SonarMcp.Server.<version>.nupkg`.

---

## Step 4: Publish to NuGet.org

```bash
# Windows (PowerShell)
$env:NUGET_API_KEY = "YOUR_NUGET_ORG_API_KEY"

# macOS / Linux
export NUGET_API_KEY=YOUR_NUGET_ORG_API_KEY

dotnet nuget push artifacts/SonarMcp.Server.*.nupkg --source https://api.nuget.org/v3/index.json --api-key $env:NUGET_API_KEY
```

The package appears at `https://www.nuget.org/packages/SonarMcp.Server` a
few minutes after indexing completes.

---

## Step 5: Automate with GitHub Actions

All of the above (plus self-contained binaries for users without .NET
installed) is already automated in
[`.github/workflows/release.yml`](../.github/workflows/release.yml), which
runs on every `vX.X` or `vX.X.X` tag push (e.g. `v1.0`, `v0.9.1`, `v2.3`). It
validates the tag format, publishes self-contained binaries for
`win-x64`/`linux-x64`/`osx-x64`/`osx-arm64` and attaches them to a GitHub
Release, then packs and pushes the NuGet tool package to NuGet.org.

That workflow authenticates to NuGet.org using
[trusted publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing)
(OIDC) rather than a stored API key — no repository secret needed. This
requires a one-time setup: a Trusted Publisher policy on nuget.org for the
`SonarMcp.Server` package pointing at `ctacke/SonarMcp`, workflow
`release.yml`, environment `production`; and a matching GitHub Environment
named `production` created under **Settings → Environments**.

---

## Versioning

Tags use `vX.X` or `vX.X.X` (`v1.0`, `v0.9.1`, `v2.3`, `v2.3.10`) — the patch
segment is optional. To release a new version:

```bash
git tag v1.1
git push origin v1.1
```

The GitHub Actions workflow triggers automatically on the tag push and
derives the package/binary version from the tag itself — there's no need to
manually bump `<Version>` in the csproj first.

---

## Installing as a user

### Via dotnet tool (recommended)

```bash
dotnet tool install -g SonarMcp.Server
```

No source configuration or authentication needed — NuGet.org is the default
package source for the .NET SDK.

### Via self-contained binary

Download the appropriate binary from the
[Releases page](https://github.com/ctacke/SonarMcp/releases), extract it,
and place it somewhere on your `PATH`.
