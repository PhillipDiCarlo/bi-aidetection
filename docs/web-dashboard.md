# Embedded Web Dashboard

AI Tool can serve a small built-in web page so you can check on it from a phone or another PC on your
network - status, AI server health, recent history with images, and camera pause/resume - without
installing anything else. It also exposes a JSON API under `/api/...` so it can be scripted.

This does **not** use ASP.NET Core/Kestrel, so AI Tool keeps running on just the .NET Desktop Runtime.
It's served by [EmbedIO](https://github.com/unosquare/embedio) (MIT licensed), an in-process HTTP server.

## Enabling it

Tray icon (right-click) > **Web Dashboard...**, or from the main window's tray menu. In the dialog:

* **Enable web dashboard** - turns the server on. A random access token is generated automatically the
  first time you enable it.
* **Port** - default `8099` (doesn't collide with Blue Iris, CodeProject.AI, Frigate, or DeepStack).
* **Allow LAN access** - off by default, which binds the server to `127.0.0.1` only (this PC alone).
  Turning it on binds to all network interfaces so other devices on your network can reach it.
* **Access token** - required on every request (see below). **Regenerate** makes a new one (invalidates
  the old one immediately); **Copy URL** copies a link with the token already included; **Open in
  browser** launches the dashboard in your default browser.

Changes take effect immediately (no restart needed) - the server is stopped and restarted with the new
settings as soon as you click Save.

> **Warning:** LAN mode exposes the dashboard to anyone on your local network who knows (or guesses) the
> port. Only enable it on a network you trust, and keep the token secret - treat it like a password.

## The token

Every request must include the token, either as:

* An `Authorization: Bearer <token>` header, or
* A `?token=<token>` query string parameter (used for things like `<img>` tags, which can't set custom
  headers).

The dashboard page itself reads `?token=` from the URL once, stores it in the browser's `sessionStorage`,
and strips it back out of the address bar - so you can bookmark/share a link with the token in it, but it
won't linger in browser history after that first load. Requests without a valid token get `401
Unauthorized`; the token comparison is constant-time to avoid timing side-channels.

The token is stored encrypted at rest in the settings file (same DPAPI-based protection used for the
Telegram/Pushover/MQTT secrets), under `WebDashboardToken`.

## What it shows

* **Status** - version, uptime, image queue length, action queue length, and per-camera state (enabled,
  paused/resume countdown, last trigger time, alert/false-alert/irrelevant counters).
* **AI Servers** - name, type, enabled, online, in-use, last result message, average response time, and
  error counts for every server configured under Settings > AI Servers.
* **Recent history** - the last N alerts (default 50) with camera, time, detection summary, success, and
  a thumbnail. Thumbnails can show the plain alert image or the annotated version (with detection boxes
  drawn, same renderer the desktop History tab uses).
* **Pause / resume** - per camera or all cameras at once, from the dashboard page or via the API. This
  calls the exact same `Camera.Pause()`/`Camera.Resume()` used by the desktop Pause dialog and the tray
  menu's Pause All/Resume All - it doesn't reimplement pause logic.

The page auto-refreshes every 5 seconds, is plain HTML/CSS/vanilla JS with no external/CDN dependencies,
works on a phone, and follows your OS/browser's light/dark theme (`prefers-color-scheme`).

## API endpoints

All of these require the token (header or query param) and return JSON unless noted.

| Method | Path                         | Notes                                                             |
|--------|------------------------------|--------------------------------------------------------------------|
| GET    | `/api/status`                | Version, uptime, queue lengths, last detection time, cameras[]    |
| GET    | `/api/servers`               | One entry per configured AI server                                |
| GET    | `/api/history?limit=50`      | Most recent history items first (limit capped at 500)             |
| GET    | `/api/image?file=...`        | JPEG bytes. `file` must be a path the history database knows about |
| GET    | `/api/image?file=...&annotated=1` | Same, with detection boxes drawn on it                        |
| POST   | `/api/pause`                 | Body: `{"camera":"<name>|all","minutes":30}` (`minutes` optional) |
| POST   | `/api/resume`                | Body: `{"camera":"<name>|all"}`                                   |

Notes on hardening:

* `/api/pause` and `/api/resume` only accept `POST` - `GET` gets `405 Method Not Allowed`.
* `/api/image` only ever serves a file that's an exact match in the history database (by its full stored
  path) - it never resolves an arbitrary filesystem path, and any `..` in the `file` parameter is rejected
  outright before the database is even checked.
* Request bodies are capped (64 KB) to avoid abuse.
* Errors never leak exception details/stack traces to the client - they're logged server-side only.

## Why EmbedIO instead of a bare `HttpListener`

`System.Net.HttpListener` (in-box with .NET) uses Windows' `http.sys`, and binding it to anything other
than `localhost` requires either running as Administrator or a one-time, admin-only
`netsh http add urlacl` reservation. That would make "Allow LAN access" unusable for a normal, non-admin
user. EmbedIO's own listener mode (the default, `HttpListenerMode.EmbedIO`) is a small, cross-platform,
MIT-licensed, plain-socket listener instead of `http.sys`, so a normal user can flip on LAN access with no
elevation and no `netsh` step. EmbedIO has no dependency on ASP.NET Core/Kestrel and runs fine on the
.NET Desktop Runtime alone.
