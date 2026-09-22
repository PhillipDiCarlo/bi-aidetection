using System;
using System.Reflection;
using MQTTnet;
using SixLabors.ImageSharp;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Media;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Speech.Synthesis;
using System.Threading.Tasks;
using MQTTnet.Protocol;
using NPushover.RequestObjects;
using NPushover.ResponseObjects;
using Telegram.Bot.Exceptions;
using static AITool.AITOOL;

namespace AITool.Actions
{
    /// <summary>Publishes the trigger or cancel topic/payload pairs (and optionally the image) via MQTT.</summary>
    public class MqttAction : IActionChannel
    {
        public string Name => "Mqtt";

        public bool ShouldRun(ClsTriggerActionQueueItem AQI)
        {
            return AQI.cam.Action_mqtt_enabled && !(AQI.cam.Paused && AQI.cam.PauseMQTT);
        }

        public async Task<bool> RunAsync(ClsTriggerActionQueueItem AQI, string CurSrv)
        {
            bool ret = true;

            //make sure it is a matching object, but call MQTT in any case if it is a canceled event
            if (!AQI.Hist.IsNull() && (!AQI.Trigger || AQI.cam.MQTTTriggeringObjects.IsRelevant(AQI.Hist.Predictions(), false, out bool IgnoreImageMask, out bool IgnoreDynamicMask) == ResultType.Relevant))
            {
                string topic = "";
                string payload = "";

                if (AQI.Trigger)
                {
                    topic = AITOOL.ReplaceParams(AQI.cam, AQI.Hist, AQI.CurImg, AQI.cam.Action_mqtt_topic, Global.IPType.URL);
                    payload = AITOOL.ReplaceParams(AQI.cam, AQI.Hist, AQI.CurImg, AQI.cam.Action_mqtt_payload, Global.IPType.URL);
                    Log($"Debug: MQTT Trigger event - [SummaryNonEscaped]='{AQI.Hist.Detections}', After replacement Topic='{topic}', Payload='{payload}'");
                }
                else
                {
                    topic = AITOOL.ReplaceParams(AQI.cam, AQI.Hist, AQI.CurImg, AQI.cam.Action_mqtt_topic_cancel, Global.IPType.URL);
                    payload = AITOOL.ReplaceParams(AQI.cam, AQI.Hist, AQI.CurImg, AQI.cam.Action_mqtt_payload_cancel, Global.IPType.URL);
                    Log($"Debug: MQTT Cancel event - [SummaryNonEscaped]='{AQI.Hist.Detections}', After replacement Topic='{topic}', Payload='{payload}'");
                }


                List<string> topics = topic.SplitStr("|");
                List<string> payloads = payload.SplitStr("|");
                if (topics.Count == payloads.Count)
                {
                    ClsImageQueueItem ci = null;

                    for (int i = 0; i < topics.Count; i++)
                    {
                        if (AQI.cam.Action_mqtt_send_image && topics[i].IndexOf("/image", StringComparison.OrdinalIgnoreCase) >= 0)
                            ci = AQI.CurImg;
                        else
                            ci = null;
                        MqttClientPublishResult pr = await AITOOL.mqttClient.PublishAsync(topics[i], payloads[i], AQI.cam.Action_mqtt_retain_message, ci);
                        if (pr == null || pr.ReasonCode != MqttClientPublishReasonCode.Success)
                            ret = false;

                        if (AppSettings.Settings.ActionDelayMS >= 100)  //dont show for tiny delays
                            Log($"Debug:  ...Applying 'ActionDelayMS' delay of {AppSettings.Settings.ActionDelayMS}ms.");

                        await Task.Delay(AppSettings.Settings.ActionDelayMS); //very short wait between trigger events
                    }

                }
                else
                {
                    Log($"Error: You must have an equal number of MQTT topics and payloads. (separated by | pipe symbol).  Topics='{topic}', Payloads='{payloads}'");
                    ret = false;
                }

            }
            else
            {
                Log("Trace: Skipping MQTT call.");
                ret = true;   //dont return false unless actual error
            }


            return ret;
        }
    }
}
