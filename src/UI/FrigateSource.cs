using Newtonsoft.Json;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

using static AITool.AITOOL;

namespace AITool
{
    //Payload shapes for Frigate's "<FrigateTopicPrefix>/events" MQTT topic.
    //Matches Frigate 0.13 - 0.18 (https://docs.frigate.video/integrations/mqtt): type is "new"/"update"/"end",
    //before/after are the object's state before/after the change.  We only need a handful of the fields Frigate sends.
    public class FrigateEventMessage
    {
        public string Type { get; set; } = "";
        public FrigateEventObject Before { get; set; } = null;
        public FrigateEventObject After { get; set; } = null;
    }

    public class FrigateEventObject
    {
        public string Id { get; set; } = "";
        public string Camera { get; set; } = "";
        public string Label { get; set; } = "";
        public double Score { get; set; } = 0;
        [JsonProperty("top_score")]
        public double TopScore { get; set; } = 0;
        [JsonProperty("has_snapshot")]
        public bool HasSnapshot { get; set; } = false;
        [JsonProperty("has_clip")]
        public bool HasClip { get; set; } = false;
        [JsonProperty("start_time")]
        public double StartTime { get; set; } = 0;
        [JsonProperty("false_positive")]
        public bool FalsePositive { get; set; } = false;
    }

    //Lets AITool use Frigate (https://frigate.video) as an image source alongside the Blue Iris JPEG folder: subscribes to
    //Frigate's MQTT event topic (using the same broker as the existing mqtt_* settings), maps the Frigate camera to an
    //AITool Camera, downloads the event's snapshot over Frigate's HTTP API, and feeds the same image queue Blue Iris does.
    //See docs/frigate.md.
    public class FrigateSource
    {
        //production singleton - Start()/Stop() manage this.  Tests construct their own FrigateSource and call
        //ProcessEventJsonAsync()/MapCamera()/etc directly instead, with the Func/Action fields below overridden.
        public static FrigateSource Instance = null;

        //All of these are overridable so tests can exercise the logic without touching AppSettings/AITOOL's global queue.
        public Func<string> GetFrigateUrl = () => AppSettings.Settings.FrigateUrl;
        public Func<string> GetTopicPrefix = () => AppSettings.Settings.FrigateTopicPrefix;
        public Func<string> GetCameraFilter = () => AppSettings.Settings.FrigateCameras;
        public Func<string> GetLabelFilter = () => AppSettings.Settings.FrigateLabels;
        public Func<string> GetApiKey = () => AppSettings.Settings.FrigateApiKey;
        public Func<string> GetSnapshotFolderSetting = () => AppSettings.Settings.FrigateSnapshotFolder;
        public Func<List<Camera>> GetCameras = () => AppSettings.Settings.CameraList;
        public Action<string, Camera> EnqueueAction = (path, cam) => AITOOL.AddImageToQueue(path, cam);
        public HttpClient Http = new HttpClient();

        //event ids we've already downloaded/enqueued, so a later "update"/"end" message for the same event doesn't re-fetch it.
        //Not persisted across restarts - a handful of duplicate downloads right after a restart is harmless.
        private readonly ConcurrentDictionary<string, DateTime> ProcessedEventIds = new ConcurrentDictionary<string, DateTime>();

        //so we only log "no camera matches" once per Frigate camera name, not once per event
        private readonly ConcurrentDictionary<string, bool> WarnedUnmatchedCameras = new ConcurrentDictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        public string SubscribedTopic { get; private set; } = "";

        public static async Task StartAsync()
        {
            using var Trace = new Trace();  //This c# 8.0 using feature will auto dispose when the function is done.

            try
            {
                if (!AppSettings.Settings.FrigateEnabled)
                    return;

                if (Instance == null)
                    Instance = new FrigateSource();

                await Instance.StartInstanceAsync();
            }
            catch (Exception ex)
            {
                Log($"Error: FrigateSource: Could not start: {ex.Msg()}");
            }
        }

        public static void Stop()
        {
            using var Trace = new Trace();  //This c# 8.0 using feature will auto dispose when the function is done.

            try
            {
                if (Instance != null && Instance.SubscribedTopic.IsNotEmpty())
                {
                    //fire and forget - we dont want shutdown/settings-save to block on an MQTT round trip
                    _ = AITOOL.mqttClient.UnsubscribeAsync(Instance.SubscribedTopic);
                }
            }
            catch (Exception ex)
            {
                Log($"Debug: FrigateSource: Stop failed: {ex.Msg()}");
            }
            finally
            {
                Instance = null;
            }
        }

        //called at startup and whenever the user saves settings - cheap no-op if Frigate is (still) disabled.
        public static async Task RestartAsync()
        {
            Stop();
            await StartAsync();
        }

        private async Task StartInstanceAsync()
        {
            string folder = this.GetSnapshotFolder();
            PurgeOldFiles(folder);

            this.SubscribedTopic = this.GetTopicPrefix().Trim().TrimEnd('/') + "/events";

            await AITOOL.mqttClient.SubscribeAsync(this.SubscribedTopic, this.OnMqttMessageAsync);

            Log($"Debug: FrigateSource: Subscribed to '{this.SubscribedTopic}', snapshots will be saved to '{folder}'.");
        }

        public string GetSnapshotFolder()
        {
            string folder = this.GetSnapshotFolderSetting();
            if (folder.IsEmpty())
                folder = Path.Combine(Global.GetTempFolder(), "frigate");

            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            return folder;
        }

        //matches the MqttMessageHandler delegate signature in MQTTClient.cs
        public async Task OnMqttMessageAsync(string Topic, byte[] Payload)
        {
            try
            {
                string json = Encoding.UTF8.GetString(Payload);
                await this.ProcessEventJsonAsync(json);
            }
            catch (Exception ex)
            {
                Log($"Error: FrigateSource: Could not process MQTT message on '{Topic}': {ex.Msg()}");
            }
        }

        //Core event handling, kept free of any direct MQTT/AppSettings calls (besides the overridable Func/Action fields above)
        //so it can be unit tested with a fake HTTP endpoint and an injected enqueue action.  Returns true if an image was enqueued.
        public async Task<bool> ProcessEventJsonAsync(string Json)
        {
            if (!TryParseEvent(Json, out FrigateEventMessage msg))
                return false;

            FrigateEventObject obj = msg.After ?? msg.Before;
            if (obj == null || obj.Id.IsEmpty())
                return false;

            if (!ShouldFetchSnapshot(msg))
                return false;

            if (!PassesFilter(obj.Camera, this.GetCameraFilter()))
                return false;

            if (!PassesFilter(obj.Label, this.GetLabelFilter()))
                return false;

            if (!this.TryMarkProcessed(obj.Id))
                return false; //already handled this event id

            Camera matched = MapCamera(obj.Camera, this.GetCameras());
            if (matched == null)
            {
                if (this.WarnedUnmatchedCameras.TryAdd(obj.Camera, true))
                    Log($"Debug: FrigateSource: No AITool camera matches Frigate camera '{obj.Camera}' (checked BICamName, Name, Prefix). Skipping. Add/rename a camera to match it if you want this camera's events.");

                return false;
            }

            byte[] bytes;
            try
            {
                bytes = await this.DownloadSnapshotAsync(obj.Id);
            }
            catch (Exception ex)
            {
                Log($"Error: FrigateSource: Could not download snapshot for event '{obj.Id}' (camera '{obj.Camera}'): {ex.Msg()}");
                return false;
            }

            if (bytes == null || bytes.Length == 0)
                return false;

            string folder = this.GetSnapshotFolder();
            string filename = BuildFileName(matched, obj.Camera, obj.Id, DateTime.Now);
            string path = Path.Combine(folder, filename);

            await File.WriteAllBytesAsync(path, bytes);

            Log($"Debug: FrigateSource: Saved snapshot for Frigate camera '{obj.Camera}' event '{obj.Id}' -> AITool camera '{matched.Name}': {path}");

            this.EnqueueAction(path, matched);

            return true;
        }

        public async Task<byte[]> DownloadSnapshotAsync(string EventId)
        {
            string url = this.GetFrigateUrl().TrimEnd('/') + $"/api/events/{EventId}/snapshot.jpg";

            using HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, url);

            string apikey = this.GetApiKey();
            if (apikey.IsNotEmpty())
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apikey);

            using HttpResponseMessage resp = await this.Http.SendAsync(req);
            resp.EnsureSuccessStatusCode();

            return await resp.Content.ReadAsByteArrayAsync();
        }

        //Fetches <FrigateUrl>/api/version - used by the "Test" button in Frm_FrigateSettings.
        public async Task<string> GetVersionAsync()
        {
            string url = this.GetFrigateUrl().TrimEnd('/') + "/api/version";

            using HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, url);

            string apikey = this.GetApiKey();
            if (apikey.IsNotEmpty())
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apikey);

            using HttpResponseMessage resp = await this.Http.SendAsync(req);
            resp.EnsureSuccessStatusCode();

            return (await resp.Content.ReadAsStringAsync()).Trim();
        }

        //---------- pure/testable helpers ----------

        public static bool TryParseEvent(string Json, out FrigateEventMessage Message)
        {
            Message = null;
            try
            {
                if (Json.IsEmpty())
                    return false;

                Message = JsonConvert.DeserializeObject<FrigateEventMessage>(Json);

                return Message != null;
            }
            catch
            {
                return false;
            }
        }

        //We only want to fetch a snapshot once per event: on the "new" message (the normal case - Frigate usually already has
        //has_snapshot=true by the time "new" is published), or, failing that, on the first "update" where has_snapshot flips
        //from false to true (covers the rare case where the snapshot isn't ready yet on "new").  We deliberately ignore "end"
        //and any later "update" so a long-tracked object (e.g. someone standing in frame) isn't re-downloaded/re-queued repeatedly.
        public static bool ShouldFetchSnapshot(FrigateEventMessage Message)
        {
            FrigateEventObject obj = Message?.After ?? Message?.Before;
            if (obj == null || !obj.HasSnapshot)
                return false;

            if (Message.Type.EqualsIgnoreCase("new"))
                return true;

            bool hadSnapshotBefore = Message.Before?.HasSnapshot ?? false;
            if (Message.Type.EqualsIgnoreCase("update") && !hadSnapshotBefore)
                return true;

            return false;
        }

        //empty filter = accept everything.  Filter is a comma separated list, case insensitive, matched by exact (trimmed) value.
        public static bool PassesFilter(string Value, string CsvFilter)
        {
            if (CsvFilter.IsEmpty())
                return true;

            if (Value.IsEmpty())
                return false;

            List<string> allowed = CsvFilter.SplitStr(",");
            return allowed.Any(a => a.EqualsIgnoreCase(Value));
        }

        //Maps a Frigate camera name to an AITool Camera, preferring (in order) BICamName, Name, then Prefix - all exact,
        //case-insensitive matches.
        public static Camera MapCamera(string FrigateCamera, IEnumerable<Camera> Cameras)
        {
            if (FrigateCamera.IsEmpty() || Cameras == null)
                return null;

            Camera match = Cameras.FirstOrDefault(c => c != null && FrigateCamera.EqualsIgnoreCase(c.BICamName));

            if (match == null)
                match = Cameras.FirstOrDefault(c => c != null && FrigateCamera.EqualsIgnoreCase(c.Name));

            if (match == null)
                match = Cameras.FirstOrDefault(c => c != null && c.Prefix.IsNotEmpty() && FrigateCamera.EqualsIgnoreCase(c.Prefix));

            return match;
        }

        //Builds the saved snapshot filename.  Uses the matched AITool camera's own Prefix (falling back to its Name, then the
        //raw Frigate camera name) as the leading token, so AITOOL.GetCamera(path) - which is called again every time the image
        //is dequeued for processing, and only matches on Prefix/input_path, not BICamName/Name - resolves back to the SAME
        //camera. A camera with no Prefix configured will fall through to GetCamera's "default camera" fallback at that point,
        //same as it would for a Blue Iris folder-based camera with no Prefix; set a Prefix on the camera to avoid that.
        public static string BuildFileName(Camera MatchedCamera, string FrigateCamera, string EventId, DateTime Timestamp)
        {
            string token = FrigateCamera;

            if (MatchedCamera != null)
                token = MatchedCamera.Prefix.IsNotEmpty() ? MatchedCamera.Prefix.Trim() : MatchedCamera.Name;

            token = SanitizeFileNameToken(token);

            return $"{token}.{EventId}.{Timestamp:yyyyMMdd_HHmmssfff}.jpg";
        }

        private static string SanitizeFileNameToken(string Value)
        {
            if (Value.IsEmpty())
                return "frigate";

            char[] invalid = Path.GetInvalidFileNameChars();
            StringBuilder sb = new StringBuilder(Value.Length);

            foreach (char c in Value)
                sb.Append(c == '.' || Array.IndexOf(invalid, c) >= 0 ? '_' : c);

            return sb.ToString();
        }

        private bool TryMarkProcessed(string EventId)
        {
            //keep this from growing forever during a long-running session
            if (this.ProcessedEventIds.Count > 2000)
            {
                DateTime cutoff = DateTime.Now.AddHours(-1);
                foreach (string key in this.ProcessedEventIds.Where(kv => kv.Value < cutoff).Select(kv => kv.Key).ToList())
                    this.ProcessedEventIds.TryRemove(key, out _);
            }

            return this.ProcessedEventIds.TryAdd(EventId, DateTime.Now);
        }

        private static void PurgeOldFiles(string Folder)
        {
            try
            {
                if (!Directory.Exists(Folder))
                    return;

                foreach (string file in Directory.GetFiles(Folder, "*.jpg"))
                {
                    try
                    {
                        if ((DateTime.Now - File.GetLastWriteTime(file)).TotalDays > 1)
                            File.Delete(file);
                    }
                    catch
                    {
                        //best effort - a file in use will get picked up on the next purge
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Debug: FrigateSource: Could not purge old files in '{Folder}': {ex.Msg()}");
            }
        }
    }
}
