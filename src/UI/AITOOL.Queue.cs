using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Media;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using Amazon;
using Amazon.Rekognition;
using Amazon.Rekognition.Model;
using Amazon.Runtime;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using NLog;

using NPushover;

using OSVersionExtension;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

using AITool.WebDashboard;

using static AITool.Global;

using Rectangle = System.Drawing.Rectangle;

namespace AITool
{
    public static partial class AITOOL
    {

        static ThreadSafe.Integer CurRunningDetectTasks = new ThreadSafe.Integer(0);

        public static async Task ImageQueueLoop()
        {
            //This runs in another thread, waiting for items to appear in the queue and process them one at a time
            try
            {
                //Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;

                ClsImageQueueItem CurImg;

                DateTime LastCleanDupesTime = DateTime.MinValue;


                //lets wait 5 seconds to let the UI settle down a bit
                await Task.Delay(5000);

                //Start infinite loop waiting for images to come into queue
                ThreadSafe.Integer MaxThreadCnt = new ThreadSafe.Integer(0);

                while (true)
                {
                    if (MasterCTS.IsCancellationRequested)
                        break;

                    ThreadSafe.Integer ThreadCnt = new ThreadSafe.Integer(0);

                    while (!ImageProcessQueue.IsEmpty)
                    {

                        ThreadSafe.Integer ProcImgCnt = 0;
                        ThreadSafe.Integer ErrCnt = 0;
                        ThreadSafe.Long LastThreadInitTimeMS = 0;
                        ThreadSafe.DateTime NextDeepstackRestartTime = new ThreadSafe.DateTime(DateTime.MinValue, AppSettings.Settings.DateFormat);

                        while (!ImageProcessQueue.IsEmpty)
                        {
                            //tiny delay to conserve cpu and allow more images to come in the queue if needed
                            //await Task.Delay(250);

                            //get the next image

                            if (ImageProcessQueue.TryDequeue(out CurImg))
                            {
                                Camera cam = GetCamera(CurImg.image_path, true);

                                if (cam == null)
                                {
                                    Log($"Error: Camera could not be found to match this image: {CurImg.image_path}");
                                    continue;
                                }

                                //skip the image if its been in the queue too long
                                if ((DateTime.Now - CurImg.TimeAdded).TotalMinutes >= AppSettings.Settings.MaxImageQueueTimeMinutes)
                                {
                                    Log($"...Taking image OUT OF QUEUE because it has been in there over 'MaxImageQueueTimeMinutes'. (QueueTime={(DateTime.Now - CurImg.TimeAdded).TotalMinutes.ToString("###0.0")}, Image ErrCount={CurImg.ErrCount}, Image RetryCount={CurImg.RetryCount}, ImageProcessQueue.Count={ImageProcessQueue.Count}: '{CurImg.image_path}'", "None", cam, CurImg);
                                    continue;
                                }

                                Stopwatch sw = Stopwatch.StartNew();

                                //wait for the next url to become available...
                                List<ClsURLItem> urls = await WaitForNextURL(cam, false);

                                sw.Stop();

                                //If we have any linked servers there may be more than one server that we send at the same time
                                foreach (ClsURLItem url in urls)
                                {
                                    Log($"Debug: Adding task for file '{Path.GetFileName(CurImg.image_path)}' (Image QueueTime='{(DateTime.Now - CurImg.TimeAdded).TotalMinutes.ToString("###0.0")}' mins, CurRunningTasks={CurRunningDetectTasks}, URL Queue wait='{sw.ElapsedMilliseconds}ms', URLOrder={url.CurOrder}, URLOriginalOrder={url.Order}) on URL '{url}'", url.CurSrv, cam, CurImg);

                                }

                                sw.Start();

                                //This *should* start detection on another thread and return control to this thread right away.
                                Task.Run(async () =>
                                {

                                    CurRunningDetectTasks++;
                                    Global.SendMessage(MessageType.BeginProcessImage, CurImg.image_path);

                                    DetectObjectsResult result = await DetectObjects(CurImg, urls); //ai process image

                                    Global.SendMessage(MessageType.EndProcessImage, CurImg.image_path);

                                    foreach (ClsURLItem url in result.OutURLs)
                                    {
                                        if (!url.LastResultSuccess)
                                        {
                                            ErrCnt++;
                                            url.ErrsInRowCount++;

                                            if (url.CurErrCount > 0)
                                            {
                                                if (url.CurErrCount < AppSettings.Settings.MaxQueueItemRetries)
                                                {
                                                    //put url back in queue when done

                                                    //Only send error if MaxWaitForAIServerTimeoutError is true
                                                    string ew = "Debug:";
                                                    if (AppSettings.Settings.MaxWaitForAIServerTimeoutError)
                                                        ew = "Error:";
                                                    if (url.LastResultMessage.Has("request timed out"))
                                                    {
                                                        Log($"{ew}...Problem with AI URL: '{url.LastResultMessage}' - '{url}' (URL ErrCount={url.CurErrCount}, max allowed of {AppSettings.Settings.MaxQueueItemRetries})", url.CurSrv, cam);
                                                    }
                                                    else
                                                    {
                                                        Log($"...Problem with AI URL: '{url.LastResultMessage}' - '{url}' (URL ErrCount={url.CurErrCount}, max allowed of {AppSettings.Settings.MaxQueueItemRetries})", url.CurSrv, cam);
                                                    }
                                                }
                                                else
                                                {
                                                    url.ErrDisabled = false;
                                                    Log($"...Error: AI URL failed with '{url.LastResultMessage}' - for '{url.Type}' failed '{url.CurErrCount}' times.  Disabling: '{url}'", url.CurSrv, cam);
                                                }

                                            }

                                            if (url.ErrsInRowCount > AppSettings.Settings.MaxErrorsInARowBeforeDisable)
                                            {
                                                Log($"...Error: AI URL failed {url.ErrsInRowCount} times in a row.  DISABLING: '{url}'", url.CurSrv, cam);
                                                url.ErrDisabled = true;
                                            }

                                            if (url.ErrsInRowCount >= AppSettings.Settings.deepstack_autorestart_fail_count &&
                                                AppSettings.Settings.deepstack_autostart &&
                                                DeepStackServerControl.IsInstalled &&
                                                DeepStackServerControl.URLS.IndexOf(url.ToString(), StringComparison.OrdinalIgnoreCase) >= 0)

                                            {
                                                if (NextDeepstackRestartTime == DateTime.MinValue)
                                                    NextDeepstackRestartTime = DateTime.Now;

                                                double mins = (DateTime.Now - NextDeepstackRestartTime).TotalMinutes;
                                                double togo = (AppSettings.Settings.deepstack_autorestart_minutes_between_restart_attempts - mins);

                                                if (!DeepStackServerControl.Starting &&
                                                   DateTime.Now >= NextDeepstackRestartTime)
                                                {
                                                    Log($"Error: Locally installed deepstack instance failed {url.ErrsInRowCount} times in a row. (autorestart_fail_count={AppSettings.Settings.deepstack_autorestart_fail_count}) Restarting Deepstack...");
                                                    //dont wait for it
                                                    await DeepStackServerControl.StartDeepstackAsync(true);
                                                    url.ErrsInRowCount = 0;
                                                    NextDeepstackRestartTime = DateTime.Now.AddSeconds(AppSettings.Settings.deepstack_autorestart_minutes_between_restart_attempts);
                                                }
                                                else
                                                {
                                                    Log($"Error: Locally installed deepstack instance failed {url.ErrsInRowCount} times in a row.  (autorestart_fail_count={AppSettings.Settings.deepstack_autorestart_fail_count}) Waiting {togo.ToString("##0.0")} mins before attempting restart...");
                                                }

                                            }

                                            CurImg.RetryCount++;  //even if there was not an error directly accessing the image

                                            if (CurImg.ErrCount <= AppSettings.Settings.MaxQueueItemRetries && CurImg.RetryCount <= AppSettings.Settings.MaxQueueItemRetries)
                                            {
                                                //put back in queue to be processed by another deepstack server
                                                Log($"...Putting image back in queue due to URL '{url}' problem (QueueTime={(DateTime.Now - CurImg.TimeAdded).TotalMinutes.ToString("###0.0")}, Image ErrCount={CurImg.ErrCount}, Image RetryCount={CurImg.RetryCount}, URL ErrCount={url.CurErrCount}): '{CurImg.image_path}', ImageProcessQueue.Count={ImageProcessQueue.Count}", url.CurSrv, cam, CurImg);
                                                ImageProcessQueue.Enqueue(CurImg);
                                            }
                                            else
                                            {
                                                cam.stats_skipped_images++;
                                                cam.stats_skipped_images_session++;
                                                int timems = (int)(DateTime.Now - CurImg.TimeAdded).TotalMilliseconds;

                                                Log($"...Error: Removing image from queue. Image RetryCount={CurImg.RetryCount}, URL ErrCount='{url.CurErrCount}': {url}', Image: '{CurImg.image_path}', ImageProcessQueue.Count={ImageProcessQueue.Count}, Skipped this session={cam.stats_skipped_images_session}", url.CurSrv, cam, CurImg);
                                                Global.CreateHistoryItem(new History().Create(CurImg.image_path, DateTime.Now, cam.Name, $"Skipped image, {CurImg.RetryCount} errors processing.", "", false, "", url.CurSrv, timems, false));

                                            }
                                        }
                                        else
                                        {
                                            ProcImgCnt++;
                                            //reset error count
                                            url.CurErrCount = 0;
                                            url.ErrsInRowCount = 0;
                                        }

                                        url.DecrementQueue();

                                    }
                                    CurRunningDetectTasks.Decrement(0);

                                });

                                sw.Stop();
                                LastThreadInitTimeMS = sw.ElapsedMilliseconds;
                            }
                            else
                            {
                                //Log("No Images left in the queue!");
                                break;
                            }

                        }

                        if (CurRunningDetectTasks > 0)
                        {
                            Log($"Debug: Done adding. {CurRunningDetectTasks} total threads running, LastThreadInitTimeMS={LastThreadInitTimeMS}, ErrCnt={ErrCnt}, ImageProcessQueue.Count={ImageProcessQueue.Count}");
                        }

                        //Clean up old images in the dupe check dic
                        if ((DateTime.Now - LastCleanDupesTime).TotalMinutes >= 60)
                        {
                            int cnt = 0;
                            foreach (KeyValuePair<string, DateTime> kvPair in image_detection_dictionary)
                            {
                                if ((DateTime.Now - kvPair.Value).TotalMinutes >= 30)
                                {   // Remove expired item.
                                    cnt++;
                                    //ClsImageQueueItem removedItem;
                                    image_detection_dictionary.TryRemove(kvPair.Key, out _);
                                }
                            }

                        }

                    }

                    if (MasterCTS.IsCancellationRequested)
                        break;

                    //Only loop 10 times a second conserve cpu
                    await Task.Delay(AppSettings.Settings.loop_delay_ms);
                }

                Log("Debug: ImageQueueLoop canceled.");

            }
            catch (Exception ex)
            {
                //if we get here its the end of the world as we know it
                Log("Error: * '...Human sacrifice, dogs and cats living together – mass hysteria!' * - " + ex.Msg());
            }
        }

        public static void AddImageToQueue(string Filename)
        {
            AddImageToQueue(Filename, null);
        }

        //Additive overload: lets a caller that has already matched its own Camera (e.g. FrigateSource, which maps a Frigate
        //camera name to an AITool Camera via BICamName/Name/Prefix) skip the GetCamera(Filename) lookup and use that match directly.
        //If Cam is null this behaves exactly like AddImageToQueue(string).
        public static void AddImageToQueue(string Filename, Camera Cam)
        {

            using var Trace = new Trace();  //This c# 8.0 using feature will auto dispose when the function is done.

            PlayTick(TickImageAddedPlayer);

            lock (FileWatcherLockObject)
            {
                try
                {
                    //make sure we are not processing a duplicate file...
                    if (image_detection_dictionary.ContainsKey(Filename.ToLower()))
                    {
                        Log("Skipping image because of duplicate Created File Event: " + Filename);
                    }
                    else
                    {
                        Camera cam = Cam ?? GetCamera(Filename, true);
                        if (cam != null)  //only put in queue if we can match to camera (even default)
                        {

                            if (cam.enabled)
                            {
                                if (!(cam.Paused && cam.PauseFileMon))
                                {
                                    //Note:  Interwebz says ConCurrentQueue.Count may be slow for large number of items but I dont think we have to worry here in most cases
                                    int qsize = ImageProcessQueue.Count + 1;
                                    if (qsize > AppSettings.Settings.MaxImageQueueSize)
                                    {
                                        Log("");
                                        Log($"Error: Skipping image because queue ({qsize}) is greater than '{AppSettings.Settings.MaxImageQueueSize}'. (Adjust 'MaxImageQueueSize' in .JSON file if needed): " + Filename, "", cam, Filename);
                                    }
                                    else
                                    {
                                        Log("Debug: ");
                                        Log($"Debug: ====================== Adding new image to queue (Count={ImageProcessQueue.Count + 1}): " + Filename, "", cam, Filename);
                                        ClsImageQueueItem CurImg = new ClsImageQueueItem(Filename, qsize);
                                        image_detection_dictionary.TryAdd(Filename.ToLower(), DateTime.Now);
                                        ImageProcessQueue.Enqueue(CurImg);
                                        scalc.AddToCalc(qsize);
                                        Global.SendMessage(MessageType.ImageAddedToQueue);
                                    }

                                }
                                else
                                {
                                    Log($"Debug: Skipping image because camera '{cam}' file monitoring is PAUSED " + Filename, "", cam, Filename);
                                }

                            }
                            else
                            {
                                Log($"Debug: Skipping image because camera '{cam}' is DISABLED " + Filename, "", cam, Filename);
                            }
                        }
                        else
                        {
                            Log("Error: Skipping image because no camera found for new image " + Filename, "", cam, Filename);
                        }


                    }

                }
                catch (Exception ex)
                {
                    Log("Error: " + ex.Msg());
                }

            }


        }
    }
}
