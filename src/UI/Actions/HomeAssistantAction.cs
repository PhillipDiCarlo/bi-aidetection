using MQTTnet;
using MQTTnet.Protocol;

using System.Collections.Concurrent;
using System.Threading.Tasks;

using static AITool.AITOOL;

namespace AITool.Actions
{
    /// <summary>
    /// Publishes Home Assistant MQTT discovery configs (once per camera per process) and keeps the resulting
    /// binary_sensor/camera entities up to date on every trigger and cancel.
    /// </summary>
    public class HomeAssistantAction : IActionChannel
    {
        public string Name => "HomeAssistant";

        //cameras we have already published discovery configs for in this process - avoids resending retained
        //discovery messages on every single trigger
        private static readonly ConcurrentDictionary<string, byte> PublishedCameras = new ConcurrentDictionary<string, byte>();

        public bool ShouldRun(ClsTriggerActionQueueItem AQI)
        {
            return AppSettings.Settings.mqtt_HomeAssistantDiscovery && !(AQI.cam.Paused && AQI.cam.PauseMQTT);
        }

        public static void ForgetPublishedCameras()
        {
            PublishedCameras.Clear();
        }

        private async Task<bool> PublishDiscoveryIfNeeded(Camera cam)
        {
            bool ret = true;

            if (PublishedCameras.TryAdd(cam.Name.ToLower(), 0))
            {
                foreach (HADiscoveryMessage msg in HomeAssistantDiscovery.GetDiscoveryMessages(cam, AppSettings.Settings))
                {
                    MqttClientPublishResult pr = await AITOOL.mqttClient.PublishAsync(msg.Topic, msg.PayloadJson, true, null);
                    if (pr == null || pr.ReasonCode != MqttClientPublishReasonCode.Success)
                        ret = false;
                }

                Log($"Debug: HomeAssistant: Published discovery configs for camera '{cam.Name}'.");
            }

            return ret;
        }

        public async Task<bool> RunAsync(ClsTriggerActionQueueItem AQI, string CurSrv)
        {
            bool ret = true;

            if (!await this.PublishDiscoveryIfNeeded(AQI.cam))
                ret = false;

            string state = AQI.Trigger ? "ON" : "OFF";

            MqttClientPublishResult pr = await AITOOL.mqttClient.PublishAsync(HomeAssistantDiscovery.GetStateTopic(AQI.cam), state, false, null);
            if (pr == null || pr.ReasonCode != MqttClientPublishReasonCode.Success)
                ret = false;

            var predictions = AQI.Hist.IsNull() ? new System.Collections.Generic.List<ClsPrediction>() : AQI.Hist.Predictions();

            foreach (ObjectType objType in new[] { ObjectType.Person, ObjectType.Vehicle, ObjectType.Animal })
            {
                string objState = (AQI.Trigger && predictions.Exists(p => p.ObjType == objType)) ? "ON" : "OFF";
                pr = await AITOOL.mqttClient.PublishAsync(HomeAssistantDiscovery.GetObjectStateTopic(AQI.cam, objType), objState, false, null);
                if (pr == null || pr.ReasonCode != MqttClientPublishReasonCode.Success)
                    ret = false;
            }

            if (AQI.Trigger)
            {
                string attributes = HomeAssistantDiscovery.BuildAttributesPayload(AQI.Hist.IsNull() ? "" : AQI.Hist.Detections, predictions, AQI.CurImg.image_path, System.DateTime.Now);
                pr = await AITOOL.mqttClient.PublishAsync(HomeAssistantDiscovery.GetAttributesTopic(AQI.cam), attributes, false, null);
                if (pr == null || pr.ReasonCode != MqttClientPublishReasonCode.Success)
                    ret = false;

                if (AppSettings.Settings.mqtt_HomeAssistantPublishImage)
                {
                    pr = await AITOOL.mqttClient.PublishAsync(HomeAssistantDiscovery.GetImageTopic(AQI.cam), "", false, AQI.CurImg);
                    if (pr == null || pr.ReasonCode != MqttClientPublishReasonCode.Success)
                        ret = false;
                }
            }

            return ret;
        }
    }
}
