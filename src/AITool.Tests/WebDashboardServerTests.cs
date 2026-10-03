using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using AITool;
using AITool.WebDashboard;

using Newtonsoft.Json.Linq;

using Xunit;

namespace AITool.Tests;

/// <summary>
/// Starts the real embedded dashboard (EmbedIO) on a free loopback port for every test, so these exercise the
/// actual HTTP routing/auth/hardening rather than calling internal methods directly.
/// </summary>
public class WebDashboardServerTests : IDisposable
{
    private const string TestToken = "unit-test-token-0123456789abcdef";

    private readonly string baseUrl;
    private readonly Camera testCamera;

    private readonly bool enabledBefore;
    private readonly int portBefore;
    private readonly bool allowLanBefore;
    private readonly string tokenBefore;

    private static int GetFreePort()
    {
        TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public WebDashboardServerTests()
    {
        enabledBefore = AppSettings.Settings.WebDashboardEnabled;
        portBefore = AppSettings.Settings.WebDashboardPort;
        allowLanBefore = AppSettings.Settings.WebDashboardAllowLan;
        tokenBefore = AppSettings.Settings.WebDashboardToken;

        int port = GetFreePort();
        baseUrl = $"http://127.0.0.1:{port}";

        AppSettings.Settings.WebDashboardEnabled = true;
        AppSettings.Settings.WebDashboardPort = port;
        AppSettings.Settings.WebDashboardAllowLan = false;
        AppSettings.Settings.WebDashboardToken = TestToken;

        //Camera(name) runs UpdateCamera() - this is the supported way to build one outside of settings load (see CameraConstructionTests)
        testCamera = new Camera("WebDashboardTestCam");
        AppSettings.Settings.CameraList.Add(testCamera);

        WebDashboardServer.Start();

        //WebDashboardServer.Start() already blocks until the listener reports 'Listening', but be defensive on slow CI machines
        for (int i = 0; i < 100 && !WebDashboardServer.IsRunning; i++)
            Thread.Sleep(20);
    }

    public void Dispose()
    {
        WebDashboardServer.Stop();

        AppSettings.Settings.CameraList.Remove(testCamera);

        AppSettings.Settings.WebDashboardEnabled = enabledBefore;
        AppSettings.Settings.WebDashboardPort = portBefore;
        AppSettings.Settings.WebDashboardAllowLan = allowLanBefore;
        AppSettings.Settings.WebDashboardToken = tokenBefore;
    }

    [Fact]
    public async Task Status_Returns401WithoutToken()
    {
        using HttpClient client = new HttpClient();
        HttpResponseMessage res = await client.GetAsync($"{baseUrl}/api/status");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Status_Returns401WithWrongToken()
    {
        using HttpClient client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-the-right-token");
        HttpResponseMessage res = await client.GetAsync($"{baseUrl}/api/status");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Status_Returns200AndValidJsonWithToken()
    {
        using HttpClient client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestToken);

        HttpResponseMessage res = await client.GetAsync($"{baseUrl}/api/status");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.StartsWith("application/json", res.Content.Headers.ContentType?.MediaType);

        JObject json = JObject.Parse(await res.Content.ReadAsStringAsync());
        Assert.True(json.ContainsKey("Version"));
        Assert.True(json.ContainsKey("ImageQueueLength"));
        Assert.True(json.ContainsKey("ActionQueueLength"));
        Assert.True(json.ContainsKey("Cameras"));

        JArray cameras = (JArray)json["Cameras"];
        Assert.Contains(cameras, c => (string)c["Name"] == testCamera.Name);
    }

    [Fact]
    public async Task TokenAsQueryParam_IsAccepted()
    {
        //the dashboard page itself can't set an Authorization header on an <img> tag, so a ?token= query param must also work
        using HttpClient client = new HttpClient();
        HttpResponseMessage res = await client.GetAsync($"{baseUrl}/api/status?token={TestToken}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Servers_Returns200AndValidJsonWithToken()
    {
        using HttpClient client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestToken);

        HttpResponseMessage res = await client.GetAsync($"{baseUrl}/api/servers");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        //just needs to be a valid JSON array - AIURLList may legitimately be empty in a test process
        JArray.Parse(await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task History_Returns200AndValidJsonWithToken()
    {
        using HttpClient client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestToken);

        HttpResponseMessage res = await client.GetAsync($"{baseUrl}/api/history?limit=5");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        JArray.Parse(await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Pause_ViaPost_ChangesCameraPausedState_AndGetIsRejected()
    {
        using HttpClient client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestToken);

        //state-changing endpoint must refuse GET
        HttpResponseMessage getRes = await client.GetAsync($"{baseUrl}/api/pause");
        Assert.Equal(HttpStatusCode.MethodNotAllowed, getRes.StatusCode);

        Assert.False(testCamera.Paused);

        HttpResponseMessage pauseRes = await client.PostAsync($"{baseUrl}/api/pause",
            new StringContent($"{{\"camera\":\"{testCamera.Name}\"}}", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.OK, pauseRes.StatusCode);
        Assert.True(testCamera.Paused);

        HttpResponseMessage resumeRes = await client.PostAsync($"{baseUrl}/api/resume",
            new StringContent($"{{\"camera\":\"{testCamera.Name}\"}}", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.OK, resumeRes.StatusCode);
        Assert.False(testCamera.Paused);
    }

    [Fact]
    public async Task Pause_UnknownCamera_Returns404()
    {
        using HttpClient client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestToken);

        HttpResponseMessage res = await client.PostAsync($"{baseUrl}/api/pause",
            new StringContent("{\"camera\":\"NoSuchCameraExists\"}", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Image_RefusesFileNotInHistory()
    {
        using HttpClient client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestToken);

        string notInHistory = Uri.EscapeDataString(@"c:\somewhere\not-a-real-history-entry.jpg");
        HttpResponseMessage res = await client.GetAsync($"{baseUrl}/api/image?file={notInHistory}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Image_RefusesDotDotTraversal()
    {
        using HttpClient client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestToken);

        string traversal = Uri.EscapeDataString(@"..\..\..\Windows\win.ini");
        HttpResponseMessage res = await client.GetAsync($"{baseUrl}/api/image?file={traversal}");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task UnknownRoute_Returns404()
    {
        using HttpClient client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestToken);

        HttpResponseMessage res = await client.GetAsync($"{baseUrl}/api/does-not-exist");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task RootPage_ReturnsHtmlWithToken()
    {
        using HttpClient client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestToken);

        HttpResponseMessage res = await client.GetAsync($"{baseUrl}/");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.StartsWith("text/html", res.Content.Headers.ContentType?.MediaType);
    }
}
