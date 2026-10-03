# Frigate as an input source

[Frigate](https://frigate.video) is an open-source NVR with its own local object detection. AI Tool can
subscribe to Frigate's MQTT events and pull the matching snapshot from Frigate's HTTP API, feeding it into
the same image queue as the Blue Iris JPEG folder - so a Frigate camera gets AI Tool's own AI backends,
refinement chain, masking and actions, without needing Blue Iris at all.

This targets Frigate 0.13 through 0.18 (the current MQTT/HTTP API shape as of late 2025/early 2026 - see
https://docs.frigate.video/integrations/mqtt and https://docs.frigate.video/integrations/api). Frigate's
`/api/events/<id>/snapshot.jpg` endpoint and `/api/version` have not changed shape across those releases;
if a future Frigate release changes them, only `FrigateSource.cs` needs updating.

## Requirements

* A running Frigate instance with an MQTT broker configured (`mqtt.enabled: true` in Frigate's
  `config.yml`) - the **same broker** AI Tool is already configured to use under the MQTT settings
  (`mqtt_serverandport`, username/password, etc.) in `Cameras > [camera] > Actions > MQTT > Settings`.
  Frigate and AI Tool just need to be pointed at the same broker; they don't talk to each other directly.
* Network access from the machine running AI Tool to Frigate's HTTP API (default `http://<frigate-host>:5000`,
  unauthenticated by default).

## Setup

Open **Cameras > [any camera] > Actions > MQTT**, then click the **Frigate** link next to **Settings**
(Frigate uses the same broker, so its settings live next to the MQTT ones even though Frigate is not
camera-specific):

* **Use Frigate as a source** - enables the feature.
* **Frigate URL** - Frigate's base URL, e.g. `http://frigate:5000`.
* **MQTT Topic Prefix** - must match Frigate's `mqtt.topic_prefix` config (default `frigate`). AI Tool
  subscribes to `<prefix>/events`.
* **Cameras** - comma separated list of Frigate camera names (as configured in Frigate's `config.yml`,
  e.g. `front_door, driveway`) to accept. Leave blank to accept events from every Frigate camera.
* **Labels** - comma separated list of object labels to accept (e.g. `person, car`). Leave blank to accept
  every label Frigate reports.
* **Snapshot folder** - where downloaded snapshots are saved before being queued. Leave blank to use
  `%TEMP%\_AITOOL\frigate` (cleared/recreated on every AI Tool startup, like the rest of `%TEMP%\_AITOOL`;
  files older than a day are also purged on each restart of the Frigate source).
* **API Key** - only needed if you've put an authenticating reverse proxy in front of Frigate; sent as
  `Authorization: Bearer <key>`. Frigate's own API has no built-in key requirement in the versions this
  targets.
* **Test** - fetches `<Frigate URL>/api/version` and shows the result, to confirm AI Tool can reach Frigate.

Settings take effect immediately on Save (and at AI Tool startup) - no restart needed.

## Camera mapping

Frigate identifies cameras by the short name configured in its `config.yml` (e.g. `front_door`), not by a
display name. AI Tool maps that Frigate camera name to one of its own configured Cameras by trying, in
order, a case-insensitive exact match against:

1. **BI Cam Name**
2. **Name**
3. **Prefix**

The first match wins. If none of your AI Tool cameras match a given Frigate camera name, AI Tool logs a
single Debug line per Frigate camera name (not once per event) and skips it - rename one of the fields
above on an existing camera (or add a new one) to pick it up.

### Why the camera needs a Prefix too

The saved snapshot is named `<token>.<frigate event id>.<timestamp>.jpg`, where `<token>` is the matched
camera's own **Prefix** if it has one set, otherwise its **Name**. This matters because AI Tool re-resolves
the camera from the filename every time an image is dequeued for processing (the same `GetCamera()` logic
Blue Iris folder watching uses), and that logic only looks at a camera's **Prefix** (or its configured input
folder) - never at BI Cam Name or Name. If the camera matched above has no Prefix configured, the image will
still be enqueued and processed, but it will fall through to AI Tool's "default" camera at that later step,
the same way a Blue Iris folder-based camera with no Prefix would. **Set a Prefix on the camera** if you want
Frigate events to reliably land on that specific camera rather than the default one.

## What gets fetched, and when

Frigate publishes a message to `<prefix>/events` for every tracked-object state change, with a `type` of
`new`, `update`, or `end`, and `before`/`after` snapshots of the object's fields (id, camera, label, score,
`has_snapshot`, ...). AI Tool only reacts to:

* The **`new`** message, if it already has `has_snapshot: true` (the normal case), or
* The **first `update`** message where `has_snapshot` flips from `false` to `true` (covers the rare case
  where Frigate publishes `new` slightly before the snapshot is ready).

Later `update` messages for an object that already had a snapshot, and `end` messages, are ignored - a
loitering object produces many `update` messages as Frigate refines its tracking, and re-downloading/
re-queuing the snapshot for each one would spam the queue for no benefit. Each Frigate event id is also only
ever enqueued once, even if a duplicate message arrives (e.g. after a reconnect).

Once accepted, AI Tool downloads `<Frigate URL>/api/events/<event id>/snapshot.jpg`, saves it, and calls the
normal image queue entry point - from that point on it's treated exactly like a Blue Iris alert image:
refinement servers, masking, triggering-object filters and actions all apply normally.

## Code

* `src/UI/MQTTClient.cs` - subscription support (`SubscribeAsync`/`UnsubscribeAsync`) added on top of the
  existing publish-only client; it re-subscribes automatically after a reconnect.
* `src/UI/FrigateSource.cs` - event parsing/filtering/camera-mapping/snapshot download, and the
  start/stop/restart lifecycle.
* `src/UI/Frm_FrigateSettings.cs` (+ `.Designer.cs`) - the settings dialog described above.
* Settings live in `AppSettings.ClsSettings` (`src/UI/Settings.cs`) as `Frigate*`.
