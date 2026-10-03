using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using AITool;
using MQTTnet.Server;
using Xunit;

namespace AITool.Tests;

//End to end: an in-process MQTTnet.Server broker, AITool's own MQTTClient subscribing/publishing against it (not the global
//AITOOL.mqttClient singleton - the enqueue call is injected instead of touching the real image queue), and a fake Frigate
//HTTP endpoint for the snapshot. Confirms a "frigate/events" message really results in a saved file + enqueue call.
public class FrigateMqttEndToEndTests
{
    private static int GetFreePort()
    {
        TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    [Fact]
    public async Task FrigateEventPublishedOverMqtt_IsDownloadedAndEnqueued()
    {
        int mqttPort = GetFreePort();

        MqttServerFactory serverFactory = new MqttServerFactory();
        MqttServerOptions serverOptions = new MqttServerOptionsBuilder()
            .WithDefaultEndpoint()
            .WithDefaultEndpointPort(mqttPort)
            .Build();

        using MqttServer broker = serverFactory.CreateMqttServer(serverOptions);
        await broker.StartAsync();

        //fake Frigate snapshot HTTP endpoint
        byte[] snapshotBytes = { 9, 8, 7, 6, 5 };
        string eventId = "1696000000.999999-e2e";
        int httpPort = GetFreePort();
        string httpUrl = $"http://127.0.0.1:{httpPort}/";

        using HttpListener httpListener = new HttpListener();
        httpListener.Prefixes.Add(httpUrl);
        httpListener.Start();

        Task httpServerTask = Task.Run(async () =>
        {
            try
            {
                HttpListenerContext ctx = await httpListener.GetContextAsync();
                ctx.Response.ContentType = "image/jpeg";
                ctx.Response.StatusCode = 200;
                await ctx.Response.OutputStream.WriteAsync(snapshotBytes, 0, snapshotBytes.Length);
                ctx.Response.Close();
            }
            catch (HttpListenerException) { }
            catch (ObjectDisposedException) { }
        });

        //MQTTClient.Connect() reads AppSettings.Settings.mqtt_* globally - point it at our in-process broker for this test
        //and restore it afterwards so other tests aren't affected.
        string savedServerAndPort = AppSettings.Settings.mqtt_serverandport;
        bool savedTls = AppSettings.Settings.mqtt_UseTLS;
        string savedClientId = AppSettings.Settings.mqtt_clientid;

        string tempFolder = Path.Combine(Path.GetTempPath(), "frigate_e2e_test_" + Guid.NewGuid());

        MQTTClient subscriberClient = null;
        MQTTClient publisherClient = null;

        try
        {
            AppSettings.Settings.mqtt_serverandport = $"127.0.0.1:{mqttPort}";
            AppSettings.Settings.mqtt_UseTLS = false;

            Camera cam = new Camera();
            cam.Name = "FrontDoor";
            cam.BICamName = "front_door";
            cam.Prefix = "FD";

            string enqueuedPath = null;
            Camera enqueuedCam = null;
            System.Threading.SemaphoreSlim enqueued = new System.Threading.SemaphoreSlim(0);

            FrigateSource src = new FrigateSource();
            src.GetFrigateUrl = () => httpUrl;
            src.GetApiKey = () => "";
            src.GetCameras = () => new List<Camera> { cam };
            src.GetCameraFilter = () => "";
            src.GetLabelFilter = () => "";
            src.GetSnapshotFolderSetting = () => tempFolder;
            src.EnqueueAction = (path, c) =>
            {
                enqueuedPath = path;
                enqueuedCam = c;
                enqueued.Release();
            };

            //each MQTTClient.Connect() uses the shared AppSettings.Settings.mqtt_clientid - two connections with the same
            //client id make the broker drop the first one, so give the subscriber and publisher distinct ids.
            AppSettings.Settings.mqtt_clientid = "sub-" + Guid.NewGuid();
            subscriberClient = new MQTTClient();
            await subscriberClient.SubscribeAsync("frigate/events", src.OnMqttMessageAsync);

            string json = $@"{{""type"":""new"",""after"":{{""id"":""{eventId}"",""camera"":""front_door"",""label"":""person"",""has_snapshot"":true}}}}";

            AppSettings.Settings.mqtt_clientid = "pub-" + Guid.NewGuid();
            publisherClient = new MQTTClient();
            await publisherClient.PublishAsync("frigate/events", json, false, null);

            bool gotEnqueue = await enqueued.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.True(gotEnqueue, "Timed out waiting for the Frigate event to be processed and enqueued.");
            Assert.NotNull(enqueuedPath);
            Assert.Same(cam, enqueuedCam);
            Assert.True(File.Exists(enqueuedPath));
            Assert.Equal(snapshotBytes, await File.ReadAllBytesAsync(enqueuedPath));
            Assert.StartsWith("FD." + eventId + ".", Path.GetFileName(enqueuedPath));
        }
        finally
        {
            AppSettings.Settings.mqtt_serverandport = savedServerAndPort;
            AppSettings.Settings.mqtt_UseTLS = savedTls;
            AppSettings.Settings.mqtt_clientid = savedClientId;

            if (subscriberClient != null)
                await subscriberClient.DisposeAsync();
            if (publisherClient != null)
                await publisherClient.DisposeAsync();

            httpListener.Stop();
            await broker.StopAsync();

            try { Directory.Delete(tempFolder, true); } catch { }
        }
    }
}
