using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using AITool;
using AITool.Actions;
using Xunit;

namespace AITool.Tests;

public class WebhookActionParseHeadersTests
{
    [Fact]
    public void ParseHeaders_SplitsNameAndValue()
    {
        var headers = WebhookAction.ParseHeaders("Authorization: Bearer abc123\r\nX-Custom: 1");

        Assert.Equal(2, headers.Count);
        Assert.Equal(("Authorization", "Bearer abc123"), headers[0]);
        Assert.Equal(("X-Custom", "1"), headers[1]);
    }

    [Fact]
    public void ParseHeaders_IgnoresBlankLines()
    {
        var headers = WebhookAction.ParseHeaders("Authorization: Bearer abc123\r\n\r\n\r\nX-Custom: 1");

        Assert.Equal(2, headers.Count);
    }

    [Fact]
    public void ParseHeaders_IgnoresLinesWithoutColon()
    {
        var headers = WebhookAction.ParseHeaders("not-a-header\r\nAuthorization: Bearer abc123");

        Assert.Single(headers);
        Assert.Equal(("Authorization", "Bearer abc123"), headers[0]);
    }

    [Fact]
    public void ParseHeaders_KeepsColonsInValue()
    {
        //header values (e.g. Bearer tokens, times) can contain their own colons - only the FIRST colon separates name from value
        var headers = WebhookAction.ParseHeaders("X-Time: 12:30:00");

        Assert.Single(headers);
        Assert.Equal(("X-Time", "12:30:00"), headers[0]);
    }

    [Fact]
    public void ParseHeaders_EmptyStringReturnsNoHeaders()
    {
        var headers = WebhookAction.ParseHeaders("");

        Assert.Empty(headers);
    }

    [Fact]
    public void ParseHeaders_TrimsWhitespaceAroundNameAndValue()
    {
        var headers = WebhookAction.ParseHeaders("  X-Custom  :   spaced out value  ");

        Assert.Single(headers);
        Assert.Equal(("X-Custom", "spaced out value"), headers[0]);
    }
}

public class WebhookActionIntegrationTests
{
    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    [Fact]
    public async Task RunAsync_PostsExpectedMethodContentTypeAndBody_ToTriggerUrl()
    {
        int port = GetFreePort();
        string url = $"http://127.0.0.1:{port}/webhook/";

        using HttpListener listener = new HttpListener();
        listener.Prefixes.Add(url);
        listener.Start();

        string receivedMethod = null;
        string receivedContentType = null;
        string receivedBody = null;

        Task serverTask = Task.Run(async () =>
        {
            HttpListenerContext ctx = await listener.GetContextAsync();
            receivedMethod = ctx.Request.HttpMethod;
            receivedContentType = ctx.Request.ContentType;
            using (StreamReader reader = new StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding))
                receivedBody = await reader.ReadToEndAsync();

            ctx.Response.StatusCode = 200;
            ctx.Response.Close();
        });

        //use the parameterless constructor - Camera(name) runs UpdateCamera(), which pulls in
        //ClsRelevantObjectManager and other static app state that isn't safe/needed here
        Camera cam = new Camera();
        cam.Name = "WebhookTestCam";
        cam.BICamName = "WebhookTestCam";
        cam.Action_webhook_enabled = true;
        cam.Action_webhook_url = url;
        cam.Action_webhook_method = "POST";
        cam.Action_webhook_content_type = "application/json";
        cam.Action_webhook_body = "{\"camera\":\"[camera]\"}";

        ClsImageQueueItem curImg = new ClsImageQueueItem(Path.Combine(AppContext.BaseDirectory, "TestImage.jpg"), 0);
        ClsTriggerActionQueueItem AQI = new ClsTriggerActionQueueItem(TriggerType.All, cam, curImg, null, true, "test", false);

        WebhookAction action = new WebhookAction();
        bool result = await action.RunAsync(AQI, "");

        await Task.WhenAny(serverTask, Task.Delay(5000));
        listener.Stop();

        Assert.True(result);
        Assert.Equal("POST", receivedMethod);
        Assert.StartsWith("application/json", receivedContentType);
        Assert.Equal("{\"camera\":\"WebhookTestCam\"}", receivedBody);
    }

    [Fact]
    public async Task RunAsync_CancelEvent_OnlyCallsCancelUrlWhenSet()
    {
        int port = GetFreePort();
        string url = $"http://127.0.0.1:{port}/webhook-cancel/";

        using HttpListener listener = new HttpListener();
        listener.Prefixes.Add(url);
        listener.Start();

        string receivedBody = null;

        Task serverTask = Task.Run(async () =>
        {
            HttpListenerContext ctx = await listener.GetContextAsync();
            using (StreamReader reader = new StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding))
                receivedBody = await reader.ReadToEndAsync();

            ctx.Response.StatusCode = 200;
            ctx.Response.Close();
        });

        Camera cam = new Camera();
        cam.Name = "WebhookCancelCam";
        cam.BICamName = "WebhookCancelCam";
        cam.Action_webhook_enabled = true;
        cam.Action_webhook_url = "";
        cam.Action_webhook_cancel_url = url;
        cam.Action_webhook_cancel_body = "{\"camera\":\"[camera]\",\"event\":\"cancel\"}";

        ClsImageQueueItem curImg = new ClsImageQueueItem(Path.Combine(AppContext.BaseDirectory, "TestImage.jpg"), 0);
        ClsTriggerActionQueueItem AQI = new ClsTriggerActionQueueItem(TriggerType.Cancel, cam, curImg, null, false, "test", false);

        WebhookAction action = new WebhookAction();

        Assert.True(action.ShouldRun(AQI));

        bool result = await action.RunAsync(AQI, "");

        await Task.WhenAny(serverTask, Task.Delay(5000));
        listener.Stop();

        Assert.True(result);
        Assert.Equal("{\"camera\":\"WebhookCancelCam\",\"event\":\"cancel\"}", receivedBody);
    }
}
