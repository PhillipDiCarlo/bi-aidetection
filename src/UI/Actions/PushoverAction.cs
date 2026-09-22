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
    /// <summary>Sends the image and message to Pushover, with cooldown and retry-after-failure handling.</summary>
    public class PushoverAction : IActionChannel
    {
        public string Name => "Pushover";

        public ThreadSafe.DateTime last_Pushover_trigger_time { get; set; } = new ThreadSafe.DateTime(DateTime.MinValue, AppSettings.Settings.DateFormat);
        public ThreadSafe.DateTime PushoverRetryTime { get; set; } = new ThreadSafe.DateTime(DateTime.MinValue, AppSettings.Settings.DateFormat);

        public async Task<bool> UploadAsync(ClsTriggerActionQueueItem AQI, string CurSrv)
        {
            using var Trace = new Trace();  //This c# 8.0 using feature will auto dispose when the function is done.

            bool ret = true;

            if (!string.IsNullOrEmpty(AppSettings.Settings.pushover_APIKey) && !string.IsNullOrEmpty(AppSettings.Settings.pushover_UserKey))
            {
                try
                {
                    //make sure it is a matching object
                    if (!AQI.Hist.IsNull() && AQI.cam.PushoverTriggeringObjects.IsRelevant(AQI.Hist.Predictions(), false, out bool IgnoreImageMask, out bool IgnoreDynamicMask) != ResultType.Relevant)
                        return true;

                    if (AppSettings.Settings.pushover_cooldown_seconds < 2)
                        AppSettings.Settings.pushover_cooldown_seconds = 2;  //force to be at least 2 seconds

                    DateTime now = DateTime.Now;

                    if (this.PushoverRetryTime == DateTime.MinValue || now >= this.PushoverRetryTime)
                    {
                        double cooltime = Math.Round((now - this.last_Pushover_trigger_time).TotalSeconds, 4);
                        if (cooltime >= AppSettings.Settings.pushover_cooldown_seconds)
                        {
                            string title = "";
                            string message = "";
                            string device = "";

                            if (AQI.Trigger)
                            {

                                if (!string.IsNullOrEmpty(AQI.Text))
                                {
                                    if (AQI.Text.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0)
                                        title = "Error";
                                    else
                                        title = AITOOL.ReplaceParams(AQI.cam, AQI.Hist, AQI.CurImg, AQI.cam.Action_pushover_title, Global.IPType.Path);

                                    message = AITOOL.ReplaceParams(AQI.cam, AQI.Hist, AQI.CurImg, AQI.Text, Global.IPType.Path);

                                }
                                else
                                {
                                    title = AITOOL.ReplaceParams(AQI.cam, AQI.Hist, AQI.CurImg, AQI.cam.Action_pushover_title, Global.IPType.Path);
                                    message = AITOOL.ReplaceParams(AQI.cam, AQI.Hist, AQI.CurImg, AQI.cam.Action_pushover_message, Global.IPType.Path);
                                }

                                device = AITOOL.ReplaceParams(AQI.cam, AQI.Hist, AQI.CurImg, AQI.cam.Action_pushover_device, Global.IPType.Path);
                            }
                            else  //TODO: Add cancel if requested
                            {
                                title = AITOOL.ReplaceParams(AQI.cam, AQI.Hist, AQI.CurImg, AQI.cam.Action_pushover_title, Global.IPType.Path);
                                message = AITOOL.ReplaceParams(AQI.cam, AQI.Hist, AQI.CurImg, AQI.cam.Action_pushover_message, Global.IPType.Path);
                                device = AITOOL.ReplaceParams(AQI.cam, AQI.Hist, AQI.CurImg, AQI.cam.Action_pushover_device, Global.IPType.Path);
                            }


                            List<string> titles = title.SplitStr("|");
                            List<string> messages = message.SplitStr("|");
                            List<string> devices = device.SplitStr("|");
                            List<string> sounds = AQI.cam.Action_pushover_Sound.SplitStr("|");
                            List<string> priorities = AQI.cam.Action_pushover_Priority.SplitStr("|");
                            List<string> times = AQI.cam.Action_pushover_active_time_range.SplitStr("|");

                            for (int t = 0; t < times.Count; t++)
                            {
                                string time = times.GetStrAtIndex(t);

                                if (Global.IsTimeBetween(now, time))
                                {

                                    if (AITOOL.pushoverClient == null)
                                        AITOOL.pushoverClient = new NPushover.Pushover(AppSettings.Settings.pushover_APIKey); //new PushoverClient.Pushover(, AppSettings.Settings.pushover_UserKey);

                                    string imginfo = "";
                                    if (AQI.CurImg != null && AQI.CurImg.IsValid())
                                    {
                                        imginfo = $"Attached Image: {Path.GetFileName(AQI.CurImg.image_path)}";
                                    }

                                    PushoverUserResponse response = null;

                                    Stopwatch sw = Stopwatch.StartNew();

                                    try
                                    {
                                        string pushtitle = titles.GetStrAtIndex(t);
                                        string pushmessage = messages.GetStrAtIndex(t);
                                        string pushsound = sounds.GetStrAtIndex(t);
                                        string pushdevice = devices.GetStrAtIndex(t);

                                        if (times.Count != priorities.Count)
                                            Log($"Warn: You should have the same number of Pushover priorities and times specified.");

                                        NPushover.RequestObjects.Priority pri = (NPushover.RequestObjects.Priority)Enum.Parse(typeof(NPushover.RequestObjects.Priority), priorities.GetStrAtIndex(t));

                                        //fix a bug where pushover expire was set to hours rather than seconds
                                        if (AQI.cam.Action_pushover_expire_seconds >= TimeSpan.FromHours(24).TotalSeconds)
                                            AQI.cam.Action_pushover_expire_seconds = 10800; //3 hours

                                        NPushover.RequestObjects.Message msg = new NPushover.RequestObjects.Message()
                                        {
                                            Title = pushtitle,
                                            Body = pushmessage,
                                            Timestamp = AQI.CurImg != null ? AQI.CurImg.TimeCreated : DateTime.Now,
                                            Priority = pri,
                                            Sound = pushsound,

                                            RetryOptions = pri == Priority.Emergency ? new RetryOptions
                                            {
                                                RetryEvery = TimeSpan.FromSeconds(AQI.cam.Action_pushover_retry_seconds),
                                                RetryPeriod = TimeSpan.FromSeconds(AQI.cam.Action_pushover_expire_seconds),
                                                CallBackUrl = !string.IsNullOrEmpty(AQI.cam.Action_pushover_retrycallback_url) ? new Uri(AQI.cam.Action_pushover_retrycallback_url) : null,
                                            } : null,
                                            SupplementaryUrl = !string.IsNullOrEmpty(AQI.cam.Action_pushover_SupplementaryUrl) ? new SupplementaryURL { Uri = new Uri(AQI.cam.Action_pushover_SupplementaryUrl), Title = "42" } : null,
                                        };

                                        sw.Restart();

                                        List<string> userkeys = AppSettings.Settings.pushover_UserKey.SplitStr("|,;");
                                        foreach (string userkey in userkeys)
                                        {
                                            Log($"Debug: Sending pushover message '{pushmessage}', priority '{pri.ToString()}', sound '{pushsound}' to user '{userkey}' {imginfo}...");
                                            response = await AITOOL.pushoverClient.SendPushoverMessageAsync(msg, userkey, pushdevice, AQI.CurImg);
                                            await Task.Delay(AppSettings.Settings.loop_delay_ms);
                                        }
                                        this.last_Pushover_trigger_time = now;
                                        sw.Stop();
                                    }
                                    catch (Exception ex)
                                    {

                                        sw.Stop();
                                        ret = false;
                                        Log($"Error: Pushover: After {sw.ElapsedMilliseconds}ms, got: " + ex.Msg(), CurSrv, AQI.cam, AQI.CurImg);
                                    }

                                    if (response != null)
                                    {
                                        string rateinfo = "";
                                        if (response.RateLimitInfo != null)
                                        {
                                            rateinfo = $"(Monthly Limit={response.RateLimitInfo.Limit}, Remaining={response.RateLimitInfo.Remaining}, ResetDate={response.RateLimitInfo.Reset})";
                                        }

                                        if (response.IsOk)
                                        {
                                            ret = true;
                                            Log($"Debug: ...Pushover success in {sw.ElapsedMilliseconds}ms {rateinfo}");
                                        }
                                        else
                                        {
                                            string errs = "";
                                            if (response.HasErrors)
                                                errs = string.Join(";", response.Errors);
                                            ret = false;
                                            Log($"Error: Pushover response code={response.Status} in {sw.ElapsedMilliseconds}ms, Errs='{errs}' {rateinfo}");
                                        }
                                    }
                                    else
                                    {
                                        ret = false;
                                        Log($"Error: Pushover failed to return a response in {sw.ElapsedMilliseconds}ms?", CurSrv, AQI.cam, AQI.CurImg);
                                    }

                                    if (!ret)
                                        this.PushoverRetryTime = DateTime.Now.AddSeconds(AppSettings.Settings.Pushover_RetryAfterFailSeconds);
                                    else
                                        this.PushoverRetryTime = DateTime.MinValue;



                                }
                                else
                                {
                                    Log($"Debug: Skipping pushover because time is not between {time}");
                                }

                            }


                        }
                        else
                        {
                            //log that nothing was done
                            Log($"Debug:   Still in PUSHOVER cooldown. No image will be uploaded to Pushover.  ({cooltime} of {AppSettings.Settings.pushover_cooldown_seconds} seconds - See 'pushover_cooldown_seconds' in settings file)", CurSrv, AQI.cam, AQI.CurImg);

                        }
                    }
                    else
                    {
                        Log($"Debug:   Waiting {Math.Round((this.PushoverRetryTime - DateTime.Now).TotalSeconds, 1)} seconds ({this.PushoverRetryTime}) to retry PUSHOVER connection.  This is due to a previous pushover send error.", CurSrv, AQI.cam, AQI.CurImg);
                    }


                }
                catch (Exception ex)
                {

                    ret = false;
                    Log($"Error: {ex.Msg()}", CurSrv, AQI.cam, AQI.CurImg);
                }
            }
            else
            {
                ret = false;
                Log("Error: Pushover API key or User Key not set.");
            }


            return ret;
        }

        public bool ShouldRun(ClsTriggerActionQueueItem AQI)
        {
            return AQI.cam.Action_pushover_enabled && AQI.Trigger && !(AQI.cam.Paused && AQI.cam.PausePushover);
        }

        public async Task<bool> RunAsync(ClsTriggerActionQueueItem AQI, string CurSrv)
        {
            bool ret = true;

            if (!await this.UploadAsync(AQI, CurSrv))
            {
                ret = false;
                Log($"Error:   -> ERROR sending message or image to Pushover", CurSrv, AQI.cam, AQI.CurImg);
            }
            else
            {
                Log($"Debug:   -> Sent message or image to Pushover.", CurSrv, AQI.cam, AQI.CurImg);
            }

            if (AppSettings.Settings.ActionDelayMS >= 100)  //dont show for tiny delays
                Log($"Debug:  ...Applying 'ActionDelayMS' delay of {AppSettings.Settings.ActionDelayMS}ms.");

            await Task.Delay(AppSettings.Settings.ActionDelayMS); //very short wait between trigger events


            return ret;
        }
    }
}
