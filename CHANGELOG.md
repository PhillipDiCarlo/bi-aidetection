# Changelog

Changes before this file existed are described in the commit messages:
https://github.com/VorlonCD/bi-aidetection/commits/master

## Unreleased

### Added
- **Vision-LLM refinement**: new AI server types `OpenAI_Vision` (any OpenAI-compatible endpoint: Ollama, LM Studio, OpenAI, OpenRouter, ...) and `Anthropic_Vision`. Configure as a refinement server with a prompt; the model's description and any objects it localizes flow into the summary/memo like any other detection. API keys are stored encrypted.
- **Webhook action** per camera: POST/PUT any URL with a templated body and headers, optional multipart image, optional cancel call. See `webhook.md`.
- **Home Assistant MQTT discovery**: one switch in MQTT settings publishes per-camera motion/person/vehicle/animal `binary_sensor`s (and optionally an MQTT camera) that auto-appear in Home Assistant. See `mqtt.md`.

### Requirements
- Now requires the **.NET 10 Desktop Runtime** (was .NET 8, which leaves support in November 2026).
  https://dotnet.microsoft.com/en-us/download/dotnet/10.0

### Fixed
- Creating a camera when the default relevant-object list was empty could hang the app in an infinite loop.
- Overlap percentage between two detections (used for duplicate merging and refinement matching) was computed with the wrong denominator whenever the two rectangles had different widths. It is now a proper Dice coefficient, so "MergePredictionsMinMatchPercent" behaves consistently regardless of object size.
- Settings were not reliably saved on exit (the final save was not awaited).

### Security
- Telegram token, MQTT password, Pushover keys, AWS secret key, SightHound key and DeepStack keys are now stored DPAPI-encrypted in `AITOOL.Settings.JSON` (previously plaintext). Existing files are migrated automatically on the next save.

### Changed
- The DeepStack tab is hidden unless DeepStack for Windows is installed (DeepStack is unmaintained). Set `ShowDeepStackTab` to `true` in `AITOOL.Settings.JSON` to force it.
- All NuGet packages updated; known vulnerabilities in ImageSharp and SQLitePCLRaw resolved.
- Telegram.Bot 22, MQTTnet 5, AWS SDK v4, NLog 6. No user-visible behavior changes are intended.
- Log archives are now written as `AITool.[date]_NN.log` next to the active log instead of `.log.zip` (NLog 6 dropped built-in archive compression).
- Installers are published on GitHub Releases instead of being checked into the repo.

### Development
- The project now builds from a clean clone with `dotnet build src/bi-aidetection.sln`.
- Removed the unused `AITool.Service` and `ThreadSafeTesting` projects and other dead code.
- Added a GitHub Actions build workflow, a tag-triggered release workflow, Dependabot, and an xUnit test project.
- See [ROADMAP.md](ROADMAP.md) for what is planned next.
