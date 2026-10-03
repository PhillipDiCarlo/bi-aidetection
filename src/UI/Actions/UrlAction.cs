using System;
using System.Reflection;
using MQTTnet;
using SixLabors.ImageSharp;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Media;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Speech.Synthesis;
using System.Threading.Tasks;
using MQTTnet.Protocol;
using NPushover.RequestObjects;
using NPushover.ResponseObjects;
using Telegram.Bot.Exceptions;
using static AITool.AITOOL;

namespace AITool.Actions
{
    /// <summary>Calls the camera's trigger URLs (or cancel URLs for a cancel event), typically the Blue Iris /admin API.</summary>
    public class UrlAction : IActionChannel
    {
        public string Name => "Url";

        public async Task<bool> CallTriggerURLs(List<string> trigger_urls, ClsTriggerActionQueueItem AQI)
        {
            using var Trace = new Trace();  //This c# 8.0 using feature will auto dispose when the function is done.

            bool ret = true;
            string url = "";
            string type = "trigger";
            if (!AQI.Trigger)
                type = "cancel";

            if (AITOOL.triggerHttpClient == null)
            {
                AITOOL.triggerHttpClient = new ThrottledHttpClient(TimeSpan.FromSeconds(AppSettings.Settings.HTTPClientLocalTimeoutSeconds));  //new System.Net.Http.HttpClient();
                AITOOL.triggerHttpClient.Timeout = TimeSpan.FromSeconds(AppSettings.Settings.HTTPClientLocalTimeoutSeconds);

                //lets give it a user agent unique to this machine and product version...
                AssemblyName ASN = Assembly.GetExecutingAssembly().GetName();
                string Version = ASN.Version.ToString();
                ProductInfoHeaderValue PIH = new ProductInfoHeaderValue("AI_Tool_MANBAT_" + Global.GetMacAddress(), Version);
                AITOOL.triggerHttpClient.DefaultRequestHeaders.UserAgent.Add(PIH);
            }


            for (int i = 0; i < trigger_urls.Count; i++)
            {
                url = trigger_urls[i];

                if (url.IsStringBefore(";", ":"))
                {
                    //prm0 - object1, object2
                    //prm1 - soundfile.wav or URL
                    string objects = url.GetWord("", ";");
                    url = url.GetWord(";", "");
                    //make sure it is a matching object
                    //if (AITOOL.ArePredictionObjectsRelevant(objects, "TriggerURL", AQI.Hist.Predictions(), false) != ResultType.Relevant) ;

                    ClsRelevantObjectManager rom = new ClsRelevantObjectManager(objects, "TriggerURL", AQI.cam);

                    if (!AQI.Hist.IsNull() && rom.IsRelevant(AQI.Hist.Predictions(), false, out bool IgnoreImageMask, out bool IgnoreDynamicMask) != ResultType.Relevant)
                        continue;

                }
                else
                {
                    //Log($"Debug: No conditional objects found in URL: {url}");
                }

                //swap out "&user=...&pw=..." for a BlueIris "&session=..." if enabled - see ROADMAP.md 1.4 / BlueIrisSession.cs
                string originalUrl = url;
                string sessionBaseUrl = null;
                if (AppSettings.Settings.BlueIrisUseSessionLogin)
                    (url, sessionBaseUrl) = await BlueIrisSession.TryRewriteUrlForSessionLoginAsync(url);

                Stopwatch sw = Stopwatch.StartNew();
                try
                {
                    Log($"Debug:   -> {type} URL is being triggered... {url}");

                    HttpResponseMessage response = await triggerHttpClient.GetAsync(url);

                    //If using a session login and the call failed, the cached session may be stale/invalid - log in again and retry once
                    if (response != null && !response.IsSuccessStatusCode && sessionBaseUrl.IsNotEmpty())
                    {
                        Log($"Debug:   -> {type} URL failed with a BlueIris session login (StatusCode='{response.StatusCode}'), re-logging in and retrying once...");
                        BlueIrisSession.InvalidateSession(sessionBaseUrl);
                        (url, sessionBaseUrl) = await BlueIrisSession.TryRewriteUrlForSessionLoginAsync(originalUrl);
                        response = await triggerHttpClient.GetAsync(url);
                    }

                    //If we get a null response it means the host+port was already in use.  In that case, we are just going to skip this URL call and call it good.
                    if (response == null)
                    {
                        Log($"Debug:   -> {type} URL called in {sw.ElapsedMilliseconds}ms: {url}, response: 'URL was in use, skipping.  Turn off 'queue actions' in camera settings to avoid this happening.'");
                    }
                    else
                    {
                        if (response.IsSuccessStatusCode)
                        {
                            string content = await response.Content.ReadAsStringAsync();
                            Log($"Debug:   -> {type} URL called in {sw.ElapsedMilliseconds}ms: {url}, response: '{content.CleanString().Truncate()}'");
                        }
                        else
                        {
                            ret = false;
                            Log($"ERROR: {type}: In {sw.ElapsedMilliseconds}ms, got StatusCode='{response.StatusCode}', Reason='{response.ReasonPhrase}: Could not {type} URL '{url}', please check if correct");
                        }
                    }

                }
                catch (Exception ex)
                {
                    ret = false;
                    Log($"ERROR: {type}: In {sw.ElapsedMilliseconds}ms, Could not {type} Error='{ex.Msg()}', URL='{url}'");
                }

                if (AppSettings.Settings.ActionDelayMS >= 100)  //dont show for tiny delays
                    Log($"Debug: ...Applying 'ActionDelayMS' delay of {AppSettings.Settings.ActionDelayMS}ms.");

                await Task.Delay(AppSettings.Settings.ActionDelayMS); //very short wait between trigger events
            }


            return ret;


        }

        public bool ShouldRun(ClsTriggerActionQueueItem AQI)
        {
            return !(AQI.cam.Paused && AQI.cam.PauseURL) && ((AQI.Trigger && AQI.cam.Action_TriggerURL_Enabled && AQI.cam.trigger_urls.Length > 0) || (!AQI.Trigger && AQI.cam.Action_CancelURL_Enabled && AQI.cam.cancel_urls.Length > 0));
        }

        public async Task<bool> RunAsync(ClsTriggerActionQueueItem AQI, string CurSrv)
        {
            bool ret = true;

            if (AQI.Trigger && AQI.cam.Action_TriggerURL_Enabled && AQI.cam.trigger_urls.Length > 0)
            {
                //replace url parameters with according values
                List<string> urls = new List<string>();
                //call urls
                foreach (string url in AQI.cam.trigger_urls)
                {
                    string tmp = AITOOL.ReplaceParams(AQI.cam, AQI.Hist, AQI.CurImg, url, Global.IPType.URL);
                    urls.Add(tmp);

                }

                bool result = await this.CallTriggerURLs(urls, AQI);
            }
            else if (!AQI.Trigger && AQI.cam.Action_CancelURL_Enabled && AQI.cam.cancel_urls.Length > 0)
            {
                //replace url parameters with according values
                List<string> urls = new List<string>();
                //call urls
                foreach (string url in AQI.cam.cancel_urls)
                {
                    string tmp = AITOOL.ReplaceParams(AQI.cam, AQI.Hist, AQI.CurImg, url, Global.IPType.URL);
                    urls.Add(tmp);

                }

                bool result = await this.CallTriggerURLs(urls, AQI);

            }


            return ret;
        }
    }
}
