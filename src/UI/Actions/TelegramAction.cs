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
    /// <summary>Sends the image (or a text message) to the configured Telegram chats, with cooldown and retry-after-failure handling.</summary>
    public class TelegramAction : IActionChannel
    {
        public string Name => "Telegram";

        public ThreadSafe.DateTime last_telegram_trigger_time { get; set; } = new ThreadSafe.DateTime(DateTime.MinValue, AppSettings.Settings.DateFormat);
        public ThreadSafe.DateTime TelegramRetryTime { get; set; } = new ThreadSafe.DateTime(DateTime.MinValue, AppSettings.Settings.DateFormat);

        public async Task<bool> UploadAsync(ClsTriggerActionQueueItem AQI, string CurSrv)
        {
            using var Trace = new Trace();  //This c# 8.0 using feature will auto dispose when the function is done.

            bool ret = true;
            string lastchatid = "";

            if ((!string.IsNullOrWhiteSpace(AQI.cam.telegram_chatid) || AppSettings.Settings.telegram_chatids.Count > 0) && AppSettings.Settings.telegram_token != "")
            {
                //telegram upload sometimes fails
                Stopwatch sw = Stopwatch.StartNew();
                try
                {

                    if (AppSettings.Settings.telegram_cooldown_seconds < 2)
                        AppSettings.Settings.telegram_cooldown_seconds = 2;  //force to be at least 2 seconds

                    string Caption = "";

                    if (!string.IsNullOrEmpty(AQI.Text))
                        Caption = AITOOL.ReplaceParams(AQI.cam, AQI.Hist, AQI.CurImg, AQI.Text, Global.IPType.Path);
                    else
                        Caption = AITOOL.ReplaceParams(AQI.cam, AQI.Hist, AQI.CurImg, AQI.cam.telegram_caption, Global.IPType.Path);

                    //make sure it is a matching object
                    //if (AITOOL.ArePredictionObjectsRelevant(AQI.cam.telegram_triggering_objects, "Telegram", AQI.Hist.Predictions(), false) != ResultType.Relevant)
                    if (!AQI.Hist.IsNull())
                    {
                        if (AQI.cam.TelegramTriggeringObjects.IsRelevant(AQI.Hist.Predictions(), false, out bool IgnoreImageMask, out bool IgnoreDynamicMask) != ResultType.Relevant)
                        {
                            AITOOL.Log("Debug: No relevant objects, skipping TelegramUpload.");
                            return true;
                        }
                    }
                    else
                    {
                        AITOOL.Log("Warn: Hist is null?");
                    }


                    DateTime now = DateTime.Now;

                    if (this.TelegramRetryTime == DateTime.MinValue || now >= this.TelegramRetryTime)
                    {
                        double cooltime = Math.Round((now - this.last_telegram_trigger_time).TotalSeconds, 4);
                        if (cooltime >= AppSettings.Settings.telegram_cooldown_seconds)
                        {
                            //in order to avoid hitting our limits when sending out mass notifications, consider spreading them over longer intervals, e.g. 8-12 hours. The API will not allow bulk notifications to more than ~30 users per second, if you go over that, you'll start getting 429 errors.

                            if (Global.IsTimeBetween(now, AQI.cam.telegram_active_time_range))
                            {
                                string chatid = "";
                                bool overrideid = (!string.IsNullOrWhiteSpace(AQI.cam.telegram_chatid));
                                if (overrideid)
                                    chatid = AQI.cam.telegram_chatid.Trim();
                                else
                                    chatid = AppSettings.Settings.telegram_chatids[0];

                                //upload image to Telegram servers and send to first chat
                                Log($"Debug:      uploading image to chat \"{chatid.ReplaceChars('*')}\"", CurSrv, AQI.cam, AQI.CurImg);
                                lastchatid = chatid;

                                Telegram.Bot.Types.Message message = await AITOOL.Telegram.SendPhotoAsync(chatid, AQI.CurImg.ToMemStream(), "", AQI.CurImg.image_path, Caption);

                                string file_id = message.Photo[0].FileId; //get file_id of uploaded image

                                if (!overrideid)
                                {
                                    //share uploaded image with all remaining telegram chats (if multiple chat_ids given) using file_id 
                                    foreach (string curchatid in AppSettings.Settings.telegram_chatids.Skip(1))
                                    {
                                        Log($"Debug:      uploading image to chat \"{curchatid.ReplaceChars('*')}\"...", CurSrv, AQI.cam, AQI.CurImg);
                                        lastchatid = curchatid;
                                        message = await AITOOL.Telegram.SendPhotoAsync(curchatid, null, file_id, "", Caption);
                                    }
                                }
                                ret = message != null;

                                this.last_telegram_trigger_time = DateTime.Now;
                                this.TelegramRetryTime = DateTime.MinValue;

                                if (AQI.IsQueued)
                                {
                                    //add a minimum delay if we are in a queue to prevent minimum cooldown error
                                    Log($"Debug: Waiting {AppSettings.Settings.telegram_cooldown_seconds} seconds (telegram_cooldown_seconds)...", CurSrv, AQI.cam, AQI.CurImg);
                                    await Task.Delay(TimeSpan.FromSeconds(AppSettings.Settings.telegram_cooldown_seconds));
                                }

                            }
                            else
                            {
                                Log($"Debug: Skipping Telegram because time is not between {AQI.cam.telegram_active_time_range}");
                            }
                        }
                        else
                        {
                            //log that nothing was done
                            Log($"Debug:   Still in TELEGRAM cooldown. No image will be uploaded to Telegram.  ({cooltime} of {AppSettings.Settings.telegram_cooldown_seconds} seconds - See 'telegram_cooldown_seconds' in settings file)", CurSrv, AQI.cam, AQI.CurImg);

                        }

                    }
                    else
                    {
                        Log($"Debug:   Waiting {Math.Round((this.TelegramRetryTime - DateTime.Now).TotalSeconds, 1)} seconds ({this.TelegramRetryTime}) to retry TELEGRAM connection.  This is due to a previous telegram send error.", CurSrv, AQI.cam, AQI.CurImg);
                    }


                }
                catch (ApiRequestException ex)  //current version only gives webexception NOT this exception!  https://github.com/TelegramBots/Telegram.Bot/issues/891
                {
                    bool se = AppSettings.Settings.send_telegram_errors;
                    AppSettings.Settings.send_telegram_errors = false;
                    Log($"ERROR: Could not upload image {AQI.CurImg.image_path} with chatid '{lastchatid.ReplaceChars('*')}' to Telegram: {ex.Msg()}", CurSrv, AQI.cam, AQI.CurImg);

                    if (!ex.Parameters.IsNull() && !ex.Parameters.RetryAfter.IsNull())
                    {
                        this.TelegramRetryTime = DateTime.Now.AddSeconds(Convert.ToDouble(ex.Parameters.RetryAfter));
                        Log($"...BOT API returned 'RetryAfter' value '{ex.Parameters.RetryAfter} seconds', so not retrying until {this.TelegramRetryTime}", CurSrv, AQI.cam, AQI.CurImg);
                    }

                    AppSettings.Settings.send_telegram_errors = se;
                    //store image that caused an error in ./errors/
                    if (!Directory.Exists("./errors/")) //if folder does not exist, create the folder
                    {
                        //create folder
                        DirectoryInfo di = Directory.CreateDirectory("./errors");
                        Log($"./errors/" + " dir created.");
                    }
                    //save error image
                    using (var image = await SixLabors.ImageSharp.Image.LoadAsync(AQI.CurImg.image_path))
                    {
                        await image.SaveAsync($"./errors/" + "TELEGRAM-ERROR-" + Path.GetFileName(AQI.CurImg.image_path) + ".jpg");
                    }
                    Global.UpdateLabel($"Can't upload error message to Telegram!", "lbl_errors");
                    ret = false;

                }
                catch (Exception ex)  //As of version 
                {
                    bool se = AppSettings.Settings.send_telegram_errors;
                    AppSettings.Settings.send_telegram_errors = false;
                    Log($"ERROR: Could not upload image {AQI.CurImg.image_path} to Telegram with chatid '{lastchatid.ReplaceChars('*')}': {ex.Msg()}", CurSrv, AQI.cam, AQI.CurImg);
                    this.TelegramRetryTime = DateTime.Now.AddSeconds(AppSettings.Settings.Telegram_RetryAfterFailSeconds);
                    Log($"Debug: ...'Default' 'Telegram_RetryAfterFailSeconds' value was set to '{AppSettings.Settings.Telegram_RetryAfterFailSeconds}' seconds, so not retrying until {this.TelegramRetryTime}", CurSrv, AQI.cam, AQI.CurImg);
                    AppSettings.Settings.send_telegram_errors = se;
                    //store image that caused an error in ./errors/
                    if (!Directory.Exists("./errors/")) //if folder does not exist, create the folder
                    {
                        //create folder
                        DirectoryInfo di = Directory.CreateDirectory("./errors");
                        Log($"./errors/" + " dir created.");
                    }
                    //save error image
                    using (var image = await SixLabors.ImageSharp.Image.LoadAsync(AQI.CurImg.image_path))
                    {
                        await image.SaveAsync("./errors/" + "TELEGRAM-ERROR-" + Path.GetFileName(AQI.CurImg.image_path) + ".jpg");
                    }
                    Global.UpdateLabel($"Can't upload error message to Telegram!", "lbl_errors");
                    ret = false;

                }


                Log($"Debug: ...Finished in {sw.ElapsedMilliseconds}ms", CurSrv, AQI.cam, AQI.CurImg);

            }
            else
            {
                Log($"Error:  Telegram settings mis-configured. telegram_chatids.Count={AppSettings.Settings.telegram_chatids.Count} ({string.Join(",", AppSettings.Settings.telegram_chatids).ReplaceChars('*')}), telegram_token='{AppSettings.Settings.telegram_token.ReplaceChars('*')}'", CurSrv, AQI.cam, AQI.CurImg);
                ret = false;
            }

            return ret;

        }

        public async Task<bool> SendTextAsync(ClsTriggerActionQueueItem AQI, string CurSrv)
        {
            using var Trace = new Trace();  //This c# 8.0 using feature will auto dispose when the function is done.

            bool ret = false;
            string lastchatid = "";
            if (AppSettings.Settings.telegram_chatids.Count > 0 && AppSettings.Settings.telegram_token != "")
            {
                try
                {

                    if (AppSettings.Settings.telegram_cooldown_seconds < 2)
                        AppSettings.Settings.telegram_cooldown_seconds = 2;  //force to be at least 2 second

                    string Caption = AITOOL.ReplaceParams(AQI.cam, AQI.Hist, AQI.CurImg, AQI.Text, Global.IPType.Path);

                    DateTime now = DateTime.Now;

                    if (this.TelegramRetryTime == DateTime.MinValue || now >= this.TelegramRetryTime)
                    {
                        double cooltime = Math.Round((now - this.last_telegram_trigger_time).TotalSeconds, 4);
                        if (cooltime >= AppSettings.Settings.telegram_cooldown_seconds)
                        {
                            if (Global.IsTimeBetween(now, AQI.cam.telegram_active_time_range))
                            {
                                string chatid = "";
                                bool overrideid = (!string.IsNullOrWhiteSpace(AQI.cam.telegram_chatid));
                                if (overrideid)
                                    chatid = AQI.cam.telegram_chatid.Trim();
                                else
                                    chatid = AppSettings.Settings.telegram_chatids[0];


                                if (overrideid)
                                {
                                    lastchatid = chatid;
                                    Telegram.Bot.Types.Message msg = await AITOOL.Telegram.SendTextMessageAsync(chatid, Caption);

                                }
                                else
                                {
                                    foreach (string curchatid in AppSettings.Settings.telegram_chatids)
                                    {
                                        lastchatid = curchatid;
                                        Telegram.Bot.Types.Message msg = await AITOOL.Telegram.SendTextMessageAsync(curchatid, Caption);

                                    }

                                }
                                this.last_telegram_trigger_time = DateTime.Now;
                                this.TelegramRetryTime = DateTime.MinValue;

                                if (AQI.IsQueued)
                                {
                                    //add a minimum delay if we are in a queue to prevent minimum cooldown error
                                    Log($"Waiting {AppSettings.Settings.telegram_cooldown_seconds} seconds (telegram_cooldown_seconds)...", CurSrv, AQI.cam, AQI.CurImg);
                                    await Task.Delay(TimeSpan.FromSeconds(AppSettings.Settings.telegram_cooldown_seconds));
                                }

                                ret = true;
                            }
                            else
                            {
                                Log($"Debug: Skipping Telegram because time is not between {AQI.cam.telegram_active_time_range}");
                            }
                        }
                        else
                        {
                            //log that nothing was done
                            Log($"   Still in TELEGRAM cooldown. No image will be uploaded to Telegram.  ({cooltime} of {AppSettings.Settings.telegram_cooldown_seconds} seconds - See 'telegram_cooldown_seconds' in settings file)", CurSrv, AQI.cam, AQI.CurImg);

                        }

                    }
                    else
                    {
                        Log($"   Waiting {Math.Round((this.TelegramRetryTime - DateTime.Now).TotalSeconds, 1)} seconds ({this.TelegramRetryTime}) to retry TELEGRAM connection.  This is due to a previous telegram send error.", CurSrv, AQI.cam, AQI.CurImg);
                    }



                }
                catch (ApiRequestException ex)  //current version only gives webexception NOT this exception!  https://github.com/TelegramBots/Telegram.Bot/issues/891
                {
                    bool se = AppSettings.Settings.send_telegram_errors;
                    AppSettings.Settings.send_telegram_errors = false;
                    Log($"ERROR: Could not upload text '{AQI.Text}' with chatid '{lastchatid}' to Telegram: {ex.Msg()}", CurSrv, AQI.cam, AQI.CurImg);
                    this.TelegramRetryTime = DateTime.Now.AddSeconds(Convert.ToDouble(ex.Parameters.RetryAfter));
                    Log($"...BOT API returned 'RetryAfter' value '{ex.Parameters.RetryAfter} seconds', so not retrying until {this.TelegramRetryTime}", CurSrv, AQI.cam, AQI.CurImg);
                    AppSettings.Settings.send_telegram_errors = se;
                    Global.UpdateLabel($"Can't upload error message to Telegram!", "lbl_errors");

                }
                catch (Exception ex)
                {
                    bool se = AppSettings.Settings.send_telegram_errors;
                    AppSettings.Settings.send_telegram_errors = false;
                    Log($"ERROR: Could not upload image '{AQI.Text}' with chatid '{lastchatid}' to Telegram: {ex.Msg()}", CurSrv, AQI.cam, AQI.CurImg);
                    this.TelegramRetryTime = DateTime.Now.AddSeconds(AppSettings.Settings.Telegram_RetryAfterFailSeconds);
                    Log($"...'Default' 'Telegram_RetryAfterFailSeconds' value was set to '{AppSettings.Settings.Telegram_RetryAfterFailSeconds}' seconds, so not retrying until {this.TelegramRetryTime}", CurSrv, AQI.cam, AQI.CurImg);
                    AppSettings.Settings.send_telegram_errors = se;
                    Global.UpdateLabel($"Can't upload error message to Telegram!", "lbl_errors");
                }

            }
            else
            {
                Log($"Error:  Telegram settings misconfigured. telegram_chatids.Count={AppSettings.Settings.telegram_chatids.Count} ({string.Join(",", AppSettings.Settings.telegram_chatids)}), telegram_token='{AppSettings.Settings.telegram_token}'", CurSrv, AQI.cam, AQI.CurImg);
            }

            return ret;
        }

        public bool ShouldRun(ClsTriggerActionQueueItem AQI)
        {
            return AQI.cam.telegram_enabled && AQI.Trigger && !(AQI.cam.Paused && AQI.cam.PauseTelegram);
        }

        public async Task<bool> RunAsync(ClsTriggerActionQueueItem AQI, string CurSrv)
        {
            bool ret = true;

            if (!await this.UploadAsync(AQI, CurSrv))
            {
                ret = false;
                Log($"Error:   -> ERROR sending image to Telegram.", CurSrv, AQI.cam, AQI.CurImg);
            }
            else
            {
                Log($"Debug:   -> Sent image to Telegram.", CurSrv, AQI.cam, AQI.CurImg);
            }

            if (AppSettings.Settings.ActionDelayMS >= 100)  //dont show for tiny delays
                Log($"Debug:  ...Applying 'ActionDelayMS' delay of {AppSettings.Settings.ActionDelayMS}ms.");

            await Task.Delay(AppSettings.Settings.ActionDelayMS); //very short wait between trigger events

            return ret;
        }
    }
}
