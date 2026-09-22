# Generic Webhook Action

AI Tool can call an arbitrary HTTP webhook when a camera triggers (and, optionally, when the alert is
canceled). Use this for services that don't have their own dedicated action, e.g. Discord, Home Assistant,
n8n, Zapier/IFTTT webhooks, etc.

## Setup

Cameras > [camera] > Actions > Webhook

* **Send Webhook** - enables the action.
* **URL** - the webhook endpoint to call on trigger. Template variables allowed.
* **Method** - HTTP method, e.g. `POST` or `PUT`. Defaults to `POST`.
* **Content-Type** - sent with the request when *Send Image* is unchecked. Defaults to `application/json`.
* **Headers** - one `Name: value` per line, e.g. `Authorization: Bearer abc123`. Template variables allowed.
* **Body** - the request body template. Defaults to `[AllJson]`, a ready-made JSON object containing the
  camera name, summary, filename, timestamp and full detection list. Template variables allowed.
* **Send Image** - when checked, the request is sent as `multipart/form-data` instead of using
  Content-Type/Body directly: each top-level field of the (JSON) Body is sent as its own form field, plus
  the alert image as a file part named `image`. If Body isn't valid JSON, it is sent whole as a single
  `payload` field instead.
* **Cancel URL** / **Cancel Body** - same as above, but used for the cancel event. The cancel call is only
  made when **Cancel URL** is non-empty.

See the **Variables** button on the Actions screen for the full list of `[variable]` names, e.g. `[camera]`,
`[SummaryNonEscaped]`, `[DetectionsJson]`, `[AllJson]`, `[ImagePathEscaped]`.

NOTE: `[SummaryJson]` and `[DetectionsJson]` already expand to complete JSON objects (e.g.
`{"summary": "..."}`), not bare values - don't wrap them in extra quotes/braces in your Body template.

## Example: Discord Webhook

Discord accepts a JSON `content` field on its webhook URL.

1. Create a webhook in Discord: Server Settings > Integrations > Webhooks > New Webhook, then copy the
   Webhook URL.
2. Configure the camera:
   * URL: `https://discord.com/api/webhooks/XXXXXXXXXXXX/YYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYY`
   * Method: `POST`
   * Content-Type: `application/json`
   * Body: `{"content":"[camera]: [SummaryNonEscaped]"}`
   * Send Image: leave unchecked (Discord webhooks don't accept an `image` form field the way this action
     sends it - if you want the picture itself in Discord, copy the alert image to a folder served over
     HTTP and link to it in `content` instead).

## Example: Home Assistant Webhook Trigger

Home Assistant automations can start from a "Webhook" trigger, which accepts either JSON or
`multipart/form-data`.

1. In Home Assistant, create an automation with a **Webhook** trigger and note the generated Webhook ID.
2. Configure the camera:
   * URL: `http://homeassistant.local:8123/api/webhook/YOUR-WEBHOOK-ID`
   * Method: `POST`
   * Content-Type: `application/json`
   * Body: `{"camera":"[camera]","summary":"[SummaryNonEscaped]"}`
   * Send Image: check this if you also want the alert image available as `trigger.data` in the
     automation (it arrives as the `image` form field).
3. In the automation, reference `{{ trigger.json.camera }}` (or `trigger.data.get('camera')` for the
   multipart/Send Image case) to use the values sent by AI Tool.
