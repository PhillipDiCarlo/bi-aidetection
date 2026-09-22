# AI Detection for Blue Iris (AITool)

Filters Blue Iris motion alerts through AI object detection and only triggers, records, or notifies when something relevant (a person, vehicle, animal, ...) is actually in frame.

This is the VorlonCD fork of gentlepumpkin's original AITool.

### Download

Installers are attached to each release:
https://github.com/VorlonCD/bi-aidetection/releases

**Requires the .NET 10 Desktop Runtime**: https://dotnet.microsoft.com/en-us/download/dotnet/10.0 (the installer checks for it and points you to the download if it is missing).

### Install guide and discussion
https://ipcamtalk.com/threads/tool-tutorial-free-ai-person-detection-for-blue-iris.37330/

* [MQTT Configuration Guide](mqtt.md)
* [Changelog](CHANGELOG.md)
* [Roadmap](ROADMAP.md)

### Key features
- Watches the Blue Iris alert image folder and runs each image through one or more AI servers
- Per-camera relevant-object lists, confidence thresholds, size limits, static masks, and dynamic masking of objects that never move
- Load balancing, failover, and "refinement" chains across multiple AI servers (e.g. detect a vehicle, then send the crop to a license plate model)
- Actions: trigger/cancel Blue Iris via URL or MQTT, Telegram, Pushover, MQTT, sounds/text-to-speech, run a program, copy annotated images
- Telegram remote control (pause, resume, screenshots, ...)
- History database with annotated images, per-camera and per-server statistics

### Supported AI backends
| Backend | Notes |
|---|---|
| [CodeProject.AI Server](https://github.com/codeproject/CodeProject.AI-Server) | Object detection, faces, scenes, license plates, custom and IPcam models. Mesh/queueing supported. |
| DeepStack | Same API as CodeProject.AI. DeepStack itself is no longer maintained, but anything that speaks its `/v1/vision/*` API works (e.g. [Blue Onyx](https://github.com/xnorpx/blue-onyx)). |
| [DOODS](https://github.com/snowzach/doods2) | |
| Amazon Rekognition | Cloud, objects and faces |
| SightHound | Cloud, vehicles and people |

### Building from source
Requires the [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0). Visual Studio 2022 17.12+ or later is optional.

```
git clone https://github.com/VorlonCD/bi-aidetection.git
cd bi-aidetection
dotnet build src/bi-aidetection.sln
dotnet test src/bi-aidetection.sln
```

The app is at `src/UI/bin/Debug/net10.0-windows10.0.19041.0/AITool.exe`.

To build the installer, install [Inno Setup 6](https://jrsoftware.org/isdl.php) and run `src\AITool.Setup\BUILD.bat [Debug|Release]`. Tagging a commit `v*` and pushing the tag makes GitHub Actions build the installer and attach it to a release.

### Contributing
Pull requests are welcome. See [ROADMAP.md](ROADMAP.md) for planned work and the order it is being tackled in.
