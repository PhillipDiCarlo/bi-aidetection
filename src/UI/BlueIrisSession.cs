using Newtonsoft.Json.Linq;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using static AITool.AITOOL;

namespace AITool
{
    /// <summary>
    /// Client for Blue Iris's "/json" API, used ONLY to obtain a session id so trigger URLs can use
    /// "&amp;session=..." instead of putting the plaintext password in the query string (ROADMAP.md 1.4).
    ///
    /// Protocol (two-step challenge/response), cross-checked against several third-party clients -
    /// NOT yet verified against a live Blue Iris server, see ROADMAP.md 1.4 and AppSettings.BlueIrisUseSessionLogin:
    ///   1) POST {"cmd":"login"} to "{server}/json"            -> {"result":"fail","session":"&lt;session&gt;"}
    ///      (the "fail" here is normal/expected - it's just the challenge handshake, not an error)
    ///   2) POST {"cmd":"login","session":"&lt;session&gt;","response":"&lt;md5&gt;"} to "{server}/json"
    ///      where md5 = MD5_hex_lower("{user}:{session}:{password}")
    ///                                                           -> {"result":"success", "data": {...}} on success
    /// The session id returned by step 1 is reused for subsequent calls (it does not change in step 2's response).
    ///
    /// Sources:
    ///  - https://ipcamtalk.com/threads/json-interface.21491/ (community mirror of Blue Iris's own JSON interface help page)
    ///  - https://www.houselogix.com/docs/blue-iris/BlueIris/json.htm
    ///  - https://github.com/magapp/blueiriscmd/blob/master/blueiris.py (reference implementation of the handshake)
    ///
    /// NOT verified against a live server (see ROADMAP.md 1.4):
    ///  - Whether "/admin?...&amp;session=S" is actually accepted by the trigger/flagalert admin endpoint in place
    ///    of user=/pw= (this is a DIFFERENT code path in Blue Iris than the JSON API, and some forum reports
    ///    suggest the web server's "Use secure session keys and login page" option changes how /admin
    ///    authenticates). If this turns out to be unsupported, AppSettings.BlueIrisUseSessionLogin should stay off.
    ///  - The exact shape of the "data" object (field names like "system name"/"version" are best-effort).
    ///  - What an expired/invalid session looks like from Blue Iris's side.
    /// </summary>
    public static class BlueIrisSession
    {
        private class CachedSession
        {
            public string SessionId = "";
            public string Info = ""; //best-effort "<system name> <version>" from the login response, for Debug log / Test Login display only
            public DateTime CreatedUtc = DateTime.MinValue;
        }

        //one cached session per BlueIris server base url (e.g. "http://192.168.1.50:81"), thread-safe
        private static readonly ConcurrentDictionary<string, CachedSession> Cache = new ConcurrentDictionary<string, CachedSession>(StringComparer.OrdinalIgnoreCase);

        //one lock per server so concurrent trigger calls don't all try to log in to the same server at once
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks = new ConcurrentDictionary<string, SemaphoreSlim>(StringComparer.OrdinalIgnoreCase);

        private static readonly HttpClient Http = new HttpClient();

        private static string NormalizeBaseUrl(string ServerBaseUrl)
        {
            return (ServerBaseUrl ?? "").Trim().TrimEnd('/');
        }

        /// <summary>Returns true if Host matches the configured BlueIris server (AppSettings.Settings.BlueIrisServer, or - if a registry read succeeded - AITOOL.BlueIrisInfo). Used by UrlAction to decide which outgoing URLs to rewrite.</summary>
        public static bool IsBlueIrisHost(string Host)
        {
            if (Host.IsEmpty())
                return false;

            string host = NormalizeHostForCompare(Host);

            string configured = NormalizeHostForCompare(AppSettings.Settings.BlueIrisServer);
            if (configured.IsNotEmpty() && host.EqualsIgnoreCase(configured))
                return true;

            //BlueIrisInfo is populated from the registry and may know the server by a different name/IP than what's configured
            if (AITOOL.BlueIrisInfo != null && AITOOL.BlueIrisInfo.Result == BlueIrisResult.Valid)
            {
                if (host.EqualsIgnoreCase(NormalizeHostForCompare(AITOOL.BlueIrisInfo.ServerName)))
                    return true;

                if (Uri.TryCreate(AITOOL.BlueIrisInfo.URL, UriKind.Absolute, out Uri biuri) && host.EqualsIgnoreCase(NormalizeHostForCompare(biuri.Host)))
                    return true;
            }

            return false;
        }

        private static string NormalizeHostForCompare(string Host)
        {
            if (Host.IsEmpty())
                return "";

            //treat localhost and the loopback IP as the same host for matching purposes
            if (Host.EqualsIgnoreCase("localhost"))
                return "127.0.0.1";

            return Host.Trim();
        }

        /// <summary>
        /// Rewrites one trigger/cancel URL (after [Username]/[Password]/etc. have already been substituted) to use
        /// "&amp;session=..." instead of "&amp;user=...&amp;pw=...", logging in (or reusing a cached session) as needed.
        /// Returns the original Url unchanged, with BaseUrl null, if Url isn't the BlueIris host or login fails -
        /// callers should keep using the un-rewritten Url in that case so triggers keep working.
        /// </summary>
        public static async Task<(string Url, string BaseUrl)> TryRewriteUrlForSessionLoginAsync(string Url)
        {
            using var Trace = new Trace();

            if (!Uri.TryCreate(Url, UriKind.Absolute, out Uri uri) || !IsBlueIrisHost(uri.Host))
                return (Url, null);

            string baseurl = $"{uri.Scheme}://{uri.Host}:{uri.Port}";

            try
            {
                string username = AppSettings.Settings.DefaultUserName;
                string password = AppSettings.Settings.DefaultPasswordEncrypted.Decrypt();

                string sessionid = await GetSessionIdAsync(baseurl, username, password);

                return (RewriteUrlWithSession(Url, sessionid), baseurl);
            }
            catch (Exception ex)
            {
                Log($"Warn: BlueIris session login failed for '{baseurl}', falling back to the original trigger URL (user/pw in the query string): {ex.Msg()}");
                return (Url, null);
            }
        }

        /// <summary>
        /// Pure query-string rewrite: drops "user"/"pw" (case-insensitive) and appends "session=...", leaving every
        /// other parameter byte-for-byte untouched (no re-encoding - memo/jpeg escaping must survive exactly).
        /// </summary>
        public static string RewriteUrlWithSession(string Url, string SessionId)
        {
            int qmark = Url.IndexOf('?');
            string baseurl = qmark < 0 ? Url : Url.Substring(0, qmark);
            string query = qmark < 0 ? "" : Url.Substring(qmark + 1);

            List<string> keep = new List<string>();
            if (query.IsNotEmpty())
            {
                foreach (string segment in query.Split('&'))
                {
                    if (segment.IsEmpty())
                        continue;

                    int eq = segment.IndexOf('=');
                    string key = eq < 0 ? segment : segment.Substring(0, eq);

                    if (key.EqualsIgnoreCase("user") || key.EqualsIgnoreCase("pw"))
                        continue; //drop the plaintext credentials

                    keep.Add(segment);
                }
            }

            keep.Add("session=" + Uri.EscapeDataString(SessionId ?? ""));

            return baseurl + "?" + string.Join("&", keep);
        }

        /// <summary>Returns a valid session id for ServerBaseUrl, logging in or reusing a cached session as needed. Throws on failure.</summary>
        public static async Task<string> GetSessionIdAsync(string ServerBaseUrl, string UserName, string Password)
        {
            using var Trace = new Trace();

            string baseurl = NormalizeBaseUrl(ServerBaseUrl);

            if (Cache.TryGetValue(baseurl, out CachedSession existing) && existing.SessionId.IsNotEmpty())
                return existing.SessionId;

            SemaphoreSlim sem = Locks.GetOrAdd(baseurl, _ => new SemaphoreSlim(1, 1));
            await sem.WaitAsync();
            try
            {
                //another thread may have already logged in while we were waiting for the lock
                if (Cache.TryGetValue(baseurl, out existing) && existing.SessionId.IsNotEmpty())
                    return existing.SessionId;

                (string sessionid, string info) = await LoginAsync(baseurl, UserName, Password);

                Cache[baseurl] = new CachedSession() { SessionId = sessionid, Info = info, CreatedUtc = DateTime.UtcNow };

                return sessionid;
            }
            finally
            {
                sem.Release();
            }
        }

        /// <summary>Forces the next GetSessionIdAsync()/TryRewriteUrlForSessionLoginAsync() call for this server to log in again (e.g. after a call indicates the cached session is no longer valid).</summary>
        public static void InvalidateSession(string ServerBaseUrl)
        {
            Cache.TryRemove(NormalizeBaseUrl(ServerBaseUrl), out _);
        }

        /// <summary>Best-effort "<system name> <version>" string from the last successful login for ServerBaseUrl, or "" if unknown. For display (e.g. the Shell "Test login" button) and Debug logging only.</summary>
        public static string GetLastLoginInfo(string ServerBaseUrl)
        {
            return Cache.TryGetValue(NormalizeBaseUrl(ServerBaseUrl), out CachedSession cs) ? cs.Info : "";
        }

        private static async Task<(string SessionId, string Info)> LoginAsync(string BaseUrl, string UserName, string Password)
        {
            using var Trace = new Trace();

            string jsonurl = BaseUrl + "/json";

            //Step 1: ask for a challenge session id (sent unauthenticated)
            JObject resp1 = await PostAsync(jsonurl, new JObject { ["cmd"] = "login" });

            string challenge = (string)resp1["session"];
            if (challenge.IsEmpty())
                throw new Exception($"BlueIris '{jsonurl}' did not return a 'session' challenge value.");

            //Step 2: respond with MD5("user:session:password") - never log UserName/Password/this hash
            string response = ComputeLoginResponse(UserName, challenge, Password);

            JObject resp2 = await PostAsync(jsonurl, new JObject { ["cmd"] = "login", ["session"] = challenge, ["response"] = response });

            string result = (string)resp2["result"];
            if (!string.Equals(result, "success", StringComparison.OrdinalIgnoreCase))
                throw new Exception($"BlueIris login to '{BaseUrl}' was rejected (result='{result ?? "(none)"}').  Check Settings > Default username/password.");

            string info = ExtractLoginInfo(resp2);

            if (info.IsNotEmpty())
                Log($"Debug: BlueIris session login succeeded for '{BaseUrl}' ({info}).");
            else
                Log($"Debug: BlueIris session login succeeded for '{BaseUrl}'.");

            return (challenge, info);
        }

        //Field names here aren't confirmed against a live server (see class-level remarks) - look in a few plausible
        //places and never throw if the shape doesn't match what we expect.
        private static string ExtractLoginInfo(JObject LoginResponse)
        {
            try
            {
                JToken data = LoginResponse["data"];
                string systemname = (string)data?["system name"] ?? (string)LoginResponse["system name"];
                string version = (string)data?["version"] ?? (string)LoginResponse["version"];

                if (systemname.IsEmpty() && version.IsEmpty())
                    return "";

                return $"{systemname} {version}".Trim();
            }
            catch (Exception)
            {
                return "";
            }
        }

        private static async Task<JObject> PostAsync(string JsonUrl, JObject Body)
        {
            using StringContent content = new StringContent(Body.ToString(Newtonsoft.Json.Formatting.None), Encoding.UTF8, "application/json");
            using HttpResponseMessage resp = await Http.PostAsync(JsonUrl, content).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            string respbody = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            return JObject.Parse(respbody);
        }

        /// <summary>MD5("user:session:password") as lower-case hex - Blue Iris's documented JSON login handshake. Never log UserName/SessionId/Password/the returned hash.</summary>
        public static string ComputeLoginResponse(string UserName, string SessionId, string Password)
        {
            string raw = $"{UserName}:{SessionId}:{Password}";

            using MD5 md5 = MD5.Create();
            byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(raw));

            StringBuilder sb = new StringBuilder(hash.Length * 2);
            foreach (byte b in hash)
                sb.Append(b.ToString("x2"));

            return sb.ToString();
        }
    }
}
