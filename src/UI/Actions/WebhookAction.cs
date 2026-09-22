using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using static AITool.AITOOL;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("AITool.Tests")]

namespace AITool.Actions
{
    /// <summary>Calls a generic HTTP webhook (JSON, form, or multipart) on trigger or cancel, e.g. Discord or Home Assistant.</summary>
    public class WebhookAction : IActionChannel
    {
        public string Name => "Webhook";

        private static HttpClient webhookHttpClient = null;

        public bool ShouldRun(ClsTriggerActionQueueItem AQI)
        {
            return AQI.cam.Action_webhook_enabled && !(AQI.cam.Paused && AQI.cam.PauseURL) &&
                   ((AQI.Trigger && AQI.cam.Action_webhook_url.IsNotEmpty()) || (!AQI.Trigger && AQI.cam.Action_webhook_cancel_url.IsNotEmpty()));
        }

        public async Task<bool> RunAsync(ClsTriggerActionQueueItem AQI, string CurSrv)
        {
            using var Trace = new Trace();  //This c# 8.0 using feature will auto dispose when the function is done.

            bool ret = true;
            string type = AQI.Trigger ? "trigger" : "cancel";

            string urltemplate = AQI.Trigger ? AQI.cam.Action_webhook_url : AQI.cam.Action_webhook_cancel_url;
            string bodytemplate = AQI.Trigger ? AQI.cam.Action_webhook_body : AQI.cam.Action_webhook_cancel_body;

            string url = AITOOL.ReplaceParams(AQI.cam, AQI.Hist, AQI.CurImg, urltemplate, Global.IPType.URL);
            string body = AITOOL.ReplaceParams(AQI.cam, AQI.Hist, AQI.CurImg, bodytemplate, Global.IPType.URL);
            List<(string Name, string Value)> headers = ParseHeaders(AITOOL.ReplaceParams(AQI.cam, AQI.Hist, AQI.CurImg, AQI.cam.Action_webhook_headers, Global.IPType.URL));

            if (webhookHttpClient == null)
            {
                webhookHttpClient = new HttpClient();
                webhookHttpClient.Timeout = TimeSpan.FromSeconds(AppSettings.Settings.HTTPClientRemoteTimeoutSeconds);
            }

            Stopwatch sw = Stopwatch.StartNew();
            try
            {
                Log($"Debug:   -> Webhook {type} is being called... {url}");

                using HttpRequestMessage request = BuildRequest(url, AQI.cam.Action_webhook_method, AQI.cam.Action_webhook_content_type, body, headers, AQI.cam.Action_webhook_send_image ? AQI.CurImg : null);

                HttpResponseMessage response = await webhookHttpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    string content = await response.Content.ReadAsStringAsync();
                    Log($"Debug:   -> Webhook {type} called in {sw.ElapsedMilliseconds}ms: {url}, response: '{content.CleanString().Truncate()}'");
                }
                else
                {
                    ret = false;
                    Log($"ERROR: Webhook {type}: In {sw.ElapsedMilliseconds}ms, got StatusCode='{response.StatusCode}', Reason='{response.ReasonPhrase}': Could not call webhook '{url}'");
                }
            }
            catch (Exception ex)
            {
                ret = false;
                Log($"ERROR: Webhook {type}: In {sw.ElapsedMilliseconds}ms, Could not call webhook. Error='{ex.Msg()}', URL='{url}'");
            }

            if (AppSettings.Settings.ActionDelayMS >= 100)  //dont show for tiny delays
                Log($"Debug: ...Applying 'ActionDelayMS' delay of {AppSettings.Settings.ActionDelayMS}ms.");

            await Task.Delay(AppSettings.Settings.ActionDelayMS); //very short wait between trigger events

            return ret;
        }

        /// <summary>Parses "Name: value" header lines (one per line), ignoring blank lines and lines without a colon.</summary>
        internal static List<(string Name, string Value)> ParseHeaders(string headersText)
        {
            List<(string Name, string Value)> ret = new List<(string Name, string Value)>();

            foreach (string line in headersText.SplitStr("\r\n"))
            {
                int idx = line.IndexOf(':');
                if (idx <= 0)
                    continue;

                string headername = line.Substring(0, idx).Trim();
                string headervalue = line.Substring(idx + 1).Trim();

                if (headername.IsNotEmpty())
                    ret.Add((headername, headervalue));
            }

            return ret;
        }

        /// <summary>Builds the outgoing HttpRequestMessage. When ImageForMultipart is set, sends multipart/form-data with the (flattened) body fields plus the image as a file part named "image"; otherwise sends the body as-is with ContentType.</summary>
        internal static HttpRequestMessage BuildRequest(string url, string method, string contentType, string body, List<(string Name, string Value)> headers, ClsImageQueueItem imageForMultipart)
        {
            HttpMethod httpmethod = new HttpMethod(method.IsNotEmpty() ? method : "POST");
            HttpRequestMessage request = new HttpRequestMessage(httpmethod, url);

            if (imageForMultipart != null)
            {
                MultipartFormDataContent multipart = new MultipartFormDataContent();

                //try to flatten the (usually JSON) body into individual form fields, falling back to a single 'payload' field if it isn't valid JSON
                JObject jbody = null;
                try { jbody = JObject.Parse(body); } catch { jbody = null; }

                if (jbody != null)
                {
                    foreach (JProperty prop in jbody.Properties())
                    {
                        string value = prop.Value.Type == JTokenType.String ? prop.Value.ToString() : prop.Value.ToString(Newtonsoft.Json.Formatting.None);
                        multipart.Add(new StringContent(value, Encoding.UTF8), prop.Name);
                    }
                }
                else if (body.IsNotEmpty())
                {
                    multipart.Add(new StringContent(body, Encoding.UTF8), "payload");
                }

                StreamContent imagepart = new StreamContent(imageForMultipart.ToMemStream());
                imagepart.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
                multipart.Add(imagepart, "image", Path.GetFileName(imageForMultipart.image_path));

                request.Content = multipart;
            }
            else
            {
                request.Content = new StringContent(body, Encoding.UTF8, contentType.IsNotEmpty() ? contentType : "application/json");
            }

            foreach ((string headername, string headervalue) in headers)
            {
                if (!request.Headers.TryAddWithoutValidation(headername, headervalue))
                    request.Content?.Headers.TryAddWithoutValidation(headername, headervalue);
            }

            return request;
        }
    }
}
