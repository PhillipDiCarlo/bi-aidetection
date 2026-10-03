using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using AITool;
using Newtonsoft.Json.Linq;
using Xunit;

namespace AITool.Tests;

public class BlueIrisSessionComputeResponseTests
{
    [Fact]
    public void ComputeLoginResponse_MatchesKnownMd5Vector()
    {
        //md5("admin:abc123:secret") independently verified with `printf 'admin:abc123:secret' | md5sum`
        string response = BlueIrisSession.ComputeLoginResponse("admin", "abc123", "secret");
        Assert.Equal("b556a555c2c690feeac3ed0e9a5e2a31", response);
    }

    [Fact]
    public void ComputeLoginResponse_IsLowerCaseHex()
    {
        string response = BlueIrisSession.ComputeLoginResponse("User", "Session123", "Password!");
        Assert.Matches("^[0-9a-f]{32}$", response);
    }
}

public class BlueIrisSessionUrlRewriteTests
{
    [Fact]
    public void RewriteUrlWithSession_RemovesUserAndPw_PreservesEverythingElseByteForByte()
    {
        string url = "http://127.0.0.1:81/admin?camera=Front&trigger&user=admin&pw=hunter2&flagalert=2&memo=person%20%2891%2C53%25%29&jpeg=C%3A%5Cimg.jpg";
        string rewritten = BlueIrisSession.RewriteUrlWithSession(url, "abc123session");

        Assert.DoesNotContain("user=admin", rewritten);
        Assert.DoesNotContain("pw=hunter2", rewritten);
        Assert.Equal("http://127.0.0.1:81/admin?camera=Front&trigger&flagalert=2&memo=person%20%2891%2C53%25%29&jpeg=C%3A%5Cimg.jpg&session=abc123session", rewritten);
    }

    [Fact]
    public void RewriteUrlWithSession_NoUserPw_JustAddsSession()
    {
        string rewritten = BlueIrisSession.RewriteUrlWithSession("http://127.0.0.1:81/admin?camera=Front&trigger", "xyz");

        Assert.Equal("http://127.0.0.1:81/admin?camera=Front&trigger&session=xyz", rewritten);
    }

    [Fact]
    public void RewriteUrlWithSession_NoQueryString_AddsSessionAsOnlyParam()
    {
        string rewritten = BlueIrisSession.RewriteUrlWithSession("http://127.0.0.1:81/admin", "xyz");

        Assert.Equal("http://127.0.0.1:81/admin?session=xyz", rewritten);
    }

    [Fact]
    public void RewriteUrlWithSession_OnlyRemovesExactUserOrPwKeys_NotParamsThatContainThemAsSubstrings()
    {
        //"username"/"pwhash" etc. must survive - only an EXACT (case-insensitive) key match of "user"/"pw" is removed
        string url = "http://127.0.0.1:81/admin?username=admin&pwhash=xyz&camera=Front&USER=admin2&PW=secret2";
        string rewritten = BlueIrisSession.RewriteUrlWithSession(url, "sess");

        Assert.Contains("username=admin", rewritten);
        Assert.Contains("pwhash=xyz", rewritten);
        Assert.Contains("camera=Front", rewritten);
        Assert.DoesNotContain("USER=admin2", rewritten);
        Assert.DoesNotContain("PW=secret2", rewritten);
    }
}

public class BlueIrisSessionHostMatchTests
{
    [Fact]
    public void IsBlueIrisHost_MatchesConfiguredServer_CaseInsensitive()
    {
        string original = AppSettings.Settings.BlueIrisServer;
        try
        {
            AppSettings.Settings.BlueIrisServer = "MyNvrBox";

            Assert.True(BlueIrisSession.IsBlueIrisHost("mynvrbox"));
            Assert.True(BlueIrisSession.IsBlueIrisHost("MYNVRBOX"));
            Assert.False(BlueIrisSession.IsBlueIrisHost("someotherhost"));
            Assert.False(BlueIrisSession.IsBlueIrisHost(""));
        }
        finally
        {
            AppSettings.Settings.BlueIrisServer = original;
        }
    }

    [Fact]
    public void IsBlueIrisHost_TreatsLocalhostAndLoopbackAsEquivalent()
    {
        string original = AppSettings.Settings.BlueIrisServer;
        try
        {
            AppSettings.Settings.BlueIrisServer = "127.0.0.1";

            Assert.True(BlueIrisSession.IsBlueIrisHost("localhost"));
            Assert.True(BlueIrisSession.IsBlueIrisHost("127.0.0.1"));
        }
        finally
        {
            AppSettings.Settings.BlueIrisServer = original;
        }
    }
}

/// <summary>
/// Minimal fake Blue Iris "/json" endpoint implementing the documented two-step login handshake
/// (see BlueIrisSession.cs remarks for sources), for testing BlueIrisSession's login/caching/re-login logic
/// without a real Blue Iris server.
/// </summary>
internal class FakeBlueIrisJsonServer : IDisposable
{
    public const string TestUser = "admin";
    public const string TestPassword = "hunter2";

    private readonly HttpListener listener;
    private readonly Task serverTask;
    private int sessionCounter = 0;

    public int LoginChallengesIssued = 0; //counts step-1 "cmd":"login" calls, i.e. full login attempts
    public bool CorrectCredentials = true;

    public string Url { get; }

    public FakeBlueIrisJsonServer(int port)
    {
        this.Url = $"http://127.0.0.1:{port}";
        this.listener = new HttpListener();
        this.listener.Prefixes.Add(this.Url + "/json/");
        this.listener.Start();
        this.serverTask = Task.Run(RunAsync);
    }

    private async Task RunAsync()
    {
        while (this.listener.IsListening)
        {
            HttpListenerContext ctx;
            try
            {
                ctx = await this.listener.GetContextAsync();
            }
            catch (Exception)
            {
                return; //listener was stopped
            }

            try
            {
                string body;
                using (StreamReader reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8))
                    body = await reader.ReadToEndAsync();

                JObject req = JObject.Parse(body);
                string cmd = (string)req["cmd"];
                JObject resp;

                if (cmd == "login" && req["response"] == null)
                {
                    //step 1: unauthenticated challenge request
                    this.LoginChallengesIssued++;
                    string session = "sess" + (++this.sessionCounter);
                    resp = new JObject { ["result"] = "fail", ["session"] = session };
                }
                else if (cmd == "login")
                {
                    //step 2: MD5("user:session:password") response
                    string session = (string)req["session"];
                    string response = (string)req["response"];
                    string expected = BlueIrisSession.ComputeLoginResponse(TestUser, session, TestPassword);

                    if (this.CorrectCredentials && string.Equals(response, expected, StringComparison.OrdinalIgnoreCase))
                        resp = new JObject { ["result"] = "success", ["data"] = new JObject { ["system name"] = "TestNVR", ["version"] = "5.7.9.9" } };
                    else
                        resp = new JObject { ["result"] = "fail" };
                }
                else
                {
                    resp = new JObject { ["result"] = "fail" };
                }

                byte[] buf = Encoding.UTF8.GetBytes(resp.ToString(Newtonsoft.Json.Formatting.None));
                ctx.Response.ContentType = "application/json";
                ctx.Response.ContentLength64 = buf.Length;
                await ctx.Response.OutputStream.WriteAsync(buf, 0, buf.Length);
                ctx.Response.Close();
            }
            catch (Exception)
            {
                try { ctx.Response.Close(); } catch (Exception) { }
            }
        }
    }

    public void Dispose()
    {
        this.listener.Stop();
        this.listener.Close();
    }
}

public class BlueIrisSessionLoginIntegrationTests
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
    public async Task GetSessionIdAsync_LogsIn_CachesSession_ThenRelogsInAfterInvalidate()
    {
        using FakeBlueIrisJsonServer server = new FakeBlueIrisJsonServer(GetFreePort());

        string sessionA = await BlueIrisSession.GetSessionIdAsync(server.Url, FakeBlueIrisJsonServer.TestUser, FakeBlueIrisJsonServer.TestPassword);
        Assert.False(string.IsNullOrEmpty(sessionA));
        Assert.Equal(1, server.LoginChallengesIssued);

        //a second call for the same server should reuse the cached session - no new request to the fake server
        string sessionAagain = await BlueIrisSession.GetSessionIdAsync(server.Url, FakeBlueIrisJsonServer.TestUser, FakeBlueIrisJsonServer.TestPassword);
        Assert.Equal(sessionA, sessionAagain);
        Assert.Equal(1, server.LoginChallengesIssued);

        string info = BlueIrisSession.GetLastLoginInfo(server.Url);
        Assert.Contains("TestNVR", info);
        Assert.Contains("5.7.9.9", info);

        //simulate the cached session having gone stale server-side: once invalidated, the next call must log in again
        BlueIrisSession.InvalidateSession(server.Url);
        string sessionB = await BlueIrisSession.GetSessionIdAsync(server.Url, FakeBlueIrisJsonServer.TestUser, FakeBlueIrisJsonServer.TestPassword);
        Assert.Equal(2, server.LoginChallengesIssued);
        Assert.NotEqual(sessionA, sessionB); //the fake server hands out a fresh challenge value for each full login
    }

    [Fact]
    public async Task GetSessionIdAsync_WrongPassword_Throws()
    {
        using FakeBlueIrisJsonServer server = new FakeBlueIrisJsonServer(GetFreePort()) { CorrectCredentials = false };

        await Assert.ThrowsAsync<Exception>(() => BlueIrisSession.GetSessionIdAsync(server.Url, FakeBlueIrisJsonServer.TestUser, "wrong-password"));
    }

    [Fact]
    public async Task TryRewriteUrlForSessionLoginAsync_RewritesBlueIrisHost_LeavesOtherHostsUntouched()
    {
        using FakeBlueIrisJsonServer server = new FakeBlueIrisJsonServer(GetFreePort());

        string originalBlueIrisServer = AppSettings.Settings.BlueIrisServer;
        string originalUser = AppSettings.Settings.DefaultUserName;
        string originalPwEnc = AppSettings.Settings.DefaultPasswordEncrypted;
        try
        {
            AppSettings.Settings.BlueIrisServer = "127.0.0.1";
            AppSettings.Settings.DefaultUserName = FakeBlueIrisJsonServer.TestUser;
            AppSettings.Settings.DefaultPasswordEncrypted = FakeBlueIrisJsonServer.TestPassword.Encrypt();

            string biUrl = $"{server.Url}/admin?camera=Front&trigger&user=admin&pw=hunter2&flagalert=2&memo=test";
            (string rewritten, string baseUrl) = await BlueIrisSession.TryRewriteUrlForSessionLoginAsync(biUrl);

            Assert.NotNull(baseUrl);
            Assert.DoesNotContain("pw=hunter2", rewritten);
            Assert.DoesNotContain("user=admin", rewritten);
            Assert.Contains("session=", rewritten);
            Assert.Contains("camera=Front", rewritten);
            Assert.Contains("flagalert=2", rewritten);

            //a URL to some other host must be returned completely unchanged
            string otherUrl = "http://example.com:1234/admin?camera=Front&user=admin&pw=hunter2";
            (string unaffected, string otherBase) = await BlueIrisSession.TryRewriteUrlForSessionLoginAsync(otherUrl);

            Assert.Null(otherBase);
            Assert.Equal(otherUrl, unaffected);
        }
        finally
        {
            AppSettings.Settings.BlueIrisServer = originalBlueIrisServer;
            AppSettings.Settings.DefaultUserName = originalUser;
            AppSettings.Settings.DefaultPasswordEncrypted = originalPwEnc;
        }
    }

    [Fact]
    public async Task TryRewriteUrlForSessionLoginAsync_LoginFails_FallsBackToOriginalUrlUnchanged()
    {
        using FakeBlueIrisJsonServer server = new FakeBlueIrisJsonServer(GetFreePort()) { CorrectCredentials = false };

        string originalBlueIrisServer = AppSettings.Settings.BlueIrisServer;
        string originalUser = AppSettings.Settings.DefaultUserName;
        string originalPwEnc = AppSettings.Settings.DefaultPasswordEncrypted;
        try
        {
            AppSettings.Settings.BlueIrisServer = "127.0.0.1";
            AppSettings.Settings.DefaultUserName = FakeBlueIrisJsonServer.TestUser;
            AppSettings.Settings.DefaultPasswordEncrypted = FakeBlueIrisJsonServer.TestPassword.Encrypt();

            string biUrl = $"{server.Url}/admin?camera=Front&trigger&user=admin&pw=hunter2";
            (string result, string baseUrl) = await BlueIrisSession.TryRewriteUrlForSessionLoginAsync(biUrl);

            //login was rejected - the original URL (still carrying user/pw) must be used so triggers keep working
            Assert.Null(baseUrl);
            Assert.Equal(biUrl, result);
        }
        finally
        {
            AppSettings.Settings.BlueIrisServer = originalBlueIrisServer;
            AppSettings.Settings.DefaultUserName = originalUser;
            AppSettings.Settings.DefaultPasswordEncrypted = originalPwEnc;
        }
    }
}
