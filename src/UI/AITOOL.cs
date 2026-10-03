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
        // =============================================================
        // ALL FUNCTIONS HERE THAT MAY EVENTUALLY BE USED IN A SERVICE
        // NO direct UI interaction
        // =============================================================

        public static DeepStack DeepStackServerControl = null;
        //public static RichTextBoxEx RTFLogger = null;
        //public static LogFileWriter LogWriter = null;
        //public static LogFileWriter HistoryWriter = null;

        public static BlueIrisInfo BlueIrisInfo = null;
        //public static List<ClsURLItem> DeepStackURLList = new List<ClsURLItem>();

        //keep track of timing
        //moving average will be faster for long running process with 1000's of samples
        public static MovingCalcs tcalc = new MovingCalcs(500, "Images", true);
        public static MovingCalcs fcalc = new MovingCalcs(500, "Images", true);
        public static MovingCalcs lcalc = new MovingCalcs(500, "Images", true);
        public static MovingCalcs icalc = new MovingCalcs(500, "Images", true);
        public static MovingCalcs qcalc = new MovingCalcs(500, "Images", true);
        public static MovingCalcs scalc = new MovingCalcs(500, "Img Queue", false);

        //public static ClsLogManager errors = new ClsLogManager();

        public static ClsLogManager LogMan = null;

        public static ClsFaceManager FaceMan = null;

        public static ConcurrentQueue<ClsImageQueueItem> ImageProcessQueue = new ConcurrentQueue<ClsImageQueueItem>();

        public static ConcurrentQueue<ClsLogItm> TmpHistQueue = new ConcurrentQueue<ClsLogItm>();  //For before the logger gets fully initialized

        //The sqlite db connection
        public static SQLiteHistory HistoryDB = null;
        public static ClsTriggerActionQueue TriggerActionQueue = null;

        public static object FileWatcherLockObject = new object();
        public static object ImageLoopLockObject = new object();

        //thread safe dictionary to prevent more than one file being processed at one time
        public static ConcurrentDictionary<string, DateTime> image_detection_dictionary = new ConcurrentDictionary<string, DateTime>();


        public static Dictionary<string, ClsFileSystemWatcher> watchers = new Dictionary<string, ClsFileSystemWatcher>();
        //public static ThreadSafe.Boolean AIURLSettingsChanged = new ThreadSafe.Boolean(true);


        public static ThreadSafe.Boolean IsClosing = new ThreadSafe.Boolean(false);
        public static ThreadSafe.Boolean IsLoading = new ThreadSafe.Boolean(true);
        public static ThreadSafe.DateTime LastImageBackupTime = new ThreadSafe.DateTime(DateTime.MinValue, AppSettings.Settings.DateFormat);

        public static CancellationTokenSource MasterCTS = new CancellationTokenSource();

        public static System.Timers.Timer FileSystemErrorCheckTimer = new System.Timers.Timer();

        public static MQTTClient mqttClient = new MQTTClient();

        public static Pushover pushoverClient = null;

        public static ClsTelegramMessages Telegram = null;
        public static ThrottledHttpClient triggerHttpClient = null;

        public static string srv = "";

        public static ThreadSafe.Integer AIURLListAvailableRefineServerCount = new ThreadSafe.Integer(0);

        public static ThreadSafe.Boolean CloseImmediately = new ThreadSafe.Boolean(false);

        public static ThreadSafe.Boolean ResetSettings = new ThreadSafe.Boolean(false);

        public static ThreadSafe.Boolean Restart = new ThreadSafe.Boolean(false);

        public static SoundPlayer TickImageAddedPlayer = null;

        public static async Task InitializeBackend()
        {

            try
            {
                using var Trace = new Trace();  //This c# 8.0 using feature will auto dispose when the function is done.



                //initialize log manager with basic settings so we can start getting output if needed
                if (Global.IsService)
                    srv = ".SERVICE.";
                else
                    srv = ".";

                string exe = $"AITOOLS{srv}EXE";

                //initialize logging as early as we can...  Write to the temp folder since we dont know the log location yet
                int TempDefSize = ((1024 * 1024) * 20); //20mb
                LogMan = new ClsLogManager(!Global.IsService, exe);

                await LogMan.UpdateNLog(LogLevel.Debug, Path.Combine(Global.GetTempFolder(true), Path.GetFileNameWithoutExtension(Assembly.GetEntryAssembly().Location) + $"{srv}LOG"), TempDefSize, 120, AppSettings.Settings.MaxGUILogItems);

                //load settings
                await AppSettings.LoadAsync();

                //reset log settings if different:
                await LogMan.UpdateNLog(LogLevel.FromString(AppSettings.Settings.LogLevel), AppSettings.Settings.LogFileName, AppSettings.Settings.MaxLogFileSize, AppSettings.Settings.MaxLogFileAgeDays, AppSettings.Settings.MaxGUILogItems);


                Assembly CurAssm = Assembly.GetEntryAssembly();
                string AssemNam = CurAssm.GetName().Name;
                string AssemVer = CurAssm.GetName().Version.ToString();

                Log("");
                Log("");
                Log("");
                Log($"Starting {AssemNam} Version {AssemVer} built on {Global.RetrieveLinkerTimestamp()}");

                try  //just in case some weird issue comes up with older os version...
                {
                    OSVersionExt.VersionInfo vi = OSVersion.GetOSVersion();
                    OSVersionExtension.OperatingSystem ov = OSVersion.GetOperatingSystem();

                    Log($"Debug:   Installed NET Framework version '{Global.GetFrameworkVersion()}', Target version '{AppDomain.CurrentDomain.SetupInformation.TargetFrameworkName}'");
                    Log($"Debug:   Windows '{ov.ToString()}', version '{vi.Version.ToString()}' Release ID '{OSVersion.MajorVersion10Properties().ReleaseId}', 64Bit={OSVersion.Is64BitOperatingSystem}, Workstation={OSVersion.IsWorkstation}, Server={OSVersion.IsServer}, SERVICE={Global.IsService}");

                }
                catch (Exception ex)
                {

                    Log("Error: Problem getting OS version: " + ex.Msg());
                }


                if (AppSettings.AlreadyRunning)
                {
                    Log("*** Warning: Another instance is already running *** ");
                    Log(" --- Files will not be monitored from within this session ");
                    Log(" --- Log tab will not display output from service instance. You will need to directly open log file for that ");
                    Log(" --- Changes made here to settings will require that you stop/start the service ");
                }
                if (Global.IsAdministrator())
                {
                    Log("Debug: *** Running as administrator ***");
                }
                else
                {
                    Log("Debug: Not running as administrator.");
                }

                if (string.Equals(AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\'), Directory.GetCurrentDirectory().TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                {
                    Log($"Debug: *** Start in/current directory is the same as where the EXE is running from: {Directory.GetCurrentDirectory()} ***");
                }
                else
                {
                    try
                    {
                        Log($"Debug: *** Changing Start in/current directory from '{Directory.GetCurrentDirectory().TrimEnd('\\')}' to '{AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\')}' ***");
                        Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
                    }
                    catch (Exception ex)
                    {

                        string msg = $"Error: The Start in/current directory is NOT the same as where the EXE is running from: \r\n{Directory.GetCurrentDirectory()}\r\n{AppDomain.CurrentDomain.BaseDirectory}";
                        Log(msg);
                        Log($"...this may prevent DLL files from loading from the wrong folder.  '{ex.Message}'");
                    }
                }

                Global.SaveRegSetting("LastRunPath", Directory.GetCurrentDirectory());

                //initialize blueiris info class to get camera names, clip paths, etc
                BlueIrisInfo = new BlueIrisInfo();

                await BlueIrisInfo.RefreshBIInfoAsync(AppSettings.Settings.BlueIrisServer);

                if (BlueIrisInfo.Result == BlueIrisResult.Valid)
                {
                    Log($"Debug: BlueIris path is '{BlueIrisInfo.AppPath}', with {BlueIrisInfo.Users.Count} users, {BlueIrisInfo.Cameras.Count} cameras and {BlueIrisInfo.ClipPaths.Count} clip folder paths configured.");
                    if (BlueIrisInfo.Users.Count > 0 && (string.IsNullOrEmpty(AppSettings.Settings.DefaultUserName) || string.Equals(AppSettings.Settings.DefaultUserName, "username", StringComparison.OrdinalIgnoreCase)))
                    {
                        AppSettings.Settings.DefaultUserName = BlueIrisInfo.Users[0].Name;
                        AppSettings.Settings.DefaultPasswordEncrypted = BlueIrisInfo.Users[0].Password.Encrypt();
                    }

                    UpdateLatLong();
                }
                else
                {
                    Log($"Debug: BlueIris not detected.");
                }


                //initialize the deepstack class - it collects info from running deepstack processes, detects install location, and
                //allows for stopping and starting of its service
                DeepStackServerControl = new DeepStack(AppSettings.Settings.deepstack_adminkey, AppSettings.Settings.deepstack_apikey, AppSettings.Settings.deepstack_mode, AppSettings.Settings.deepstack_sceneapienabled, AppSettings.Settings.deepstack_faceapienabled, AppSettings.Settings.deepstack_detectionapienabled, AppSettings.Settings.deepstack_port, AppSettings.Settings.deepstack_customModelPath, AppSettings.Settings.deepstack_stopbeforestart, AppSettings.Settings.deepstack_customModelName, AppSettings.Settings.deepstack_customModelPort, AppSettings.Settings.deepstack_customModelMode, AppSettings.Settings.deepstack_customModelApiEnabled);

                if (DeepStackServerControl.IsInstalled && AppSettings.Settings.deepstack_autostart)
                {
                    await DeepStackServerControl.StartDeepstackAsync();
                }

                //Load the database, and migrate any old csv lines if needed
                HistoryDB = new SQLiteHistory(AppSettings.Settings.HistoryDBFileName, AppSettings.AlreadyRunning);

                await HistoryDB.Initialize();

                TriggerActionQueue = new ClsTriggerActionQueue();

                //Headless/service friendly on purpose - doesn't depend on Shell/WinForms, so it also comes up when running as a service.
                WebDashboardServer.Start();

                await UpdateWatchers(false);

                FileSystemErrorCheckTimer.Elapsed += new System.Timers.ElapsedEventHandler(TimerCheckFileSystemWatchers);
                FileSystemErrorCheckTimer.Interval = AppSettings.Settings.FileSystemWatcherRetryOnErrorTimeMS;
                FileSystemErrorCheckTimer.Enabled = true;
                FileSystemErrorCheckTimer.Start();

                //Start the thread that watches for the file queue
                if (!AppSettings.AlreadyRunning)
                    Task.Run(ImageQueueLoop);


                Telegram = new ClsTelegramMessages();

                if (AppSettings.LastShutdownState.StartsWith("checkpoint") && !AppSettings.AlreadyRunning)
                    Log($"Error: Program did not shutdown gracefully.  Last log entry was '{AppSettings.LastLogEntry}', '{AppSettings.LastShutdownState}'");

                //initialize the annoying tick player
                if (AppSettings.Settings.Tick && AppSettings.Settings.TickImageAddedSoundFile.IsNotEmpty())
                {
                    string soundfile = Global.FindSoundFile(AppSettings.Settings.TickImageAddedSoundFile);
                    if (File.Exists(soundfile))
                    {
                        TickImageAddedPlayer = new SoundPlayer(soundfile);
                        TickImageAddedPlayer.Load();
                    }
                    else
                    {
                        Log($"Debug: Could not find tick sound file {AppSettings.Settings.TickImageAddedSoundFile}.");
                    }
                }

            }
            catch (Exception ex)
            {

                Log("Error: " + ex.Msg());
            }

        }

        public static void UpdateLatLong()
        {
            //use blueiris lat/long setting if found, and not already set to something different in :
            if (BlueIrisInfo.Result == BlueIrisResult.Valid &&
                !Global.IsLatLongValid(AppSettings.Settings.LocalLatitude, AppSettings.Settings.LocalLongitude) &&
                Global.IsLatLongValid(BlueIrisInfo.Latitude, BlueIrisInfo.Longitude))  //default is middle of USA, assume that is not a valid lat/long
            {
                AppSettings.Settings.LocalLatitude = BlueIrisInfo.Latitude;
                AppSettings.Settings.LocalLongitude = BlueIrisInfo.Longitude;
            }



        }
        //public static void Log(string Detail, string AIServer = "", Camera Camera = null, ClsImageQueueItem Image = null, string Source = "", int Depth = 0, LogLevel Level = null, Nullable<DateTime> Time = default(DateTime?), [CallerMemberName()] string memberName = null)
        //{
        //    string cam = Camera != null ? Camera.Name : "";
        //    string img = Image != null ? Image.image_path : "";
        //    Log(Detail, AIServer, cam, img, Source, Depth, Level, Time, memberName);
        //}
        //public static void Log(string Detail, bool fakeout = false, [CallerMemberName()] string memberName = null)
        //{

        //    Log(Detail, "", "", "", "", 0, null, null, memberName);
        //}

        //just an alias to make things easier
        [DebuggerStepThrough]
        public static void Log(string Detail, string AIServer = "", object Camera = null, object Image = null, string Source = "", int Depth = 0, LogLevel Level = null, Nullable<DateTime> Time = default(DateTime?), [CallerMemberName()] string memberName = null)
        {

            string cam = "";
            string img = "";

            if (Camera != null)
            {
                if (Camera is Camera)
                {
                    cam = ((Camera)Camera).Name;
                }
                else if (Camera is String)
                {
                    cam = Camera.ToString();
                }
                else
                {
                    cam = Camera.ToString();
                    //should not be here?
                }
            }

            if (Image != null)
            {
                if (Image is ClsImageQueueItem)
                {
                    img = ((ClsImageQueueItem)Image).image_path;
                }
                else if (Image is string)
                {
                    img = Image.ToString();
                }
                else
                {
                    img = Image.ToString();
                }
            }


            if (LogMan != null && LogMan.Enabled)
            {
                //flush any entries from before logman initialized
                while (!TmpHistQueue.IsEmpty)
                {
                    ClsLogItm cli;
                    if (TmpHistQueue.TryDequeue(out cli))
                        LogMan.Log(cli.Detail, cli.AIServer, cli.Camera, cli.Image, cli.Source, cli.Depth, cli.Level, cli.Date, cli.Func);
                }

                LogMan.Log(Detail, AIServer, cam, img, Source, Depth, Level, Time, memberName);
            }
            else
            {
                TmpHistQueue.Enqueue(new ClsLogItm(null, DateTime.Now, Source, memberName, AIServer, cam, img, Detail, 0, Depth, "", 0));
                //Console.WriteLine($"Error: Wrote to log before initialized? '{Detail}'");
            }
        }



    }
}
