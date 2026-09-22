# AITool Modernization Roadmap

This repo was last touched in mid-2024. This document tracks the work to bring it back to a healthy, buildable, secure state and then extend it for the 2026 AI/camera ecosystem. Phases are ordered by dependency: each phase unblocks the next. Within a phase, items are in the order they should be tackled.

Check items off as they land. Keep this file current — it is the source of truth for what is done and what is next.

---

## Phase 0 — Get it green (build, deps, hygiene)

Goal: a fresh `git clone` builds with one command, has no known-vulnerable packages, and is on a supported runtime. No behavior changes.

- [x] **0.1 Fix the ObjectListView dependency.** `src/UI/UI.csproj` references `..\..\..\ObjectListView.NET6\...` outside the repo. Replace with a NuGet package (e.g. `ObjectListView.Repack.NET6Plus`) or vendor the source into `src/`.
- [x] **0.2 Fix the pre/post-build events.** `CleanOldInstalls.bat` and `BUILD.bat` (Inno Setup) run on every build via `$(SolutionDir)`, which is empty when building the project directly. Remove them from the csproj; installer creation becomes a separate script / CI job (see 0.7).
- [x] **0.3 Delete dead projects and code.**
  - `src/AITool.Service/` — .NET Framework 4.7.2 stub with empty `OnStart`/`OnStop` and a missing `..\packages` folder.
  - `src/ThreadSafeTesting/` — ad-hoc test harness, not a real test project.
  - Files excluded from compile but still in the tree: `NamedPipeWrapper\**`, `RichTextBoxEx.cs`, `ThreadSafe_OLD.cs`, `BlueIrisControl.cs`.
  - Rename `bi-aidetection.NET6.sln` → `bi-aidetection.sln`.
- [x] **0.4 Remove binaries from git.** `src/AITool.Setup/INNO/` (~15 MB Inno Setup compiler) and `src/UI/Installer/AIToolSetup.*.exe` (19 MB). Installers ship via GitHub Releases; Inno Setup is installed on the build machine / CI runner.
- [x] **0.5 Update NuGet packages.** _Note: ImageSharp is pinned to 3.1.x because 4.x requires a paid license key for Release builds._ Vulnerable ones first, then everything else. Drop shim packages that are in-box on modern .NET (`System.Buffers`, `System.Memory`, `System.ValueTuple`, `System.Numerics.Vectors`, `System.Threading.Tasks.Extensions`, `NETStandard.Library`, `Microsoft.CSharp`).
  - [x] `SixLabors.ImageSharp` 3.1.4 → 3.1.12 (2 high + 2 moderate CVEs)
  - [x] `SQLitePCLRaw.lib.e_sqlite3` 2.1.8 → 3.x (high CVE)
  - [x] `Telegram.Bot` 19 → 22 (breaking API)
  - [x] `MQTTnet` 4 → 5 (breaking API)
  - [x] `AWSSDK.Rekognition` 3 → 4 (breaking API)
  - [x] `NLog` 5 → 6, `Octokit` 11 → 14, `Markdig`, `NAudio.*`, `WindowsAPICodePack`, `WinForms.DataVisualization`, `Angle`, `SolarCalculator`, `System.Management`, `System.Speech`
- [x] **0.6 Retarget to .NET 10 LTS.** .NET 8 leaves support 2026-11-10. Update `TargetFramework`, the installer's runtime check, and the README download link.
- [x] **0.7 Add GitHub Actions.**
  - [x] `build.yml` — restore + build on every push/PR.
  - [x] `release.yml` — on tag, build, run Inno Setup, attach installer to a GitHub Release. The in-app update checker already reads Releases via Octokit.
  - [x] `dependabot.yml` — weekly NuGet + Actions updates.
- [x] **0.8 Add a test project** (`src/AITool.Tests/`, xUnit) with a handful of tests around pure logic to seed it: `RectangleMatches`, `RemovePredictionDuplicates`, `ReplaceParams`, mask hit-testing. Wire into `build.yml`.
- [x] **0.9 Docs.** Add `CHANGELOG.md` (seed from recent commit messages), update `README.md` (build instructions, supported backends, runtime requirement), keep `mqtt.md`.

---

## Phase 1 — Make it extensible (refactor + security)

Goal: adding a new AI backend or notification channel means adding one class, not editing a 900-line method. Secrets are never on disk in plaintext. No user-visible behavior changes.

- [x] **1.1 Extract `IAIProvider`.** `AITOOL.GetDetectionsFromAIServer` dispatches on `AiUrl.Type.ToString().Has("codeproject")` etc. Create `src/UI/AIProviders/` with one class per backend implementing `Task<ClsAIServerResponse> DetectAsync(ClsImageQueueItem img, ClsURLItem url, Camera cam, CancellationToken ct)`:
  - [x] `DeepStackCompatibleProvider` (CodeProject.AI, DeepStack, Blue Onyx — all speak the same `/v1/vision/*` API)
  - [x] `DoodsProvider`
  - [x] `SightHoundProvider`
  - [x] `AwsRekognitionProvider`
  - [x] Registry keyed on `URLTypeEnum`; `GetDetectionsFromAIServer` becomes a lookup + call.
- [x] **1.2 Extract `INotificationChannel`.** _Landed as `IActionChannel` in `src/UI/Actions/`._ Same treatment for `ClsTriggerActionQueue`: Telegram, Pushover, MQTT, trigger/cancel URL, sound, run-program, image-copy each become a class with `Task SendAsync(...)` / `Task CancelAsync(...)`.
- [x] **1.3 Encrypt all secrets at rest.** Only the Blue Iris password uses DPAPI today. Apply the existing `.Encrypt()`/`.Decrypt()` to `telegram_token`, `mqtt_password`, `pushover_APIKey`, `pushover_UserKey`, `AmazonSecretKey`, `SightHoundAPIKey`, `deepstack_adminkey`, `deepstack_apikey`. Migrate plaintext values on first load.
- [ ] **1.4 Blue Iris JSON API client.** Replace `user=&pw=` query-string credentials in trigger URLs with session login via `/json` (`login` → MD5(session:user:pw) → `trigger`, `alertlist`, `camlist`). Keep the legacy URL action working for people with custom URLs.
- [ ] **1.5 Async hygiene.** Replace `.Result` / `.Wait()` on the UI thread (93 sites) and the `async void` event handlers in `Shell.cs` (33) with proper `async Task` + `await`. Remove leftover debug code (`int testing = 0;`).
- [ ] **1.6 Retire the DeepStack process manager.** DeepStack's last release was Jan 2022. Hide the DeepStack tab behind a "legacy" toggle; the `DeepStack` URL type stays (Blue Onyx and others use the same API).
- [ ] **1.7 Split the god files.** `Shell.cs` (5.6k lines), `Global.cs` (5.1k), `AITOOL.cs` (4.7k) into partial classes by concern (queue, watchers, masking, params, etc.). Mechanical moves only.

---

## Phase 2 — Features

Goal: keep AITool relevant now that Blue Iris has native AI. Lean into what BI doesn't do: multiple backends, refinement chains, rich routing, and local zero-install detection. Ordered by value ÷ effort.

- [ ] **2.1 In-process detection via ONNX Runtime + YOLO.** New `OnnxYoloProvider`: load a YOLOv8/v11 `.onnx` model, run with `Microsoft.ML.OnnxRuntime.DirectML` for GPU on Windows (CPU fallback). No external server to install. Ship a default model download on first use. Add to the AI Servers list as a `Local_ONNX` type.
- [ ] **2.2 Vision-LLM refinement provider.** `OpenAICompatibleVisionProvider` targeting any OpenAI-style `/v1/chat/completions` endpoint (Ollama, LM Studio, OpenAI, Gemini) plus a native Anthropic option. Used as a *refinement server*: send the crop + a per-camera prompt ("Is this person carrying a package? Answer JSON."), get back a label/detail that flows into `[Summary]` and the Blue Iris memo.
- [ ] **2.3 Blue Onyx as a named backend.** Verify it works via the DeepStack-compatible provider, add `Blue_Onyx` to `URLTypeEnum` with default port/URL/help link, document it.
- [ ] **2.4 Generic webhook action.** JSON POST with configurable body using the existing template vars (`[Summary]`, `[DetectionsJson]`, `[ImagePath]`…), optional image as base64 or multipart. Covers Discord, Slack, ntfy, n8n, Home Assistant webhooks.
- [ ] **2.5 Home Assistant MQTT discovery.** Publish `homeassistant/binary_sensor/aitool_<camera>_<object>/config` so each camera/object pair appears in HA automatically. Small addition to `MQTTClient`.
- [ ] **2.6 Frigate as an input source.** Subscribe to Frigate's `frigate/events` MQTT topic and pull the snapshot via its HTTP API, feeding the same image queue as the Blue Iris JPEG folder.
- [ ] **2.7 Embedded web dashboard.** Kestrel minimal API + a small static page: status, AI server health, recent history with images, pause/resume. This is also what enables a real headless / Windows Service mode.
- [ ] **2.8 Object tracking across frames.** Blue Iris sends several JPEGs per event; match predictions across them (IoU) to support loitering ("person present > N seconds") and line-crossing rules on top of the existing masks.

---

## Not planned

- Azure AI Vision / Google Cloud Vision label APIs — superseded by 2.2 (vision LLMs give richer results for the same integration effort).
- Face training UI — faces are stored under `_Settings\FaceStorage` but CodeProject.AI/DeepStack face APIs are effectively unmaintained; revisit if a maintained backend appears.
