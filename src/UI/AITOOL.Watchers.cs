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

        //EVENT: new image added to input_path -> START AI DETECTION
        private static void OnCreated(object source, FileSystemEventArgs e)
        {
            AddImageToQueue(e.FullPath);
        }
        //event: image in input_path renamed
        private static void OnRenamed(object source, RenamedEventArgs e)
        {
            Global.DeleteHistoryItem(e.OldFullPath);
        }

        //event: image in input path deleted
        private static void OnDeleted(object source, FileSystemEventArgs e)
        {
            Global.DeleteHistoryItem(e.FullPath);
        }

        private static void OnError(object sender, System.IO.ErrorEventArgs e)
        {
            //Too many changes at once in directory:C:\BlueIris\aiinput.
            //File watcher  The specified network name is no longer available
            string path = ((FileSystemWatcher)sender).Path;
            Log("Error: File watcher error: " + e.GetException().Message + $" on path '{path}'");
            UpdateWatchers(true);

        }

        public static void TimerCheckFileSystemWatchers(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (FileWatcherHasError)
            {
                Log($"Debug: Re-checking bad File System Watcher Paths ('FileSystemWatcherRetryOnErrorTimeMS' = {AppSettings.Settings.FileSystemWatcherRetryOnErrorTimeMS}ms)...");
                UpdateWatchers(true);
            }
        }

        public static SemaphoreSlim Semaphore_Watcher_Updating = new SemaphoreSlim(1, 1);

        public static async Task UpdateWatchers(bool Reset)
        {
            await Semaphore_Watcher_Updating.WaitAsync();

            using var Trace = new Trace();  //This c# 8.0 using feature will auto dispose when the function is done.

            try
            {
                if (AppSettings.AlreadyRunning)
                {
                    Log("*** Another instance is already running, skip watching for changed files ***");
                    return;
                }

                FileWatcherHasError = false;

                Global.UpdateProgressBar($"Updating watched folders'...", 1, 1, 1);

                //first add all the names and paths to check...
                List<string> names = new List<string>();
                Dictionary<string, string> paths = new Dictionary<string, string>();

                string pths = AppSettings.Settings.input_path.Trim().TrimEnd(@"\".ToCharArray());
                names.Add($"INPUT_PATH|{pths}|{AppSettings.Settings.input_path_includesubfolders}");
                foreach (Camera cam in AppSettings.Settings.CameraList)
                {
                    if (cam.enabled && !String.IsNullOrWhiteSpace(cam.input_path))
                    {
                        pths = cam.input_path.Trim().TrimEnd(@"\".ToCharArray());
                        names.Add($"{cam.Name}|{pths}|{cam.input_path_includesubfolders}");
                    }
                }

                if (Reset)
                {
                    foreach (ClsFileSystemWatcher watcher1 in watchers.Values)
                    {
                        if (watcher1 != null && watcher1.watcher != null)
                        {
                            try
                            {
                                watcher1.watcher.EnableRaisingEvents = false;
                                watcher1.watcher.Dispose();
                                watcher1.watcher = null;
                            }
                            catch (Exception ex)
                            {

                                Log($"Error: Failed to reset/clear watcher for folder '{watcher1.Name}' - {watcher1.Path}: {ex.Message}");
                            }
                        }
                    }
                    watchers.Clear();
                }

                //check each one to see if needs to be added
                foreach (string item in names)
                {
                    List<string> splt = item.SplitStr("|", false);
                    string name = splt[0];
                    string path = splt[1];
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        bool include = Convert.ToBoolean(splt[2]);
                        if (!paths.ContainsKey(path.ToLower()))
                        {
                            paths.Add(path.ToLower(), path);

                            if (!watchers.ContainsKey(name.ToLower()))
                            {
                                //this will return null if the path is invalid...
                                FileSystemWatcher curwatch = await CreateFileWatcherAsync(path, include);
                                if (curwatch != null)
                                {
                                    ClsFileSystemWatcher mywtc = new ClsFileSystemWatcher(name, path, curwatch, include);
                                    //add even if null to keep track of things
                                    watchers.Add(name.ToLower(), mywtc);
                                }
                            }
                            else
                            {
                                //update path if needed, even to empty
                                watchers[name.ToLower()].Path = path;
                                if (watchers[name.ToLower()].watcher == null)
                                {
                                    //could be null if path is bad
                                    watchers[name.ToLower()].watcher = await CreateFileWatcherAsync(path, include);
                                }
                            }
                        }
                        else
                        {
                            Log($"Debug: Skipping duplicate path for '{name}': '{path}'");
                        }

                    }


                }

                //check to see if any need disabling - a camera was deleted
                foreach (ClsFileSystemWatcher watcher1 in watchers.Values)
                {
                    bool fnd = false;
                    foreach (string item in names)
                    {
                        List<string> splt = item.SplitStr("|");
                        string name = splt[0];
                        if (string.Equals(name, watcher1.Name, StringComparison.OrdinalIgnoreCase))
                        {
                            fnd = true;
                            break;
                        }
                    }
                    if (!fnd)
                    {
                        watcher1.Path = "";
                    }

                }


                //enable or disable watchers
                int enabledcnt = 0;
                int disabledcnt = 0;

                Dictionary<string, string> dupes = new Dictionary<string, string>();

                foreach (ClsFileSystemWatcher watcher in watchers.Values)
                {
                    if (watcher.watcher != null)
                    {
                        if (!String.IsNullOrWhiteSpace(watcher.Path))
                        {
                            if (!dupes.ContainsKey(watcher.Path.ToLower()))
                            {
                                if (watcher.Path != watcher.watcher.Path)
                                {
                                    watcher.watcher.Path = watcher.Path;
                                    Log($"Debug: Watcher '{watcher.Name}' changed from '{watcher.watcher.Path}' to '{watcher.Path}'.");
                                }

                                if (watcher.IncludeSubdirectories != watcher.watcher.IncludeSubdirectories)
                                {
                                    watcher.watcher.IncludeSubdirectories = watcher.IncludeSubdirectories;
                                    Log($"Debug: Watcher '{watcher.Name}' IncludeSubdirectories changed from '{watcher.watcher.IncludeSubdirectories}' to '{watcher.IncludeSubdirectories}'.");
                                }

                                if (watcher.watcher.EnableRaisingEvents != true)
                                {
                                    enabledcnt++;
                                    watcher.watcher.EnableRaisingEvents = true;
                                    dupes.Add(watcher.Path.ToLower(), watcher.Path);
                                    Log($"Debug: Watcher '{watcher.Name}' is now watching '{watcher.Path}'");
                                }

                            }
                            else
                            {
                                Log($"Debug: Watcher '{watcher.Name}' has a duplicate path, skipping '{watcher.Path}'");
                            }
                        }
                        else
                        {
                            //make sure it is disabled
                            disabledcnt++;

                            watcher.watcher.EnableRaisingEvents = false;
                            watcher.watcher.Dispose();
                            watcher.watcher = null;
                            Log($"Debug: Watcher '{watcher.Name}' has an empty path, just disabled.");
                        }

                    }
                    else if (!string.IsNullOrEmpty(watcher.Path))
                    {
                        Log($"Error: Watcher '{watcher.Name}' disabled. INVALID PATH='{watcher.Path}'");
                    }
                    else
                    {
                        //Log($"Watcher '{watcher.Name}' already disabled.");
                    }


                }

                if (watchers.Count == 0)
                {
                    Log("Debug: No FileSystemWatcher input folders defined yet.");
                }
                else
                {
                    if (enabledcnt == 0)
                    {
                        Log("Debug: No NEW FileSystemWatcher input folders found.");
                    }
                    else
                    {
                        Log($"Debug: Enabled {enabledcnt} FileSystemWatchers.");
                    }
                }


            }
            catch (Exception ex)
            {
                FileWatcherHasError = true;
                Log($"Error: {ex.Msg()}");
            }
            finally
            {
                Semaphore_Watcher_Updating.Release();
            }

        }

        static ThreadSafe.Boolean FileWatcherHasError = new ThreadSafe.Boolean(false);

        public static async Task<FileSystemWatcher> CreateFileWatcherAsync(string path, bool IncludeSubdirectories = false, string filter = "*.jpg")
        {
            using var Trace = new Trace();  //This c# 8.0 using feature will auto dispose when the function is done.

            FileSystemWatcher watcher = null;

            try
            {
                // Be aware: https://stackoverflow.com/questions/1764809/filesystemwatcher-changed-event-is-raised-twice

                if (!String.IsNullOrWhiteSpace(path))
                {
                    Stopwatch sw = Stopwatch.StartNew();

                    if (await Global.DirectoryExistsAsync(path, 10000))
                    {
                        watcher = new FileSystemWatcher(path);
                        watcher.Path = path;
                        watcher.Filter = filter;
                        watcher.IncludeSubdirectories = IncludeSubdirectories;
                        watcher.InternalBufferSize = 65536;  //defaults to 8k, we are going max it out to try to prevent "too many changes at once in directory"

                        //The 'default' is the bitwise OR combination of NotifyFilters.LastWrite | NotifyFilters.LastAccess | NotifyFilters.CreationTime | NotifyFilters.FileName | NotifyFilters.DirectoryName
                        watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName;

                        //fswatcher events
                        watcher.Created += new FileSystemEventHandler(OnCreated);
                        watcher.Renamed += new RenamedEventHandler(OnRenamed);
                        watcher.Deleted += new FileSystemEventHandler(OnDeleted);
                        watcher.Error += new ErrorEventHandler(OnError);

                    }
                    else
                    {
                        FileWatcherHasError = true;
                        Log($"Error: Path does not exist. Time={sw.ElapsedMilliseconds}ms: " + path);
                    }
                }
            }
            catch (Exception ex)
            {
                FileWatcherHasError = true;
                Log($"Error: {ex.Msg()}");
            }

            return watcher;
        }
    }
}
