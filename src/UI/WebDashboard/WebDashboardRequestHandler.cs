using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using EmbedIO;

using Newtonsoft.Json;

using static AITool.AITOOL;

namespace AITool.WebDashboard
{
    /// <summary>
    /// All request routing/auth/hardening for the embedded dashboard lives here so it can be unit tested
    /// (by actually starting WebDashboardServer on a loopback port) without touching the WinForms UI.
    /// </summary>
    public static class WebDashboardRequestHandler
    {
        private const int MaxRequestBodyBytes = 64 * 1024;  //pause/resume bodies are tiny - this is already generous
        private const int DefaultHistoryLimit = 50;
        private const int MaxHistoryLimit = 500;

        public static async Task HandleAsync(IHttpContext context)
        {
            using var Trace = new Trace();  //This c# 8.0 using feature will auto dispose when the function is done.

            try
            {
                if (!IsAuthorized(context))
                {
                    await SendJsonAsync(context, 401, new { error = "Unauthorized" });
                    return;
                }

                string path = context.RequestedPath;
                HttpVerbs verb = context.Request.HttpVerb;

                if (path.IsEmpty() || path == "/" || path.EqualsIgnoreCase("/index.html"))
                {
                    await context.SendStringAsync(WebDashboardPage.Html, "text/html", Encoding.UTF8);
                }
                else if (path.EqualsIgnoreCase("/api/status"))
                {
                    if (!RequireMethod(verb, HttpVerbs.Get, out var err)) { await SendJsonAsync(context, err, new { error = "Method not allowed" }); return; }
                    await SendJsonAsync(context, 200, BuildStatus());
                }
                else if (path.EqualsIgnoreCase("/api/servers"))
                {
                    if (!RequireMethod(verb, HttpVerbs.Get, out var err)) { await SendJsonAsync(context, err, new { error = "Method not allowed" }); return; }
                    await SendJsonAsync(context, 200, BuildServers());
                }
                else if (path.EqualsIgnoreCase("/api/history"))
                {
                    if (!RequireMethod(verb, HttpVerbs.Get, out var err)) { await SendJsonAsync(context, err, new { error = "Method not allowed" }); return; }

                    int limit = DefaultHistoryLimit;
                    int.TryParse(context.Request.QueryString["limit"], out limit);
                    if (limit <= 0)
                        limit = DefaultHistoryLimit;
                    if (limit > MaxHistoryLimit)
                        limit = MaxHistoryLimit;

                    await SendJsonAsync(context, 200, BuildHistory(limit));
                }
                else if (path.EqualsIgnoreCase("/api/image"))
                {
                    if (!RequireMethod(verb, HttpVerbs.Get, out var err)) { await SendJsonAsync(context, err, new { error = "Method not allowed" }); return; }
                    await HandleImageAsync(context);
                }
                else if (path.EqualsIgnoreCase("/api/pause"))
                {
                    if (!RequireMethod(verb, HttpVerbs.Post, out var err)) { await SendJsonAsync(context, err, new { error = "Method not allowed" }); return; }
                    await HandlePauseResumeAsync(context, true);
                }
                else if (path.EqualsIgnoreCase("/api/resume"))
                {
                    if (!RequireMethod(verb, HttpVerbs.Post, out var err)) { await SendJsonAsync(context, err, new { error = "Method not allowed" }); return; }
                    await HandlePauseResumeAsync(context, false);
                }
                else
                {
                    await SendJsonAsync(context, 404, new { error = "Not found" });
                }
            }
            catch (Exception ex)
            {
                //never leak exception details (stack traces, file paths) to a remote client
                Log($"Error: Web dashboard request for '{context.RequestedPath}' failed: {ex.Msg()}");

                try
                {
                    await SendJsonAsync(context, 500, new { error = "Internal server error" });
                }
                catch
                {
                    //response may already be partially written (eg. mid-image-stream) - nothing more we can do
                }
            }
        }

        private static bool RequireMethod(HttpVerbs actual, HttpVerbs required, out int errorStatusCode)
        {
            errorStatusCode = 405;
            return actual == required;
        }

        private static bool IsAuthorized(IHttpContext context)
        {
            string expected = AppSettings.Settings.WebDashboardToken;

            //fail closed - if no token has been configured yet, nobody gets in (Start() always generates one first)
            if (expected.IsEmpty())
                return false;

            string provided = "";

            string auth = context.Request.Headers["Authorization"];
            if (auth.IsNotEmpty() && auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                provided = auth.Substring("Bearer ".Length).Trim();

            if (provided.IsEmpty())
                provided = context.Request.QueryString["token"] ?? "";

            byte[] expectedBytes = Encoding.UTF8.GetBytes(expected);
            byte[] providedBytes = Encoding.UTF8.GetBytes(provided);

            if (expectedBytes.Length != providedBytes.Length)
                return false;

            return CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
        }

        private static Task SendJsonAsync(IHttpContext context, int statusCode, object data)
        {
            context.Response.StatusCode = statusCode;
            string json = JsonConvert.SerializeObject(data);
            return context.SendStringAsync(json, "application/json", Encoding.UTF8);
        }

        private static async Task<string> ReadLimitedBodyAsync(IHttpContext context, int maxBytes)
        {
            if (context.Request.ContentLength64 > maxBytes)
                return null;

            using MemoryStream ms = new MemoryStream();
            byte[] buffer = new byte[8192];
            int total = 0;
            int read;

            while ((read = await context.Request.InputStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                total += read;
                if (total > maxBytes)
                    return null;

                ms.Write(buffer, 0, read);
            }

            return Encoding.UTF8.GetString(ms.ToArray());
        }

        private static DashboardStatusModel BuildStatus()
        {
            DashboardStatusModel m = new DashboardStatusModel();

            m.Version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "";
            m.UptimeSeconds = (DateTime.Now - Process.GetCurrentProcess().StartTime).TotalSeconds;
            m.ImageQueueLength = AITOOL.ImageProcessQueue.Count;
            m.ActionQueueLength = AITOOL.TriggerActionQueue != null ? (int)AITOOL.TriggerActionQueue.Count : 0;

            DateTime lastDetection = DateTime.MinValue;

            foreach (Camera cam in AppSettings.Settings.CameraList)
            {
                DateTime camTrigger = cam.last_trigger_time;

                if (camTrigger > lastDetection)
                    lastDetection = camTrigger;

                m.Cameras.Add(new DashboardCameraModel
                {
                    Name = cam.Name,
                    Enabled = cam.enabled,
                    Paused = cam.Paused,
                    ResumeTime = cam.Paused ? (DateTime?)cam.ResumeTime : null,
                    LastTriggerTime = camTrigger == DateTime.MinValue ? null : (DateTime?)camTrigger,
                    StatsAlerts = cam.stats_alerts,
                    StatsFalseAlerts = cam.stats_false_alerts,
                    StatsIrrelevantAlerts = cam.stats_irrelevant_alerts,
                    StatsSkippedImages = cam.stats_skipped_images,
                });
            }

            m.LastDetectionTime = lastDetection == DateTime.MinValue ? null : (DateTime?)lastDetection;

            return m;
        }

        private static List<DashboardServerModel> BuildServers()
        {
            List<DashboardServerModel> ret = new List<DashboardServerModel>();

            foreach (ClsURLItem url in AppSettings.Settings.AIURLList)
            {
                ret.Add(new DashboardServerModel
                {
                    Name = url.Name,
                    Type = url.Type.ToString(),
                    Enabled = url.Enabled,
                    Online = url.IsOnline,
                    InUse = url.InUse,
                    LastResultMessage = url.LastResultMessage,
                    AvgTimeMS = url.AvgTimeMS,
                    ErrCount = url.ErrCount,
                    ErrDisabled = url.ErrDisabled,
                });
            }

            return ret;
        }

        private static List<DashboardHistoryItemModel> BuildHistory(int limit)
        {
            List<DashboardHistoryItemModel> ret = new List<DashboardHistoryItemModel>();

            if (AITOOL.HistoryDB == null)
                return ret;

            List<History> all = AITOOL.HistoryDB.GetAllValues();  //ascending by date
            IEnumerable<History> recent = all.Skip(Math.Max(0, all.Count - limit)).Reverse();  //most recent first

            string token = Uri.EscapeDataString(AppSettings.Settings.WebDashboardToken);

            foreach (History hist in recent)
            {
                string file = Uri.EscapeDataString(hist.Filename);

                ret.Add(new DashboardHistoryItemModel
                {
                    Camera = hist.Camera,
                    Date = hist.Date,
                    Detections = hist.Detections,
                    Success = hist.Success,
                    ImageUrl = $"/api/image?file={file}&token={token}",
                    AnnotatedImageUrl = $"/api/image?file={file}&annotated=1&token={token}",
                });
            }

            return ret;
        }

        private static async Task HandleImageAsync(IHttpContext context)
        {
            string file = context.Request.QueryString["file"] ?? "";
            bool annotated = context.Request.QueryString["annotated"] == "1";

            //defense in depth - the HistoryDic lookup below already refuses anything that isn't a known
            //history entry, but reject obvious traversal attempts outright before touching the filesystem.
            if (file.IsEmpty() || file.Contains(".."))
            {
                await SendJsonAsync(context, 400, new { error = "Invalid file" });
                return;
            }

            History hist;

            if (AITOOL.HistoryDB == null || !AITOOL.HistoryDB.HistoryDic.TryGetValue(file.ToLower(), out hist))
            {
                await SendJsonAsync(context, 404, new { error = "Not found" });
                return;
            }

            if (!File.Exists(hist.Filename))
            {
                await SendJsonAsync(context, 404, new { error = "Not found" });
                return;
            }

            byte[] bytes;

            try
            {
                bytes = annotated ? RenderAnnotatedImage(hist) : File.ReadAllBytes(hist.Filename);
            }
            catch (Exception ex)
            {
                Log($"Error: Web dashboard could not read/render image '{hist.Filename}': {ex.Msg()}");
                await SendJsonAsync(context, 500, new { error = "Could not read image" });
                return;
            }

            context.Response.ContentType = "image/jpeg";
            context.Response.ContentLength64 = bytes.Length;
            await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
        }

        private static byte[] RenderAnnotatedImage(History hist)
        {
            byte[] raw = File.ReadAllBytes(hist.Filename);

            using MemoryStream input = new MemoryStream(raw);
            using Bitmap bmp = new Bitmap(input);
            using Graphics g = Graphics.FromImage(bmp);

            List<ClsPrediction> predictions = hist.Predictions();

            //draw in reverse, same order the desktop History tab uses, so the most important box ends up on top
            for (int i = predictions.Count - 1; i >= 0; i--)
            {
                ClsPrediction pred = predictions[i];
                if (pred != null)
                    AITOOL.DrawAnnotation(g, pred, bmp.Width, bmp.Height);
            }

            using MemoryStream output = new MemoryStream();
            bmp.Save(output, ImageFormat.Jpeg);
            return output.ToArray();
        }

        private static async Task HandlePauseResumeAsync(IHttpContext context, bool pause)
        {
            string body = await ReadLimitedBodyAsync(context, MaxRequestBodyBytes);

            if (body == null)
            {
                await SendJsonAsync(context, 413, new { error = "Request body too large" });
                return;
            }

            DashboardPauseRequest req;

            try
            {
                req = body.IsEmpty() ? new DashboardPauseRequest() : (JsonConvert.DeserializeObject<DashboardPauseRequest>(body) ?? new DashboardPauseRequest());
            }
            catch (Exception)
            {
                await SendJsonAsync(context, 400, new { error = "Invalid JSON body" });
                return;
            }

            string camName = (req.Camera ?? "").Trim();

            List<Camera> targets;

            if (camName.IsEmpty() || camName.EqualsIgnoreCase("all") || camName.EqualsIgnoreCase("All Cameras"))
            {
                targets = AppSettings.Settings.CameraList;
            }
            else
            {
                Camera cam = AITOOL.GetCamera(camName, false);

                if (cam == null)
                {
                    await SendJsonAsync(context, 404, new { error = "Camera not found" });
                    return;
                }

                targets = new List<Camera> { cam };
            }

            foreach (Camera cam in targets)
            {
                if (req.Minutes.HasValue && req.Minutes.Value > 0)
                    cam.PauseMinutes = req.Minutes.Value;

                if (pause)
                {
                    if (!cam.Paused)
                        cam.Pause();
                }
                else
                {
                    if (cam.Paused)
                        cam.Resume();
                }
            }

            await SendJsonAsync(context, 200, new { ok = true, cameras = targets.Select(c => c.Name).ToList() });
        }
    }
}
