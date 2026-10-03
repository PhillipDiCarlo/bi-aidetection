using System;
using System.Security.Cryptography;
using System.Threading;

using EmbedIO;

using static AITool.AITOOL;

namespace AITool.WebDashboard
{
    /// <summary>
    /// Starts/stops the embedded dashboard's HTTP listener (ROADMAP 2.7).
    /// Kept as a thin static wrapper around EmbedIO's WebServer so it can be started from
    /// InitializeBackend (headless/service friendly, no WinForms needed) as well as from the UI.
    /// </summary>
    public static class WebDashboardServer
    {
        private static readonly object StartStopLock = new object();
        private static WebServer server = null;
        private static CancellationTokenSource cts = null;

        public static bool IsRunning
        {
            get { return server != null && server.State == WebServerState.Listening; }
        }

        public static int Port { get; private set; } = 0;

        public static string GenerateToken()
        {
            byte[] bytes = new byte[32];

            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }

            //base64url-ish without padding so it drops cleanly into a query string or an Authorization header
            return Convert.ToBase64String(bytes).Replace("+", "").Replace("/", "").Replace("=", "");
        }

        public static void Start()
        {
            using var Trace = new Trace();  //This c# 8.0 using feature will auto dispose when the function is done.

            lock (StartStopLock)
            {
                if (IsRunning)
                    return;

                if (!AppSettings.Settings.WebDashboardEnabled)
                    return;

                try
                {
                    if (AppSettings.Settings.WebDashboardToken.IsEmpty())
                    {
                        AppSettings.Settings.WebDashboardToken = GenerateToken();
                        _ = AppSettings.SaveAsync(true);
                    }

                    int port = AppSettings.Settings.WebDashboardPort;
                    string host = AppSettings.Settings.WebDashboardAllowLan ? "*" : "127.0.0.1";
                    string prefix = $"http://{host}:{port}/";

                    cts = new CancellationTokenSource();

                    //EmbedIO's own ("EmbedIO" mode) listener is a plain socket listener, not Windows http.sys.
                    //Unlike System.Net.HttpListener it does NOT require admin rights or a 'netsh http add urlacl'
                    //reservation to bind a non-localhost prefix, so a normal user can enable LAN access.
                    server = new WebServer(o => o
                            .WithUrlPrefix(prefix)
                            .WithMode(HttpListenerMode.EmbedIO))
                        .OnAny(WebDashboardRequestHandler.HandleAsync);

                    server.Start(cts.Token);

                    Port = port;

                    Log($"Debug: Web dashboard started on {prefix} (LAN access {(AppSettings.Settings.WebDashboardAllowLan ? "ENABLED" : "disabled, localhost only")}).");
                }
                catch (Exception ex)
                {
                    Log($"Error: Could not start web dashboard on port {AppSettings.Settings.WebDashboardPort}: {ex.Msg()}");
                    CleanUp();
                }
            }
        }

        public static void Stop()
        {
            using var Trace = new Trace();  //This c# 8.0 using feature will auto dispose when the function is done.

            lock (StartStopLock)
            {
                bool wasRunning = server != null;

                try
                {
                    cts?.Cancel();
                    server?.Dispose();
                }
                catch (Exception ex)
                {
                    Log($"Debug: Error stopping web dashboard: {ex.Msg()}");
                }
                finally
                {
                    CleanUp();
                }

                if (wasRunning)
                    Log("Debug: Web dashboard stopped.");
            }
        }

        public static void Restart()
        {
            Stop();
            Start();
        }

        private static void CleanUp()
        {
            server = null;
            cts?.Dispose();
            cts = null;
            Port = 0;
        }
    }
}
