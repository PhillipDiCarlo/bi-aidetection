# Changelog

Changes before this file existed are described in the commit messages:
https://github.com/VorlonCD/bi-aidetection/commits/master

## Unreleased

### Requirements
- Now requires the **.NET 10 Desktop Runtime** (was .NET 8, which leaves support in November 2026).
  https://dotnet.microsoft.com/en-us/download/dotnet/10.0

### Changed
- All NuGet packages updated; known vulnerabilities in ImageSharp and SQLitePCLRaw resolved.
- Telegram.Bot 22, MQTTnet 5, AWS SDK v4, NLog 6. No user-visible behavior changes are intended.
- Log archives are now written as `AITool.[date]_NN.log` next to the active log instead of `.log.zip` (NLog 6 dropped built-in archive compression).
- Installers are published on GitHub Releases instead of being checked into the repo.

### Development
- The project now builds from a clean clone with `dotnet build src/bi-aidetection.sln`.
- Removed the unused `AITool.Service` and `ThreadSafeTesting` projects and other dead code.
- Added a GitHub Actions build workflow, a tag-triggered release workflow, Dependabot, and an xUnit test project.
- See [ROADMAP.md](ROADMAP.md) for what is planned next.
