using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AITool
{
    /// <summary>One "&lt;discovery_prefix&gt;/&lt;component&gt;/&lt;object_id&gt;/config" message for Home Assistant MQTT discovery.</summary>
    public class HADiscoveryMessage
    {
        public string Topic { get; set; } = "";
        public string PayloadJson { get; set; } = "";
        public HADiscoveryMessage(string Topic, string PayloadJson)
        {
            this.Topic = Topic;
            this.PayloadJson = PayloadJson;
        }
    }

    /// <summary>
    /// Builds Home Assistant MQTT discovery config payloads (https://www.home-assistant.io/integrations/mqtt/) and the
    /// matching state/attribute/image topic names for a camera. Pure functions only - no MQTT I/O happens here, the
    /// caller (HomeAssistantAction / MQTTClient) is responsible for actually publishing the returned messages.
    /// </summary>
    public static class HomeAssistantDiscovery
    {
        //object classes that get their own binary_sensor, beyond the main "Motion" one
        private static readonly (ObjectType ObjType, string TopicSuffix, string DeviceClass)[] ObjectClasses = new[]
        {
            (ObjectType.Person, "person", "occupancy"),
            (ObjectType.Vehicle, "vehicle", "motion"),
            (ObjectType.Animal, "animal", "motion"),
        };

        /// <summary>Lowercases and replaces anything not [a-z0-9_] with '_' so a name is safe to use in an MQTT topic or a HA unique_id.</summary>
        public static string SanitizeName(string Name)
        {
            if (Name.IsEmpty())
                return "";

            return Regex.Replace(Name.Trim().ToLowerInvariant(), "[^a-z0-9_]", "_");
        }

        public static string GetStateTopic(Camera cam)
        {
            return $"aitool/{SanitizeName(cam.Name)}/state";
        }

        public static string GetAttributesTopic(Camera cam)
        {
            return $"aitool/{SanitizeName(cam.Name)}/attributes";
        }

        public static string GetImageTopic(Camera cam)
        {
            return $"aitool/{SanitizeName(cam.Name)}/image";
        }

        public static string GetObjectStateTopic(Camera cam, ObjectType ObjType)
        {
            foreach (var oc in ObjectClasses)
            {
                if (oc.ObjType == ObjType)
                    return $"aitool/{SanitizeName(cam.Name)}/{oc.TopicSuffix}";
            }
            return "";
        }

        public static string GetDeviceId(AppSettings.ClsSettings Settings)
        {
            return $"aitool_{SanitizeName(Settings.mqtt_clientid)}";
        }

        private static string GetSoftwareVersion()
        {
            return Assembly.GetExecutingAssembly().GetName().Version.ToString();
        }

        private static JObject BuildDeviceBlock(AppSettings.ClsSettings Settings, string SwVersion)
        {
            JObject device = new JObject
            {
                ["identifiers"] = new JArray(GetDeviceId(Settings)),
                ["name"] = Settings.mqtt_HomeAssistantDeviceName,
                ["manufacturer"] = "AITool",
                ["sw_version"] = SwVersion,
            };
            return device;
        }

        private static JObject BuildAvailability(AppSettings.ClsSettings Settings)
        {
            return new JObject
            {
                ["availability_topic"] = Settings.mqtt_LastWillTopic,
                ["payload_available"] = Settings.mqtt_OnlinePayload,
                ["payload_not_available"] = Settings.mqtt_LastWillPayload,
            };
        }

        private static JObject BuildBinarySensor(Camera cam, AppSettings.ClsSettings Settings, string SwVersion, string NameSuffix, string UniqueIdSuffix, string DeviceClass, string StateTopic)
        {
            JObject o = new JObject
            {
                ["name"] = $"{cam.Name} {NameSuffix}",
                ["unique_id"] = $"aitool_{SanitizeName(cam.Name)}_{UniqueIdSuffix}",
                ["device_class"] = DeviceClass,
                ["state_topic"] = StateTopic,
                ["payload_on"] = "ON",
                ["payload_off"] = "OFF",
                ["off_delay"] = Settings.mqtt_HomeAssistantOffDelaySeconds,
                ["json_attributes_topic"] = GetAttributesTopic(cam),
            };
            o.Merge(BuildAvailability(Settings));
            o["device"] = BuildDeviceBlock(Settings, SwVersion);
            return o;
        }

        /// <summary>Builds the discovery ("&lt;prefix&gt;/&lt;component&gt;/&lt;object_id&gt;/config", retained payload) messages for one camera.</summary>
        public static List<HADiscoveryMessage> GetDiscoveryMessages(Camera cam, AppSettings.ClsSettings Settings)
        {
            List<HADiscoveryMessage> ret = new List<HADiscoveryMessage>();
            string SwVersion = GetSoftwareVersion();
            string prefix = Settings.mqtt_HomeAssistantDiscoveryPrefix;
            string camId = SanitizeName(cam.Name);

            JObject motion = BuildBinarySensor(cam, Settings, SwVersion, "Motion", "motion", "motion", GetStateTopic(cam));
            ret.Add(new HADiscoveryMessage($"{prefix}/binary_sensor/aitool_{camId}_motion/config", motion.ToString(Formatting.None)));

            foreach (var oc in ObjectClasses)
            {
                JObject sensor = BuildBinarySensor(cam, Settings, SwVersion, oc.TopicSuffix.UpperFirst(), oc.TopicSuffix, oc.DeviceClass, GetObjectStateTopic(cam, oc.ObjType));
                ret.Add(new HADiscoveryMessage($"{prefix}/binary_sensor/aitool_{camId}_{oc.TopicSuffix}/config", sensor.ToString(Formatting.None)));
            }

            if (Settings.mqtt_HomeAssistantPublishImage)
            {
                JObject camEntity = new JObject
                {
                    ["name"] = $"{cam.Name} Image",
                    ["unique_id"] = $"aitool_{camId}_image",
                    ["topic"] = GetImageTopic(cam),
                };
                camEntity.Merge(BuildAvailability(Settings));
                camEntity["device"] = BuildDeviceBlock(Settings, SwVersion);
                ret.Add(new HADiscoveryMessage($"{prefix}/camera/aitool_{camId}_image/config", camEntity.ToString(Formatting.None)));
            }

            return ret;
        }

        /// <summary>Builds the JSON payload published to the camera's attributes topic alongside each trigger.</summary>
        public static string BuildAttributesPayload(string DetectionsSummary, IEnumerable<ClsPrediction> Predictions, string ImagePath, DateTime Time)
        {
            JArray predictions = new JArray();
            if (Predictions != null)
            {
                foreach (ClsPrediction pred in Predictions)
                {
                    predictions.Add(new JObject
                    {
                        ["label"] = pred.Label,
                        ["confidence"] = pred.Confidence,
                    });
                }
            }

            JObject o = new JObject
            {
                ["detections"] = DetectionsSummary,
                ["predictions"] = predictions,
                ["image"] = ImagePath,
                ["time"] = Time.ToString("o"),
            };

            return o.ToString(Formatting.None);
        }
    }
}
