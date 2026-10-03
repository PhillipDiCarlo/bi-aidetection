using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using AITool;
using Xunit;

namespace AITool.Tests;

public class FrigateSnapshotDownloadTests
{
    private static int GetFreePort()
    {
        TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    //a minimal stand-in for Frigate's HTTP API: GET /api/events/<id>/snapshot.jpg -> fixed bytes.
    private static (HttpListener listener, string url, Task serverTask) StartFakeFrigateServer(byte[] snapshotBytes, string eventId)
    {
        int port = GetFreePort();
        string url = $"http://127.0.0.1:{port}/";

        HttpListener listener = new HttpListener();
        listener.Prefixes.Add(url);
        listener.Start();

        Task serverTask = Task.Run(async () =>
        {
            try
            {
                HttpListenerContext ctx = await listener.GetContextAsync();
                if (ctx.Request.Url.AbsolutePath == $"/api/events/{eventId}/snapshot.jpg")
                {
                    ctx.Response.ContentType = "image/jpeg";
                    ctx.Response.StatusCode = 200;
                    await ctx.Response.OutputStream.WriteAsync(snapshotBytes, 0, snapshotBytes.Length);
                }
                else
                {
                    ctx.Response.StatusCode = 404;
                }
                ctx.Response.Close();
            }
            catch (HttpListenerException) { } //listener stopped while waiting
            catch (ObjectDisposedException) { }
        });

        return (listener, url, serverTask);
    }

    [Fact]
    public async Task DownloadSnapshotAsync_ReturnsBytesFromFrigateApi()
    {
        byte[] expected = { 0xFF, 0xD8, 1, 2, 3, 4 }; //fake jpeg bytes
        var (listener, url, serverTask) = StartFakeFrigateServer(expected, "evt-1");

        try
        {
            FrigateSource src = new FrigateSource();
            src.GetFrigateUrl = () => url;
            src.GetApiKey = () => "";

            byte[] result = await src.DownloadSnapshotAsync("evt-1");

            await Task.WhenAny(serverTask, Task.Delay(5000));

            Assert.Equal(expected, result);
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task ProcessEventJsonAsync_DownloadsSnapshot_SavesExpectedFileName_AndEnqueues()
    {
        byte[] expected = { 10, 20, 30, 40 };
        string eventId = "1696000000.123456-abc123";
        var (listener, url, serverTask) = StartFakeFrigateServer(expected, eventId);

        string tempFolder = Path.Combine(Path.GetTempPath(), "frigate_download_test_" + Guid.NewGuid());

        try
        {
            Camera cam = new Camera();
            cam.Name = "FrontDoor";
            cam.BICamName = "front_door";
            cam.Prefix = "FD";

            string enqueuedPath = null;
            Camera enqueuedCam = null;

            FrigateSource src = new FrigateSource();
            src.GetFrigateUrl = () => url;
            src.GetApiKey = () => "";
            src.GetCameras = () => new List<Camera> { cam };
            src.GetCameraFilter = () => "";
            src.GetLabelFilter = () => "";
            src.GetSnapshotFolderSetting = () => tempFolder;
            src.EnqueueAction = (path, c) => { enqueuedPath = path; enqueuedCam = c; };

            string json = $@"{{""type"":""new"",""after"":{{""id"":""{eventId}"",""camera"":""front_door"",""label"":""person"",""has_snapshot"":true}}}}";

            bool result = await src.ProcessEventJsonAsync(json);

            await Task.WhenAny(serverTask, Task.Delay(5000));

            Assert.True(result);
            Assert.NotNull(enqueuedPath);
            Assert.Same(cam, enqueuedCam);
            Assert.True(File.Exists(enqueuedPath));
            Assert.Equal(expected, await File.ReadAllBytesAsync(enqueuedPath));
            Assert.StartsWith("FD." + eventId + ".", Path.GetFileName(enqueuedPath));
            Assert.Equal(tempFolder, Path.GetDirectoryName(enqueuedPath));
        }
        finally
        {
            listener.Stop();
            try { Directory.Delete(tempFolder, true); } catch { }
        }
    }
}
