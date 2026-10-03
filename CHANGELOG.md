# Changelog

Changes before this file existed are described in the commit messages:
https://github.com/VorlonCD/bi-aidetection/commits/master

## Unreleased

### Added
- **Works out of the box with no AI server**: on a fresh install a "Local YOLO (built-in)" server is created automatically and the default model (YOLOv8n, ~12 MB) is downloaded on first use. Detection runs in-process with ONNX Runtime (DirectML GPU, CPU fallback). To use your own model, point a `Local_ONNX` server's "Model Path" at an exported `.onnx` (`yolo export model=yolo11n.pt format=onnx`); class names come from an optional `<model>.names` sidecar. See `docs/local-detection.md`.
- **Blue Onyx** is a selectable AI server type (DeepStack-compatible API, default port 32168).
- **Vision-LLM refinement**: new AI server types `OpenAI_Vision` (any OpenAI-compatible endpoint: Ollama, LM Studio, OpenAI, OpenRouter, ...) and `Anthropic_Vision`. Configure as a refinement server with a prompt; the model's description and any objects it localizes flow into the summary/memo like any other detection. API keys are stored encrypted.
- **Webhook action** per camera: POST/PUT any URL with a templated body and headers, optional multipart image, optional cancel call. See `webhook.md`.

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
- AITool is now built as a 64-bit-only application (it already shipped a 64-bit-only SQLite; DirectML requires an explicit platform target).
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
