# MQTT with AI Tool & Blue Iris

Blue Iris [MQTT](https://mqtt.org/) ([ELI5](https://www.reddit.com/r/homeautomation/comments/515fh5/eli5_mqtt/))
support provides a way to [control the system](https://wiki.instar.com/Software/Windows/Blue_Iris_v5/INSTAR_MQTT/#controlling-blueiris-through-mqtt)
without relying on HTTP and URL based authentication.

## Requirements

A MQTT broker running on your local network, https://mosquitto.org/ is a commonly used OSS MQTT Broker.

## Setup

1.  [Configure Blue Iris](https://wiki.instar.com/Software/Windows/Blue_Iris_v5/INSTAR_MQTT/#configuring-the-blueiris-mqtt-service)
    to connect to your MQTT broker.
1.  Configure AI Tool to connect to your MQTT Broker
    * Cameras > [camera] > Action Settings > MQTT Settings
    * ![MQTT Config](https://imgur.com/S4sDjzs.png)
1.  Configure AI Tool to trigger the Camera via MQTT
    * Cameras > [camera] > Action Settings
    * MQTT Trigger Topic: `ai/[camera]/motion | BlueIris/admin`
    * MQTT Trigger Payload: `[detections] | camera=[camera]&trigger&memo=[SummaryNonEscaped]`

NOTE: Use `[SummaryNonEscaped]` instead of `[Summary]` in the BlueIris payload to avoid getting escaped characters in
the Blue Iris UI.

Now when AI Tool triggers it will publish to two MQTT topics `ai/[camera]/motion` and `BlueIris/admin`. BlueIris
subscribes to the `BlueIris/admin` topic and will trigger the named camera with the specified memo. With this setup
you no longer need the URL based integrations with BlueIris.

## Home Assistant

Enable **MQTT Settings > Enable Home Assistant Discovery** (Cameras > [camera] > Action Settings > MQTT Settings) and
AI Tool will publish [MQTT discovery](https://www.home-assistant.io/integrations/mqtt/#mqtt-discovery) configs for
every camera, so entities show up in Home Assistant automatically - no `configuration.yaml` editing needed.

All entities are grouped under one Home Assistant device (named by **Device Name**, default `AITool`). For each
camera you get:

* `binary_sensor.<camera>_motion` - `device_class: motion`, turned on with every trigger and automatically turned
  back off `off_delay` seconds later (or immediately on a cancel event).
* `binary_sensor.<camera>_person`, `..._vehicle`, `..._animal` - the same, but only turned on when that object class
  was actually detected in the triggering event. `person` uses `device_class: occupancy`, the others use `motion`.
* A `json_attributes_topic` on each binary sensor with the detections summary, the list of `{label, confidence}`
  predictions, the image path, and the trigger time.
* If **Publish Image** is also checked, a `camera.<camera>_image` entity showing the latest alert image
  (published as raw JPEG bytes, same as the existing `/image` MQTT topics).

Other settings:

* **Discovery Prefix** - the discovery topic prefix Home Assistant is listening on (default `homeassistant`, matches
  Home Assistant's default).
* **Off Delay (seconds)** - how long Home Assistant waits after the last "ON" before it turns a binary sensor back
  off on its own (default 30).

Discovery configs are retained and get republished for every camera whenever AI Tool (re)connects to the broker, so
Home Assistant picks them back up after a broker restart. Turning the setting on from the MQTT Settings dialog
publishes them immediately as well; turning it off does not remove the retained configs from the broker (delete the
retained messages on the broker, or use Home Assistant's "delete" discovery button, if you want to fully remove
them).