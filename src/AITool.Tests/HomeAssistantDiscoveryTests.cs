using System;
using System.Collections.Generic;
using AITool;
using Newtonsoft.Json.Linq;
using Xunit;

namespace AITool.Tests;

public class HomeAssistantDiscoveryTests
{
    private static AppSettings.ClsSettings MakeSettings()
    {
        return new AppSettings.ClsSettings
        {
            mqtt_clientid = "AITool-Test",
            mqtt_LastWillTopic = "AITool/status",
            mqtt_LastWillPayload = "Offline",
            mqtt_OnlinePayload = "Online",
            mqtt_HomeAssistantDiscoveryPrefix = "homeassistant",
            mqtt_HomeAssistantDeviceName = "AITool",
            mqtt_HomeAssistantOffDelaySeconds = 30,
            mqtt_HomeAssistantPublishImage = false,
        };
    }

    [Theory]
    [InlineData("Front Door", "front_door")]
    [InlineData("Back-Yard Cam!", "back_yard_cam_")]
    [InlineData("ALLCAPS", "allcaps")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void SanitizeName_LowercasesAndReplacesInvalidChars(string input, string expected)
    {
        Assert.Equal(expected, HomeAssistantDiscovery.SanitizeName(input));
    }

    [Fact]
    public void GetDiscoveryMessages_ReturnsMotionAndObjectClassSensorsWithExpectedTopics()
    {
        var cam = new Camera { Name = "Front Door" };
        var settings = MakeSettings();

        List<HADiscoveryMessage> messages = HomeAssistantDiscovery.GetDiscoveryMessages(cam, settings);

        //motion + person + vehicle + animal, no camera entity since PublishImage is off
        Assert.Equal(4, messages.Count);

        HADiscoveryMessage motion = messages.Find(m => m.Topic == "homeassistant/binary_sensor/aitool_front_door_motion/config");
        Assert.NotNull(motion);

        JObject payload = JObject.Parse(motion.PayloadJson);
        Assert.Equal("Front Door Motion", payload["name"]);
        Assert.Equal("aitool_front_door_motion", payload["unique_id"]);
        Assert.Equal("motion", payload["device_class"]);
        Assert.Equal("aitool/front_door/state", payload["state_topic"]);
        Assert.Equal("ON", payload["payload_on"]);
        Assert.Equal("OFF", payload["payload_off"]);
        Assert.Equal(30, payload["off_delay"]);
        Assert.Equal("aitool/front_door/attributes", payload["json_attributes_topic"]);
    }

    [Fact]
    public void GetDiscoveryMessages_ObjectClassSensorsUseCorrectDeviceClassesAndTopics()
    {
        var cam = new Camera { Name = "Driveway" };
        var settings = MakeSettings();

        List<HADiscoveryMessage> messages = HomeAssistantDiscovery.GetDiscoveryMessages(cam, settings);

        JObject person = JObject.Parse(messages.Find(m => m.Topic == "homeassistant/binary_sensor/aitool_driveway_person/config").PayloadJson);
        Assert.Equal("occupancy", person["device_class"]);
        Assert.Equal("aitool/driveway/person", person["state_topic"]);

        JObject vehicle = JObject.Parse(messages.Find(m => m.Topic == "homeassistant/binary_sensor/aitool_driveway_vehicle/config").PayloadJson);
        Assert.Equal("motion", vehicle["device_class"]);
        Assert.Equal("aitool/driveway/vehicle", vehicle["state_topic"]);

        JObject animal = JObject.Parse(messages.Find(m => m.Topic == "homeassistant/binary_sensor/aitool_driveway_animal/config").PayloadJson);
        Assert.Equal("motion", animal["device_class"]);
        Assert.Equal("aitool/driveway/animal", animal["state_topic"]);
    }

    [Fact]
    public void GetDiscoveryMessages_AllEntitiesShareOneDeviceBlockAndAvailability()
    {
        var cam = new Camera { Name = "Garage" };
        var settings = MakeSettings();

        List<HADiscoveryMessage> messages = HomeAssistantDiscovery.GetDiscoveryMessages(cam, settings);

        foreach (HADiscoveryMessage msg in messages)
        {
            JObject payload = JObject.Parse(msg.PayloadJson);

            Assert.Equal("AITool/status", payload["availability_topic"]);
            Assert.Equal("Online", payload["payload_available"]);
            Assert.Equal("Offline", payload["payload_not_available"]);

            JObject device = (JObject)payload["device"];
            Assert.Equal("AITool", device["manufacturer"]);
            Assert.Equal("AITool", device["name"]);
            JArray identifiers = (JArray)device["identifiers"];
            Assert.Single(identifiers);
            Assert.Equal("aitool_aitool_test", identifiers[0]);
            Assert.False(string.IsNullOrEmpty(device["sw_version"].ToString()));
        }
    }

    [Fact]
    public void GetDiscoveryMessages_AddsCameraEntityOnlyWhenPublishImageEnabled()
    {
        var cam = new Camera { Name = "Backyard" };
        var settings = MakeSettings();
        settings.mqtt_HomeAssistantPublishImage = true;

        List<HADiscoveryMessage> messages = HomeAssistantDiscovery.GetDiscoveryMessages(cam, settings);

        Assert.Equal(5, messages.Count);
        HADiscoveryMessage camMsg = messages.Find(m => m.Topic == "homeassistant/camera/aitool_backyard_image/config");
        Assert.NotNull(camMsg);

        JObject payload = JObject.Parse(camMsg.PayloadJson);
        Assert.Equal("aitool/backyard/image", payload["topic"]);
        Assert.Equal("aitool_backyard_image", payload["unique_id"]);
    }

    [Fact]
    public void GetDiscoveryMessages_RespectsCustomDiscoveryPrefix()
    {
        var cam = new Camera { Name = "Front" };
        var settings = MakeSettings();
        settings.mqtt_HomeAssistantDiscoveryPrefix = "custom-prefix";

        List<HADiscoveryMessage> messages = HomeAssistantDiscovery.GetDiscoveryMessages(cam, settings);

        Assert.Contains(messages, m => m.Topic == "custom-prefix/binary_sensor/aitool_front_motion/config");
    }

    [Fact]
    public void BuildAttributesPayload_IncludesDetectionsPredictionsImageAndTime()
    {
        DateTime time = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        List<ClsPrediction> predictions = new List<ClsPrediction>
        {
            new ClsPrediction { Label = "Person", Confidence = 92.5 },
            new ClsPrediction { Label = "Car", Confidence = 81 },
        };

        string json = HomeAssistantDiscovery.BuildAttributesPayload("Person (92.5%); Car (81%)", predictions, "C:\\images\\test.jpg", time);

        JObject payload = JObject.Parse(json);
        Assert.Equal("Person (92.5%); Car (81%)", payload["detections"]);
        Assert.Equal("C:\\images\\test.jpg", payload["image"]);
        Assert.Equal(time.ToString("o"), payload["time"]);

        JArray preds = (JArray)payload["predictions"];
        Assert.Equal(2, preds.Count);
        Assert.Equal("Person", preds[0]["label"]);
        Assert.Equal(92.5, preds[0]["confidence"]);
        Assert.Equal("Car", preds[1]["label"]);
        Assert.Equal(81, preds[1]["confidence"]);
    }

    [Fact]
    public void BuildAttributesPayload_HandlesEmptyPredictions()
    {
        string json = HomeAssistantDiscovery.BuildAttributesPayload("false alert", new List<ClsPrediction>(), "", DateTime.Now);

        JObject payload = JObject.Parse(json);
        JArray preds = (JArray)payload["predictions"];
        Assert.Empty(preds);
    }
}
