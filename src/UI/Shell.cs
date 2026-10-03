using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Media;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

using BrightIdeasSoftware;

using Microsoft.WindowsAPICodePack.Dialogs;

using Newtonsoft.Json; //deserialize DeepquestAI response

using NLog;

using static AITool.AITOOL;
using static AITool.Global;

namespace AITool
{

    public partial class Shell:Form
    {
        private ThreadSafe.DateTime LastListUpdate = new ThreadSafe.DateTime(DateTime.MinValue, AppSettings.Settings.DateFormat);

        private ThreadSafe.Boolean DatabaseInitialized = new ThreadSafe.Boolean(false);

        private ThreadSafe.Boolean IsHistoryListUpdating = new ThreadSafe.Boolean(false);
        private ThreadSafe.Boolean IsLogListUpdating = new ThreadSafe.Boolean(false);
        private ThreadSafe.Boolean IsStatsUpdating = new ThreadSafe.Boolean(false);

        //Dictionary<string, History> HistoryDic = new Dictionary<string, History>();
        //private ThreadSafe.Boolean FilterChanged = new ThreadSafe.Boolean(true);

        //Instantiate a Singleton of the Semaphore with a value of 1. This means that only 1 thread can be granted access at a time.
        public static SemaphoreSlim Semaphore_List_Updating = new SemaphoreSlim(1, 1);

        //public static ConcurrentQueue<History> AddedHistoryItems = new ConcurrentQueue<History>();
        //public static ConcurrentQueue<History> DeletedHistoryItems = new ConcurrentQueue<History>();

        //for searching log tab:
        System.Timers.Timer tmr;
        DateTime TimeSinceType = DateTime.MinValue;

        FrmSplash SplashScreen = null;

        Stopwatch StartupSW = null;




        public Shell()
        {


            this.StartupSW = Stopwatch.StartNew();

            this.InitializeComponent();

            using var Trace = new Trace();  //This c# 8.0 using feature will auto dispose when the function is done.

            //this is to log and status messages from other classes...
            Global.progress = new Progress<ClsMessage>(this.EventMessage);

            this.SplashScreen = new FrmSplash();

            if (Debugger.IsAttached)
                this.SplashScreen.Hide();
            else
                this.SplashScreen.Show();

            HideForm();

            Application.DoEvents();
            Debug.Print("Init tid=" + Thread.CurrentThread.ManagedThreadId);

        }


        private async void Shell_Load(object sender, EventArgs e)
        {
            //PlayOOG("D:\\TEMP\\AwACAgEAAxkBAAI7OGGhP0AhP-aBlubv3Vbq4OahTbkAAy4CAAJ59AlFMssC4x5Yy3wiBA.ogg");

            //using var synth = new SpeechSynthesizer();
            //foreach (InstalledVoice voice in synth.GetInstalledVoices())
            //{
            //    VoiceInfo info = voice.VoiceInfo;
            //    Console.WriteLine(" Voice Name: " + info.Name);
            //}
            //string test = CompressToBase64String(" er 2 35464353 4t f fg wefg wer gwe gw t345 y4 t4 rtw w egfgkfkkkk");
            //bool isbase = IsBase64String(test);
            //TimeSpan ts = TimeSpan.FromDays(1.2);
            //Debug.Print(ts.FormatTS(true));
            //Debug.Print(ts);

            //Global.IsTimeBetween(new DateTime(2021, 6, 13, 06, 00, 00), "dusk-dawn");
            //Global.IsTimeBetween(new DateTime(2021, 6, 13, 23, 00, 00), "dusk-dawn");
            //Global.IsTimeBetween(new DateTime(2021, 6, 13, 07, 00, 00), "dusk-dawn");
            //Global.IsTimeBetween(new DateTime(2021, 6, 13, 21, 00, 00), "dusk-dawn");

            //Global.IsTimeBetween(new DateTime(2021, 6, 13, 06, 00, 00), "dawn-dusk");
            //Global.IsTimeBetween(new DateTime(2021, 6, 13, 23, 00, 00), "dawn-dusk");
            //Global.IsTimeBetween(new DateTime(2021, 6, 13, 07, 00, 00), "dawn-dusk");
            //Global.IsTimeBetween(new DateTime(2021, 6, 13, 21, 00, 00), "dawn-dusk");

            //Rectangle img = new Rectangle(0, 0, 3840, 2160);
            //Rectangle prd = new Rectangle(0, 0, 155, 204);  // .38% of the original image
            //Rectangle prd = new Rectangle(0, 0, img.Width / 2, img.Height);  //half of the original image = 50%
            //Rectangle prd = new Rectangle(0, 0, img.Width, img.Height / 2);  //half of the original image = 50%
            //Rectangle prd = new Rectangle(0, 0, img.Width / 2, img.Height / 2);  //a quarter of the original image = 25%

            //double percent = img.PercentOfSize(prd);

            Debug.Print("load tid=" + Thread.CurrentThread.ManagedThreadId);

            //Uri pth = new Uri("\\\\[2600:6c64:6b7f:f8d8::1d4]\\c$");

            //ClsDoodsRequest cdr = new ClsDoodsRequest();

            //cdr.Detect.MinPercentMatch = 50;

            //string testjson = JsonConvert.SerializeObject(cdr);


            using var cw = new Global_GUI.CursorWait(true);

            //---------------------------------------------------------------------------
            //INITIALIZE HISTORY DB, load settings, ETC

            if (AppSettings.AlreadyRunning && !Global.IsService)
            {
                if (MessageBox.Show("AITOOL.EXE is already running.  If you need to modify settings:\r\n\r\n1) Close this copy.\r\n2) Stop the other service/instance. \r\n3) Restart AITOOL.\r\n\r\nCLOSE THIS COPY?", "Already running", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                {
                    this.SplashScreen.Close();
                    this.SplashScreen = null;
                    CloseImmediately = true;
                    Application.Exit();
                }
            }

            Stopwatch sw = Stopwatch.StartNew();

            await AITOOL.InitializeBackend();

            await FrigateSource.StartAsync();


            //Camera testcam = GetCamera("c:\\test\\CAMNAME.1.123456.jpg");

            //ClsImageQueueItem cli = new ClsImageQueueItem("C:\\Downloads\\TestImage.jpg", 0);
            //NPushover.RequestObjects.Message msg = new NPushover.RequestObjects.Message();
            //msg.Timestamp = DateTime.Now;
            //msg.Title = "Test title";
            //msg.Body = "Some message";

            //AITOOL.pushoverClient = new NPushover.Pushover(AppSettings.Settings.pushover_APIKey);
            //NPushover.ResponseObjects.PushoverUserResponse usr = await AITOOL.pushoverClient.SendPushoverMessageAsync(msg, "", cli);

            Log($"Debug: Back end initialization completed in {sw.ElapsedMilliseconds}ms.");


            //since async stuff continues on another thread, we have to do this:

            Global_GUI.InvokeIFRequired(this, () =>
            {
                //---------------------------------------------------------------------------
                //HISTORY TAB
                Global_GUI.ConfigureFOLV(this.folv_history, typeof(History), new Font("Segoe UI", (float)9.75, FontStyle.Regular), this.HistoryImageList, GridLines: false);
                //this.folv_history.EmptyListMsg = "Initializing database";
                this.cb_showMask.Checked = AppSettings.Settings.HistoryShowMask;
                this.cb_showObjects.Checked = AppSettings.Settings.HistoryShowObjects;
                this.cb_follow.Checked = AppSettings.Settings.HistoryFollow;
                this.automaticallyRefreshToolStripMenuItem.Checked = AppSettings.Settings.HistoryAutoRefresh;
                this.storeFalseAlertsToolStripMenuItem.Checked = AppSettings.Settings.HistoryStoreFalseAlerts;
                this.storeMaskedAlertsToolStripMenuItem.Checked = AppSettings.Settings.HistoryStoreMaskedAlerts;
                this.showOnlyRelevantObjectsToolStripMenuItem.Checked = AppSettings.Settings.HistoryOnlyDisplayRelevantObjects;
                this.restrictThresholdAtSourceToolStripMenuItem.Checked = AppSettings.Settings.HistoryRestrictMinThresholdAtSource;
                this.mergeDuplicatePredictionsToolStripMenuItem.Checked = AppSettings.Settings.HistoryMergeDuplicatePredictions;
                this.cb_filter_animal.Checked = AppSettings.Settings.HistoryFilterAnimals;
                this.cb_filter_masked.Checked = AppSettings.Settings.HistoryFilterMasked;
                this.cb_filter_nosuccess.Checked = AppSettings.Settings.HistoryFilterNoSuccess;
                this.cb_filter_person.Checked = AppSettings.Settings.HistoryFilterPeople;
                this.cb_filter_skipped.Checked = AppSettings.Settings.HistoryFilterSkipped;
                this.cb_filter_success.Checked = AppSettings.Settings.HistoryFilterRelevant;
                this.cb_filter_vehicle.Checked = AppSettings.Settings.HistoryFilterVehicles;
                this.HistoryUpdateListTimer.Interval = AppSettings.Settings.TimeBetweenListRefreshsMS;

                //---------------------------------------------------------------------------
                //CAMERAS TAB

                Global_GUI.ConfigureFOLV(this.FOLV_Cameras, typeof(Camera), new Font("Segoe UI", (float)9.75, FontStyle.Regular), CameraImageList, GridLines: false);


                this.comboBox_filter_camera.SelectedIndex = this.comboBox_filter_camera.FindStringExact("All Cameras"); //select all cameras entry

                //---------------------------------------------------------------------------
                //SETTINGS TAB

                this.LoadSettingsTab();

                //---------------------------------------------------------------------------
                //STATS TAB
                this.comboBox1.Items.Add("All Cameras"); //add all cameras stats entry
                this.comboBox1.SelectedIndex = this.comboBox1.FindStringExact("All Cameras"); //select all cameras entry


                //---------------------------------------------------------------------------
                //Deepstack server TAB


                if (!DeepStackServerControl.IsInstalled)
                {
                    // DeepStack is unmaintained; only show its process-manager tab when it is actually installed
                    // (or the user forces it with ShowDeepStackTab in the settings JSON)
                    if (!AppSettings.Settings.ShowDeepStackTab && this.tabControl1.TabPages.Contains(this.tabDeepStack))
                    {
                        Log("Debug: Hiding DeepStack tab since DeepStack for Windows is not installed. Set 'ShowDeepStackTab' to true in AITOOL.Settings.JSON to show it.");
                        this.tabControl1.TabPages.Remove(this.tabDeepStack);
                    }
                }
                else
                {
                    if (DeepStackServerControl.NeedsSaving)
                    {
                        //this may happen if the already running instance has a different port, etc, so we update the config
                        this.SaveDeepStackTabAsync();
                    }
                    this.LoadDeepStackTab();
                }

                //---------------------------------------------------------------------------
                //LOG TAB

                Global_GUI.ConfigureFOLV(this.folv_log, typeof(ClsLogItm), null, null, GridLines: false);

                this.UpdateLogAddedRemovedAsync(true);
                this.LogUpdateListTimer.Interval = AppSettings.Settings.TimeBetweenListRefreshsMS;

                if (this.toolStripButtonPauseLog.Checked)
                {
                    this.LogUpdateListTimer.Enabled = false;
                    this.LogUpdateListTimer.Stop();
                }
                else
                {
                    this.LogUpdateListTimer.Enabled = true;
                    this.LogUpdateListTimer.Start();
                }

                this.UpdateLogAddedRemovedAsync(true);

                this.tmr = new System.Timers.Timer();
                this.tmr.Interval = 300;
                this.tmr.Elapsed += new System.Timers.ElapsedEventHandler(this.tmr_Elapsed);
                this.tmr.Stop();

                this.ToolStripComboBoxSearch.Text = Global.GetRegSetting("SearchText", "");
                this.mnu_Filter.Checked = AppSettings.Settings.log_mnu_Filter;
                this.mnu_Highlight.Checked = AppSettings.Settings.log_mnu_Highlight;

                if (string.Equals(AppSettings.Settings.LogLevel, "off", StringComparison.OrdinalIgnoreCase))
                {
                    this.mnu_log_filter_off.Checked = true;
                }
                else if (string.Equals(AppSettings.Settings.LogLevel, "fatal", StringComparison.OrdinalIgnoreCase))
                {
                    this.mnu_log_filter_fatal.Checked = true;
                }
                else if (string.Equals(AppSettings.Settings.LogLevel, "error", StringComparison.OrdinalIgnoreCase))
                {
                    this.mnu_log_filter_error.Checked = true;
                }
                else if (string.Equals(AppSettings.Settings.LogLevel, "warn", StringComparison.OrdinalIgnoreCase))
                {
                    this.mnu_log_filter_warn.Checked = true;
                }
                else if (string.Equals(AppSettings.Settings.LogLevel, "info", StringComparison.OrdinalIgnoreCase))
                {
                    this.mnu_log_filter_info.Checked = true;
                }
                else if (string.Equals(AppSettings.Settings.LogLevel, "debug", StringComparison.OrdinalIgnoreCase))
                {
                    this.mnu_log_filter_debug.Checked = true;
                }
                else if (string.Equals(AppSettings.Settings.LogLevel, "trace", StringComparison.OrdinalIgnoreCase))
                {
                    this.mnu_log_filter_trace.Checked = true;
                }

                folv_log.MouseWheel += ListScrollZoom;
                folv_history.MouseWheel += ListScrollZoom;

                //---------------------------------------------------------------------------
                // finish up

                string AssemVer = Assembly.GetExecutingAssembly().GetName().Version.ToString();
                this.lbl_version.Text = $"Version {AssemVer} built on {Global.RetrieveLinkerTimestamp()}";

                //---------------------------------------------------------------------------------------------------------

                IsLoading = false;

                this.Resize += new System.EventHandler(this.Form1_Resize); //resize event to enable 'minimize to tray'

                Log($"{{yellow}}APP START complete.  Initialized in {this.StartupSW.Elapsed.TotalSeconds.ToString("##0.0")} seconds ({this.StartupSW.ElapsedMilliseconds}ms)");

                this.SplashScreen.Close();
                this.SplashScreen = null;

                Global_GUI.RestoreWindowState(this);

                this.LoadCameras(); //load camera list

                if (Environment.CommandLine.IndexOf("/min", StringComparison.OrdinalIgnoreCase) == -1)
                {
                    this.ShowForm();
                }


            });


        }

        private void ListScrollZoom(object sender, MouseEventArgs e)
        {
            if (Control.ModifierKeys != Keys.Control)
                return;
            float delta = (e.Delta > 0 ? 2f : -2f);
            FastObjectListView folv = ((FastObjectListView)sender);
            folv.Font = new Font(folv.Font.FontFamily, folv.Font.Size + delta);
            folv.AutoResizeColumns(ColumnHeaderAutoResizeStyle.ColumnContent);
        }
        private void tmr_Elapsed(object sender, ElapsedEventArgs e)
        {
            if ((DateTime.Now - this.TimeSinceType).Milliseconds >= 600)
            {
                Global_GUI.InvokeIFRequired(this.toolStripStatusLabelHistoryItems.GetCurrentParent(), () =>
                {
                    this.tmr.Stop();
                    this.tmr.Enabled = false;
                    if (Global.IsRegexPatternValid(this.ToolStripComboBoxSearch.Text) || this.ToolStripComboBoxSearch.Text.Length == 0)
                    {
                        this.ToolStripComboBoxSearch.ForeColor = Color.Blue;
                        if (this.ToolStripComboBoxSearch.FindStringExact(this.ToolStripComboBoxSearch.Text) == -1)
                            this.ToolStripComboBoxSearch.Items.Add(this.ToolStripComboBoxSearch.Text);

                        Global_GUI.FilterFOLV(this.folv_log, this.ToolStripComboBoxSearch.Text, this.mnu_Filter.Checked);
                    }
                    else
                    {
                        this.ToolStripComboBoxSearch.ForeColor = Color.Red;
                    }
                });
            }
        }

        async void EventMessage(ClsMessage msg)
        {
            if (IsClosing)
                return;

            using var Trace = new Trace();  //This c# 8.0 using feature will auto dispose when the function is done.

            //output messages from the deepstack, blueiris, etc class to the text log window and log file
            if (msg.MessageType == MessageType.LogEntry)
            {
                Log(msg.Description, "");
            }
            else if (msg.MessageType == MessageType.DatabaseInitialized)
            {

                Log("debug: Database initialized.");

                this.folv_history.EmptyListMsg = "No History";

                this.DatabaseInitialized = true;
                await this.LoadHistoryAsync(true, true);
                this.HistoryStartStop();

            }
            else if (msg.MessageType == MessageType.UpdateDeepstackStatus)
            {

                Log($"debug: Deepstack control message '{msg.Description}'");

                LoadDeepStackTab();

            }
            else if (msg.MessageType == MessageType.CreateHistoryItem)
            {
                JsonSerializerSettings jset = new JsonSerializerSettings { };
                jset.TypeNameHandling = TypeNameHandling.All;
                jset.PreserveReferencesHandling = PreserveReferencesHandling.Objects;
                jset.ContractResolver = Global.JSONContractResolver;

                History hist = JsonConvert.DeserializeObject<History>(msg.JSONPayload, jset);


                if (!HistoryDB.ReadOnly)
                {
                    bool StoreMasked = hist.Success || !hist.WasMasked || (hist.WasMasked && AppSettings.Settings.HistoryStoreMaskedAlerts);
                    bool StoreFalse = hist.Success || !(hist.Detections.IndexOf("false alert", StringComparison.OrdinalIgnoreCase) >= 0) || (hist.Detections.IndexOf("false alert", StringComparison.OrdinalIgnoreCase) >= 0 && AppSettings.Settings.HistoryStoreFalseAlerts);
                    bool Save = true;
                    if (!StoreMasked || !StoreFalse)
                        Save = false;

                    if (Save)
                    {
                        HistoryDB.InsertHistoryQueue(hist);
                    }
                    else
                    {
                        Log($"debug: Not storing item in db - StoreMasked={StoreMasked}, StoreFalse={StoreFalse}: {hist.Detections}");
                    }
                }

                //UpdateHistoryAddedRemoved();

                this.UpdateStats();
            }
            else if (msg.MessageType == MessageType.DeleteHistoryItem)
            {

                if (!HistoryDB.ReadOnly)  //assume service or other instance will be handling 
                {
                    HistoryDB.DeleteHistoryQueue(msg.Description);
                }

                //UpdateHistoryAddedRemoved();

                this.UpdateStats();

            }
            else if (msg.MessageType == MessageType.ImageAddedToQueue)
            {
                this.UpdateStats();
            }
            else if (msg.MessageType == MessageType.UpdateStatus)
            {
                this.UpdateStats();
            }
            else if (msg.MessageType == MessageType.UpdateProgressBar)
            {

                this.UpdateProgressBar(msg.Description, msg.CurVal, msg.MinVal, msg.MaxVal);

            }
            else if (msg.MessageType == MessageType.BeginProcessImage)
            {
                this.BeginProcessImage(msg.Description);
            }
            else if (msg.MessageType == MessageType.EndProcessImage)
            {
                this.EndProcessImage(msg.Description);
                this.UpdateStats();
            }
            else if (msg.MessageType == MessageType.UpdateLabel)
            {
                string lblcontrolname = (string)Global.SetJSONString<object>(msg.JSONPayload);

                if (!string.IsNullOrWhiteSpace(lblcontrolname))
                {
                    Label lbl = null;
                    try
                    {
                        lbl = this.Controls.Find(lblcontrolname, true).FirstOrDefault() as Label;
                    }
                    catch (Exception ex)
                    {

                        Log($"Error: Could not find label '{lblcontrolname}': {ex.Msg()}");
                    }

                    if (lbl != null)
                    {
                        Global_GUI.InvokeIFRequired(lbl, () =>
                        {
                            lbl.Show();
                            lbl.Text = msg.Description;
                        });

                    }
                    else
                    {
                        Log($"Error: Could not find label '{lblcontrolname}'?");

                    }

                }
                else
                {
                    Log($"Error: No label name passed - '{msg.Description}'");

                }

                this.UpdateStats();
            }
            else
            {
                Log($"Error: Unhandled message type '{msg.MessageType}'");
            }
        }

        private void UpdateProgressBar(string Message, int CurVal = -1, int MinVal = -1, int MaxVal = -1)
        {

            if (IsClosing)
                return;

            if (this.SplashScreen != null)
            {
                Global_GUI.InvokeIFRequired(this.SplashScreen.progressBar, () =>
                {

                    if (MaxVal != -1 && this.SplashScreen.progressBar.Maximum != MaxVal)
                        this.SplashScreen.progressBar.Maximum = MaxVal;

                    if (MinVal != -1 && this.SplashScreen.progressBar.Minimum != MinVal)
                        this.SplashScreen.progressBar.Minimum = MinVal;

                    if (this.SplashScreen.progressBar.Style != ProgressBarStyle.Continuous)
                        this.SplashScreen.progressBar.Style = ProgressBarStyle.Continuous;

                    if (CurVal == 1 && MinVal == 1 && MaxVal == 1)
                    {
                        this.SplashScreen.progressBar.Maximum = 2;
                        this.SplashScreen.progressBar.Value = this.SplashScreen.progressBar.Maximum;

                    }
                    else
                    {

                        if (CurVal > -1)
                        {
                            if (CurVal >= this.SplashScreen.progressBar.Minimum && CurVal <= this.SplashScreen.progressBar.Maximum)
                                this.SplashScreen.progressBar.Value = CurVal;
                            if (CurVal < this.SplashScreen.progressBar.Minimum)
                                this.SplashScreen.progressBar.Value = this.SplashScreen.progressBar.Minimum;
                            if (CurVal > this.SplashScreen.progressBar.Maximum)
                                this.SplashScreen.progressBar.Value = this.SplashScreen.progressBar.Maximum;
                        }
                    }

                    string msg = Global.ReplaceCaseInsensitive(Message, "debug:", "").Trim();
                    if (msg != this.SplashScreen.lbl_status.Text)
                    {
                        this.SplashScreen.lbl_status.Text = msg;
                        //SplashScreen.Refresh();
                    }

                    //SplashScreen.progressBar.Refresh();
                    //Application.DoEvents();  //causes all sorts of issues

                });

            }
            else
            {
                Global_GUI.InvokeIFRequired(this.toolStripProgressBar1.GetCurrentParent(), () =>
                {

                    if (MaxVal != -1 && this.toolStripProgressBar1.Maximum != MaxVal)
                        this.toolStripProgressBar1.Maximum = MaxVal;

                    if (MinVal != -1 && this.toolStripProgressBar1.Minimum != MinVal)
                        this.toolStripProgressBar1.Minimum = MinVal;

                    if (this.toolStripProgressBar1.Style != ProgressBarStyle.Continuous)
                        this.toolStripProgressBar1.Style = ProgressBarStyle.Continuous;

                    if (CurVal == 1 && MinVal == 1 && MaxVal == 1)
                    {
                        this.toolStripProgressBar1.Maximum = 2;
                        this.toolStripProgressBar1.Value = this.toolStripProgressBar1.Maximum;

                    }
                    else
                    {

                        if (CurVal > -1)
                        {
                            if (CurVal >= this.toolStripProgressBar1.Minimum && CurVal <= this.toolStripProgressBar1.Maximum)
                                this.toolStripProgressBar1.Value = CurVal;
                            if (CurVal < this.toolStripProgressBar1.Minimum)
                                this.toolStripProgressBar1.Value = this.toolStripProgressBar1.Minimum;
                            if (CurVal > this.toolStripProgressBar1.Maximum)
                                this.toolStripProgressBar1.Value = this.toolStripProgressBar1.Maximum;
                        }
                    }

                    if (this.toolStripProgressBar1.Value > 0 && !this.Visible)
                    {
                        this.Visible = true;
                    }
                    else if (this.toolStripProgressBar1.Value == 0 && this.Visible)
                    {
                        this.Visible = false;
                    }

                    this.toolStripProgressBar1.GetCurrentParent().Refresh();
                    //Application.DoEvents();  //causes all sorts of issues

                });


                this.UpdateStats(Message);

            }

        }

        //----------------------------------------------------------------------------------------------------------
        //CORE
        //----------------------------------------------------------------------------------------------------------


        //add text to log
        //public async void Log(string text, [CallerMemberName] string memberName = null)
        //{

        //    if (IsClosing)
        //        return;

        //    try
        //    {

        //        //get current date and time

        //        string time = DateTime.Now.ToString("dd.MM.yyyy, HH:mm:ss");
        //        string rtftime = DateTime.Now.ToString("dHH:mm:ss");  //no need for date in log tab
        //        string ModName = "";
        //        if (memberName == ".ctor")
        //            memberName = "Constructor";

        //        if (AppSettings.Settings.log_everything == true || AppSettings.Settings.deepstack_debug)
        //        {
        //            time = DateTime.Now.ToString("dd.MM.yyyy, HH:mm:ss.fff");
        //            rtftime = DateTime.Now.ToString("HH:mm:ss.fff");
        //            if (memberName != null && !string.IsNullOrEmpty(memberName))
        //                ModName = memberName.PadLeft(24) + "> ";

        //            //when the global logger reports back to the progress logger we cant use CallerMemberName, so extract the member name from text

        //            int gg = text.IndexOf(">> ");

        //            if (gg > 0 && gg <= 24)
        //            {
        //                string modfromglobal = Global.GetWordBetween(text, "", ">> ");
        //                if (!string.IsNullOrEmpty(modfromglobal))
        //                {
        //                    ModName = modfromglobal.PadLeft(24) + "> ";
        //                    text = Global.GetWordBetween(text, ">> ", "");
        //                }

        //            }
        //        }

        //        //check for messages coming from deepstack processes and kill them if we didnt ask for debugging messages
        //        if (!AppSettings.Settings.deepstack_debug)
        //        {
        //            if (text.ToLower().Contains("redis-server.exe>") || text.ToLower().Contains("python.exe>"))
        //            {
        //                return;
        //            }
        //        }

        //        //make the error and warning detection case insensitive:
        //        bool HasError = (text.IndexOf("error", StringComparison.InvariantCultureIgnoreCase) > -1) || (text.IndexOf("exception", StringComparison.InvariantCultureIgnoreCase) > -1);
        //        bool HasWarning = (text.IndexOf("warning:", StringComparison.InvariantCultureIgnoreCase) > -1);
        //        bool HasInfo = (text.IndexOf("info:", StringComparison.InvariantCultureIgnoreCase) > -1);
        //        bool HasDebug = (text.IndexOf("debug:", StringComparison.InvariantCultureIgnoreCase) > -1);
        //        bool IsDeepStackMsg = (memberName.IndexOf("deepstack", StringComparison.InvariantCultureIgnoreCase) > -1) || (text.IndexOf("deepstack", StringComparison.InvariantCultureIgnoreCase) > -1) || (ModName.IndexOf("deepstack", StringComparison.InvariantCultureIgnoreCase) > -1);

        //        string RTFText = "";

        //        //set the color for RTF text window:
        //        if (HasError)
        //        {
        //            RTFText = $"{{gray}}[{rtftime}]: {ModName}{{red}}{text}";
        //        }
        //        else if (HasWarning)
        //        {
        //            RTFText = $"{{gray}}[{rtftime}]: {ModName}{{mediumorchid}}{text}";
        //        }
        //        else if (IsDeepStackMsg)
        //        {
        //            RTFText = $"{{gray}}[{rtftime}]: {ModName}{{lime}}{text}";
        //        }
        //        else if (HasInfo)
        //        {
        //            RTFText = $"{{gray}}[{rtftime}]: {ModName}{{yellow}}{text}";
        //        }
        //        else if (HasDebug)
        //        {
        //            RTFText = $"{{gray}}[{rtftime}]: {ModName}{text}";
        //        }
        //        else
        //        {
        //            RTFText = $"{{gray}}[{rtftime}]: {ModName}{{white}}{text}";
        //        }

        //        if (!AppSettings.AlreadyRunning)
        //        {
        //            Global.SaveSetting("LastLogEntry", RTFText);
        //            Global.SaveSetting("LastShutdownState", $"checkpoint: GUI.Log: {DateTime.Now}");
        //        }

        //        //get rid of any common color coding before logging to file or console
        //        text = text.Replace("{yellow}", "").Replace("{red}", "").Replace("{white}", "").Replace("{orange}", "").Replace("{lime}", "").Replace("{orange}", "mediumorchid");

        //        //if log everything is disabled and the text is neither an ERROR, nor a WARNING: write only to console and ABORT
        //        if (AppSettings.Settings.log_everything == false && !HasError && !HasWarning)
        //        {
        //            //Creates a lot of extra text in immediate window while debugging, disabling -Vorlon
        //            //text += "Enabling \'Log everything\' might give more information.";
        //            Console.WriteLine($"[{rtftime}]: {ModName}{text}");

        //            return;
        //        }



        //        RTFLogger.LogToRTF(RTFText);
        //        LogWriter.WriteToLog($"[{time}]:  {ModName}{text}", HasError);



        //                        //add log text to console


        //    }
        //    catch (Exception ex)
        //    {

        //        Console.WriteLine("Error: In LOG, got: " + ex.Message);
        //    }

        //}



        //----------------------------------------------------------------------------------------------------------
        //GUI
        //----------------------------------------------------------------------------------------------------------

        //minimize to tray
        private void Form1_Resize(object sender, EventArgs e)
        {

            if (IsLoading)
                return;

            if (IsClosing)
                return;

            if (this.WindowState == FormWindowState.Minimized)
            {
                HideForm();
            }

            UpdateErrorIcon();

        }

        //protected override void SetVisibleCore(bool value)
        //{
        //    base.SetVisibleCore(AllowShowDisplay ? value : AllowShowDisplay);
        //}

        private async void ShowForm()
        {

            if (IsClosing)
                return;

            try
            {
                //this.AllowShowDisplay = true;
                this.Opacity = 100;
                this.TopMost = true;
                this.TopMost = false;
                this.Show();
                this.WindowState = FormWindowState.Normal;
                this.ShowInTaskbar = true;
                this.notifyIcon.Visible = false;
                this.LoadHistoryAsync(true, true);
                this.UpdateLogAddedRemovedAsync(true, true);
                this.Activate();
                Application.DoEvents();

            }
            catch { }
        }

        private async void HideForm()
        {

            if (IsClosing)
                return;

            if (!AppSettings.Settings.MinimizeToTray)
                return;

            try
            {
                this.Hide();
                this.Opacity = 0;
                this.notifyIcon.Visible = true;
                //Log($"Debug: Hiding app. Visible={this.Visible}, state={this.WindowState}, tbicon={this.ShowInTaskbar}, trayicon={this.notifyIcon.Visible}");
                Application.DoEvents();

            }
            catch { }

        }
        //open Log when clicking or error message
        private void lbl_errors_Click(object sender, EventArgs e)
        {
            this.ShowErrors();

        }

        private void ShowErrors()
        {

            //filter the main log list for errors
            this.chk_filterErrors.Checked = true;
            this.tabControl1.SelectedTab = this.tabLog;
            this.FilterLogErrors();
        }


        //EVENTS:

        //event: mouse click on tab control
        private void tabControl1_MouseDown(object sender, MouseEventArgs e)
        {
        }

        //event: another tab selected (Only load certain things in tabs if they are actually open)
        private async void tabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {
            //Application.DoEvents();
            using var Trace = new Trace();

            try
            {
                if (this.tabControl1.SelectedIndex == 1)
                {
                    this.UpdatePieChart(); this.UpdateTimeline(); this.UpdateConfidenceChart();
                }
                else if (this.tabControl1.SelectedIndex == 2)
                {
                    await this.LoadHistoryAsync(true, true);
                }
                else if (this.tabControl1.SelectedIndex == 3)
                {
                    this.LoadCameras();
                }
                else if (this.tabControl1.SelectedIndex == 4)
                {
                    this.LoadSettingsTab();
                }
                else if (this.tabControl1.SelectedTab == this.tabControl1.TabPages["tabDeepStack"])
                {
                    this.LoadDeepStackTab();
                }
                else if (this.tabControl1.SelectedTab == this.tabControl1.TabPages["tabLog"])
                {
                    //scroll to bottom, only when tab is active for better performance 
                    if (!this.toolStripButtonPauseLog.Checked)
                    {
                        await this.UpdateLogAddedRemovedAsync(true, true);
                    }
                }
                UpdateStats();

            }
            catch (Exception ex)
            {
                string err = $"Error: While changing to tab {this.tabControl1.SelectedTab.Name}, got error: {ex.Msg()}";
                Log(err);
                MessageBox.Show(err);

            }

        }


        //----------------------------------------------------------------------------------------------------------
        //STATS TAB
        //----------------------------------------------------------------------------------------------------------

        //other camera in combobox selected, display according PieChart
        private void comboBox1_SelectedIndexChanged_1(object sender, EventArgs e)
        {

        }

        //update pie chart
        public void UpdatePieChart()
        {

            if (this.tabControl1.SelectedIndex != 1 || !this.Visible || this.WindowState == FormWindowState.Minimized || string.IsNullOrEmpty(this.comboBox1.Text))
                return;

            int alerts = 0;
            int irrelevantalerts = 0;
            int falsealerts = 0;
            int skipped = 0;
            Series ser = this.chart1.Series[0];


            if (this.comboBox1.Text == "All Cameras")
            {
                foreach (Camera cam in AppSettings.Settings.CameraList)
                {
                    alerts += cam.stats_alerts;
                    irrelevantalerts += cam.stats_irrelevant_alerts;
                    falsealerts += cam.stats_false_alerts;
                    skipped += cam.stats_skipped_images;
                }
            }
            else
            {

                Camera cam = AITOOL.GetCamera(this.comboBox1.Text);  //int i = AppSettings.Settings.CameraList.FindIndex(x => x.name.ToLower().Trim() == comboBox1.Text.ToLower().Trim());
                if (cam != null)
                {
                    alerts = cam.stats_alerts;
                    irrelevantalerts = cam.stats_irrelevant_alerts;
                    falsealerts = cam.stats_false_alerts;
                    skipped = cam.stats_skipped_images;
                }
                else
                {
                    alerts = 0;
                    irrelevantalerts = 0;
                    falsealerts = 0;
                    skipped = 0;
                    Log($"Error: Could not match combobox dropdown '{this.comboBox1.Text}' to a known camera name?");
                }
            }

            ser.Points.Clear();
            ser.IsVisibleInLegend = true;


            ser.LegendText = "#LABEL"; //"#VALY"; //"#VALY #VALX"
            //ser["PieLabelStyle"] = "Disabled";

            int index = -1;

            //show Alerts label
            index = ser.Points.AddXY("Alerts", alerts);
            ser.Points[index].Color = System.Drawing.Color.Green;
            ser.Points[index].LegendText = $"{alerts.ToString("00000")} - Alerts";
            ser.Points[index].Label = "Alerts";

            //show irrelevant Alerts label
            index = ser.Points.AddXY("Irrelevant Alerts", irrelevantalerts);
            ser.Points[index].Color = System.Drawing.Color.Orange;
            ser.Points[index].LegendText = $"{irrelevantalerts.ToString("00000")} - Irrelevant Alerts";
            ser.Points[index].Label = "Irrelevant";

            //show false Alerts label
            index = ser.Points.AddXY("False Alerts", falsealerts);
            ser.Points[index].Color = System.Drawing.Color.OrangeRed;
            ser.Points[index].LegendText = $"{falsealerts.ToString("00000")} - False Alerts";
            ser.Points[index].Label = "False";

            //show skipped label
            index = ser.Points.AddXY("Skipped Images", skipped);
            ser.Points[index].Color = System.Drawing.Color.Purple;
            ser.Points[index].LegendText = $"{skipped.ToString("00000")} - Skipped Images";
            ser.Points[index].Label = "Skipped";



        }

        //update timeline
        public void UpdateTimeline()
        {

            if (this.tabControl1.SelectedIndex != 1 || !this.Visible || this.WindowState == FormWindowState.Minimized || string.IsNullOrEmpty(this.comboBox1.Text))
                return;

            Log("Debug: Loading time line from history database ...");

            //clear previous values
            this.timeline.Series[0].Points.Clear();
            this.timeline.Series[1].Points.Clear();
            this.timeline.Series[2].Points.Clear();
            this.timeline.Series[3].Points.Clear();
            this.timeline.Series[4].Points.Clear();

            Stopwatch SW = Stopwatch.StartNew();

            try
            {
                List<History> result = HistoryDB.GetAllValues();

                if (!string.Equals(this.comboBox1.Text.Trim(), "All Cameras", StringComparison.OrdinalIgnoreCase)) //all cameras selected
                {
                    result = result.Where(hist => hist.Camera.StartsWith(this.comboBox1.Text.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
                }

                //every int represents the number of ai calls in successive half hours (p.e. relevant[0] is 0:00-0:30 o'clock, relevant[1] is 0:30-1:00 o'clock) 
                int[] all = new int[48];
                int[] falses = new int[48];
                int[] irrelevant = new int[48];
                int[] relevant = new int[48];
                int[] skipped = new int[48];

                //fill arrays with amount of calls/half hour
                foreach (History hist in result)
                {               //example of time column entry: 23.08.19, 18:31:09
                                //get hour
                    int hour = hist.Date.Hour;

                    //get minute
                    int minute = hist.Date.Minute;

                    int halfhour; //stores the half hour in which the alert occurred

                    //add +1 to counter for corresponding half-hour
                    if (minute >= 30) //if alert occurred after half o clock
                    {
                        halfhour = hour * 2 + 1;
                    }
                    else //if alert occured before half o clock
                    {
                        halfhour = hour * 2;
                    }

                    //if detection was successful
                    if (hist.Success)
                    {
                        relevant[halfhour]++;
                    }
                    //if it was a false alert
                    else if (string.Equals(hist.Detections, "false alert", StringComparison.OrdinalIgnoreCase))
                    {
                        falses[halfhour]++;
                    }
                    //if something irrelevant was detected
                    else
                    {
                        irrelevant[halfhour]++;
                    }

                    if (hist.WasSkipped)
                        skipped[halfhour]++;

                    all[halfhour]++;
                }

                //add to graph "all":

                /*the graph will have a gap at the end and at the beginning if we don'f specify a value
                * with an x value outside the visible area at the end and before the first visible point. 
                * So the first point is at -0.25 and has the value of the last visible point and the 
                * last point is at 24.25 and has the value of the first visible point. */

                this.timeline.Series[0].Points.AddXY(-0.25, all[47]); // beginning point with value of last visible point

                //and now add all visible points 
                double x = 0.25;
                foreach (int halfhour in all)
                {
                    int index = this.timeline.Series[0].Points.AddXY(x, halfhour);
                    x = x + 0.5;
                }

                this.timeline.Series[0].Points.AddXY(24.25, all[0]); // finally add last point with value of first visible point

                //add to graph "falses":

                this.timeline.Series[1].Points.AddXY(-0.25, falses[47]); // beginning point with value of last visible point
                                                                         //and now add all visible points 
                x = 0.25;
                foreach (int halfhour in falses)
                {
                    int index = this.timeline.Series[1].Points.AddXY(x, halfhour);
                    x = x + 0.5;
                }
                this.timeline.Series[1].Points.AddXY(24.25, falses[0]); // finally add last point with value of first visible point

                //add to graph "irrelevant":

                this.timeline.Series[2].Points.AddXY(-0.25, irrelevant[47]); // beginning point with value of last visible point
                                                                             //and now add all visible points 
                x = 0.25;
                foreach (int halfhour in irrelevant)
                {
                    int index = this.timeline.Series[2].Points.AddXY(x, halfhour);
                    x = x + 0.5;
                }
                this.timeline.Series[2].Points.AddXY(24.25, irrelevant[0]); // finally add last point with value of first visible point

                //add to graph "relevant":

                this.timeline.Series[3].Points.AddXY(-0.25, relevant[47]); // beginning point with value of last visible point
                                                                           //and now add all visible points 
                x = 0.25;
                foreach (int halfhour in relevant)
                {
                    int index = this.timeline.Series[3].Points.AddXY(x, halfhour);
                    x = x + 0.5;
                }
                this.timeline.Series[3].Points.AddXY(24.25, relevant[0]); // finally add last point with value of first visible point


                //add to graph "skipped":

                this.timeline.Series[4].Points.AddXY(-0.25, skipped[47]); // beginning point with value of last visible point
                                                                          //and now add all visible points 
                x = 0.25;
                foreach (int halfhour in skipped)
                {
                    int index = this.timeline.Series[4].Points.AddXY(x, halfhour);
                    x = x + 0.5;
                }
                this.timeline.Series[4].Points.AddXY(24.25, skipped[0]); // finally add last point with value of first visible point



            }
            catch (Exception ex)
            {

                Log("Error: " + ex.Msg());
            }




        }

        //update confidence_frequency chart
        public void UpdateConfidenceChart()
        {

            if (this.tabControl1.SelectedIndex != 1 || !this.Visible || this.WindowState == FormWindowState.Minimized || string.IsNullOrEmpty(this.comboBox1.Text))
                return;


            Log("Debug: Loading confidence-frequency chart from history database ...");

            //clear previous values
            this.chart_confidence.Series[0].Points.Clear();
            this.chart_confidence.Series[1].Points.Clear();


            Stopwatch SW = Stopwatch.StartNew();

            try
            {
                List<History> result = HistoryDB.GetAllValues();

                if (!string.Equals(this.comboBox1.Text.Trim(), "All Cameras", StringComparison.OrdinalIgnoreCase)) //all cameras selected
                {
                    result = result.Where(hist => hist.Camera.StartsWith(this.comboBox1.Text.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
                }

                //this array stores the Absolute frequencies of all possible confidence values (0%-100%)
                int[] green_values = new int[101];
                int[] orange_values = new int[101];

                //fill array with frequencies
                foreach (History hist in result)
                {
                    //example of detections column entry: "person (41%); person (97%);" or "masked: person (41%); person (97%);"
                    string detections_column = hist.Detections;
                    if (detections_column.Contains(':'))
                    {
                        detections_column = detections_column.Split(':')[1];

                        string[] detections = detections_column.Split(';');

                        //write the confidence of every detection into the green_values string
                        foreach (string detection in detections)
                        {
                            int x_value = Global.GetNumberInt(detection);  // gets a number anywhere in the string

                            //fix temp bug from earlier where whole number percentages were multiplied by 100 when they shouldnt have been.
                            if (x_value > 500)
                                x_value = x_value / 100;

                            if (x_value > 0 && x_value <= orange_values.Count() - 1)
                            {
                                //example: -> "person (41%)"
                                //Int32.TryParse(detection.Split('(')[1].Split('%')[0], out int x_value); //example: -> "41"
                                orange_values[x_value]++;
                            }
                        }
                    }
                    else
                    {
                        string[] detections = detections_column.Split(';');

                        //write the confidence of every detection into the green_values string
                        foreach (string detection in detections)
                        {
                            int x_value = Global.GetNumberInt(detection);  // gets first number anywhere in the string - TODO: May not be correct
                            if (x_value > 0 && x_value <= green_values.Count() - 1)
                            {
                                //example: -> "person (41%)"
                                //Int32.TryParse(detection.Split('(')[1].Split('%')[0], out int x_value); //example: -> "41"
                                green_values[x_value]++;
                            }
                        }
                    }
                }


                //write green series in chart
                int i = 0;
                foreach (int y_value in green_values)
                {
                    this.chart_confidence.Series[1].Points.AddXY(i, y_value);
                    i++;
                }

                //write orange series in chart
                i = 0;
                foreach (int y_value in orange_values)
                {
                    this.chart_confidence.Series[0].Points.AddXY(i, y_value);
                    i++;
                }



            }
            catch (Exception ex)
            {

                Log("Error: " + ex.Msg());
            }


        }


        private void showHideMask()
        {
            if (this.cb_showMask.Checked == true) //show overlay
            {
                //Log("Show mask toggled.");

                if (this.folv_history.SelectedObjects != null && this.folv_history.SelectedObjects.Count > 0)
                {
                    History hist = (History)this.folv_history.SelectedObjects[0];

                    string imagefile = AITOOL.GetMaskFile(hist.Camera);

                    if (File.Exists(imagefile))
                    {
                        using (var img = new Bitmap(imagefile))
                        {
                            this.pictureBox1.Image = new Bitmap(img); //load mask as overlay
                        }
                    }
                    else
                    {
                        this.pictureBox1.Image = null; //if file does not exist, empty mask overlay (from possible overlays of previous images)
                    }

                }

            }
            else //if showmask toggle-button is not checked, hide the mask overlay
            {
                this.pictureBox1.Image = null;
            }

        }

        //show rectangle overlay
        //        private void showObject(PaintEventArgs e, double _xmin, double _ymin, double _xmax, double _ymax, string text, ResultType result)
        //        {
        //            try
        //            {
        //                if (this.folv_history.SelectedObjects != null && this.folv_history.SelectedObjects.Count > 0 && (this.pictureBox1 != null) && (this.pictureBox1.BackgroundImage != null))
        //                {

        //                    System.Drawing.Color color = new System.Drawing.Color();
        //                    int BorderWidth = AppSettings.Settings.RectBorderWidth
        //;

        //                    if (result == ResultType.Relevant)
        //                    {
        //                        color = System.Drawing.Color.FromArgb(AppSettings.Settings.RectRelevantColorAlpha, AppSettings.Settings.RectRelevantColor);
        //                    }
        //                    else if (result == ResultType.DynamicMasked || result == ResultType.ImageMasked || result == ResultType.StaticMasked)
        //                    {
        //                        color = System.Drawing.Color.FromArgb(AppSettings.Settings.RectMaskedColorAlpha, AppSettings.Settings.RectMaskedColor);
        //                    }
        //                    else
        //                    {
        //                        color = System.Drawing.Color.FromArgb(AppSettings.Settings.RectIrrelevantColorAlpha, AppSettings.Settings.RectIrrelevantColor);
        //                    }

        //                    //1. get the padding between the image and the picturebox border

        //                    //get dimensions of the image and the picturebox
        //                    double imgWidth = this.pictureBox1.BackgroundImage.Width;
        //                    double imgHeight = this.pictureBox1.BackgroundImage.Height;
        //                    double boxWidth = this.pictureBox1.Width;
        //                    double boxHeight = this.pictureBox1.Height;
        //                    //double clnWidth = this.pictureBox1.ClientSize.Width;
        //                    //double clnHeight = this.pictureBox1.ClientSize.Height;
        //                    //double rctWidth = this.pictureBox1.ClientRectangle.Width;
        //                    //double rctHeight = this.pictureBox1.ClientRectangle.Height;

        //                    //these variables store the padding between image border and picturebox border
        //                    double absX = 0;
        //                    double absY = 0;

        //                    //because the sizemode of the picturebox is set to 'zoom', the image is scaled down
        //                    double scale = 1;


        //                    //Comparing the aspect ratio of both the control and the image itself.
        //                    if (imgWidth / imgHeight > boxWidth / boxHeight) //if the image is p.e. 16:9 and the picturebox is 4:3
        //                    {
        //                        scale = boxWidth / imgWidth; //get scale factor
        //                        absY = (boxHeight - scale * imgHeight) / 2; //padding on top and below the image
        //                    }
        //                    else //if the image is p.e. 4:3 and the picturebox is widescreen 16:9
        //                    {
        //                        scale = boxHeight / imgHeight; //get scale factor
        //                        absX = (boxWidth - scale * imgWidth) / 2; //padding left and right of the image
        //                    }

        //                    //2. inputted position values are for the original image size. As the image is probably smaller in the picturebox, the positions must be adapted. 
        //                    double xmin = (scale * _xmin) + absX;
        //                    double xmax = (scale * _xmax) + absX;
        //                    double ymin = (scale * _ymin) + absY;
        //                    double ymax = (scale * _ymax) + absY;

        //                    double sclWidth = xmax - xmin;
        //                    double sclHeight = ymax - ymin;

        //                    double sclxmax = boxWidth - (absX * 2);
        //                    double sclymax = boxHeight - (absY * 2);
        //                    double sclxmin = absX;
        //                    double sclymin = absY;

        //                    //3. paint rectangle
        //                    System.Drawing.Rectangle rect = new System.Drawing.Rectangle(xmin.ToInt(),
        //                                                                                 ymin.ToInt(),
        //                                                                                 sclWidth.ToInt(),
        //                                                                                 sclHeight.ToInt());
        //                    using (Pen pen = new Pen(color, BorderWidth))
        //                    {
        //                        e.Graphics.DrawRectangle(pen, rect); //draw rectangle
        //                    }


        //                    ///testing=================================================
        //                    //3. paint rectangle
        //                    //rect = new System.Drawing.Rectangle(absX + 5,
        //                    //                                    absY + 5,
        //                    //                                    sclxmax - 10,
        //                    //                                    sclymax - 10);

        //                    //using (Pen pen = new Pen(Color.Red, BorderWidth))
        //                    //{
        //                    //    e.Graphics.DrawRectangle(pen, rect); //draw rectangle
        //                    //}
        //                    ///testing=================================================

        //                    //we need this since people can change the border width in the json file
        //                    double halfbrd = BorderWidth / 2;


        //                    System.Drawing.SizeF TextSize = e.Graphics.MeasureString(text, new Font(AppSettings.Settings.RectDetectionTextFont, AppSettings.Settings.RectDetectionTextSize)); //finds size of text to draw the background rectangle


        //                    //object name text below rectangle

        //                    double x = xmin - halfbrd;
        //                    double y = ymax + halfbrd;


        //                    //adjust the x / width label so it doesnt go off screen
        //                    double EndX = x + TextSize.Width;
        //                    if (EndX > sclxmax)
        //                    {
        //                        //int diffx = x - sclxmax;
        //                        x = xmax - TextSize.Width + halfbrd;
        //                    }

        //                    if (x < sclxmin)
        //                        x = sclxmin;

        //                    if (x < 0)
        //                        x = 0;

        //                    //adjust the y / height label so it doesnt go off screen
        //                    double EndY = y + TextSize.Height;
        //                    if (EndY > sclymax)
        //                    {
        //                        //float diffy = EndY - sclymax;
        //                        y = ymax - TextSize.Height - halfbrd;
        //                    }

        //                    if (y < 0)
        //                        y = 0;


        //                    rect = new System.Drawing.Rectangle(x.ToInt(),
        //                                                        y.ToInt(),
        //                                                        boxWidth.ToInt(),
        //                                                        boxHeight.ToInt()); //sets bounding box for drawn text


        //                    Brush brush = new SolidBrush(color); //sets background rectangle color
        //                    if (AppSettings.Settings.RectDetectionTextBackColor != Color.Gainsboro)
        //                        brush = new SolidBrush(AppSettings.Settings.RectDetectionTextBackColor);

        //                    Brush forecolor = Brushes.Black;
        //                    if (AppSettings.Settings.RectDetectionTextForeColor != Color.Gainsboro)
        //                        forecolor = new SolidBrush(AppSettings.Settings.RectDetectionTextForeColor);

        //                    e.Graphics.FillRectangle(brush,
        //                                             x.ToInt(),
        //                                             y.ToInt(),
        //                                             TextSize.Width,
        //                                             TextSize.Height); //draw grey background rectangle for detection text

        //                    e.Graphics.DrawString(text,
        //                                          new Font(AppSettings.Settings.RectDetectionTextFont,
        //                                          AppSettings.Settings.RectDetectionTextSize),
        //                                          forecolor,
        //                                          rect); //draw detection text


        //                }

        //            }
        //            catch (Exception ex)
        //            {

        //                Log("Error: " + ex.Msg());
        //            }
        //        }

        //load object rectangle overlays
        //TODO: refactor detections
        private void pictureBox1_Paint(object sender, PaintEventArgs e)
        {
            if (this.pictureBox1.BackgroundImage.IsNull())
                return;

            if (AppSettings.Settings.HistoryShowObjects && this.folv_history.SelectedObjects != null && this.folv_history.SelectedObjects.Count > 0) //if checkbox button is enabled
            {
                //Log("Loading object rectangles...");
                History hist = (History)this.folv_history.SelectedObjects[0];

                if (hist == null)
                {
                    Log("Warn: Selected history item is null?");
                    return;
                }

                string positions = hist.Positions;
                string detections = hist.Detections;

                //int XOffset = 0;
                //int YOffset = 0;

                //Camera cam = AITOOL.GetCamera(hist.Camera);
                //if (cam != null)
                //{
                //    //apply offset if one is defined by user in json file
                //    XOffset = cam.XOffset;
                //    YOffset = cam.YOffset;
                //}

                try
                {

                    if (!string.IsNullOrEmpty(hist.PredictionsJSON))
                    {
                        List<ClsPrediction> predictions = hist.Predictions();

                        if (predictions.Count > 0)
                        {
                            //draw in reverse, assuming most important should be on top
                            for (int i = predictions.Count - 1; i >= 0; i--)
                            {
                                ClsPrediction pred = predictions[i];
                                if (pred != null)
                                {

                                    AITOOL.DrawAnnotation(e.Graphics,
                                                          pred,
                                                          this.pictureBox1.BackgroundImage.Width,
                                                          this.pictureBox1.BackgroundImage.Height,
                                                          this.pictureBox1.Width,
                                                          this.pictureBox1.Height);

                                    //if (AppSettings.Settings.HistoryOnlyDisplayRelevantObjects && pred.Result == ResultType.Relevant)
                                    //{
                                    //    this.showObject(e, pred.XMin + XOffset, pred.YMin + YOffset, pred.XMax, pred.YMax, pred.ToString(), pred.Result); //call rectangle drawing method, calls appropriate detection text
                                    //}
                                    //else if (!AppSettings.Settings.HistoryOnlyDisplayRelevantObjects)
                                    //{
                                    //    this.showObject(e, pred.XMin + XOffset, pred.YMin + YOffset, pred.XMax, pred.YMax, pred.ToString(), pred.Result); //call rectangle drawing method, calls appropriate detection text
                                    //}
                                }
                                else
                                {
                                    Log($"Warn: Prediction #{i + 1} of {predictions.Count} is empty?");

                                }

                            }
                        }
                        else
                        {
                            Log("Warn: No predictions for the selected history item.");
                        }

                    }
                    //else
                    //{

                    //    //we should never get here after all old AITOOL entries have been deleted
                    //    List<string> positionssArray = positions.SplitStr(";");//creates array of detected objects, used for adding text overlay

                    //    int countr = positionssArray.Count;

                    //    ResultType result = ResultType.Unknown;

                    //    if (detections.IndexOf("irrelevant", StringComparison.OrdinalIgnoreCase) >= 0 || detections.IndexOf("masked", StringComparison.OrdinalIgnoreCase) >= 0 || detections.IndexOf("confidence", StringComparison.OrdinalIgnoreCase) >= 0)
                    //    {
                    //        detections = detections.Split(':')[1]; //removes the "1x masked, 3x irrelevant:" before the actual detection, otherwise this would be displayed in the detection tags
                    //        if (detections.Contains("masked"))
                    //        {
                    //            result = ResultType.ImageMasked;
                    //        }
                    //        else
                    //        {
                    //            result = ResultType.Unknown;
                    //        }
                    //    }
                    //    else
                    //    {
                    //        result = ResultType.Relevant;
                    //    }

                    //    List<string> detectionsArray = detections.SplitStr(";");//creates array of detected objects, used for adding text overlay

                    //    if (cam != null)
                    //    {
                    //        //apply offset if one is defined by user in json file
                    //        XOffset = cam.XOffset;
                    //        YOffset = cam.YOffset;
                    //    }

                    //    //display a rectangle around each relevant object
                    //    for (int i = 0; i < countr; i++)
                    //    {

                    //        //load 'xmin,ymin,xmax,ymax' from third column into a string
                    //        List<string> positionsplt = positionssArray[i].SplitStr(",");

                    //        //store xmin, ymin, xmax, ymax in separate variables
                    //        Int32.TryParse(positionsplt[0], out int xmin);
                    //        Int32.TryParse(positionsplt[1], out int ymin);
                    //        Int32.TryParse(positionsplt[2], out int xmax);
                    //        Int32.TryParse(positionsplt[3], out int ymax);


                    //        this.showObject(e, xmin + XOffset, ymin + YOffset, xmax, ymax, detectionsArray[i], result); //call rectangle drawing method, calls appropriate detection text

                    //    }

                    //}


                }
                catch (Exception ex)
                {

                    Log($"Error: Positions (subitem4) ='{positions}', Detections (subitem3) ='{detections}': {ex.Msg()}");
                }

            }
        }

        // add new entry in left list
        private bool showingtrayerr = false;
        private bool showingtrayok = false;

        private void UpdateErrorIcon()
        {
            try
            {
                //make tray icon red if there are errors
                if (LogMan.ErrorCount == 0 && !showingtrayok)
                {
                    this.notifyIcon.Icon = AITool.Properties.Resources.Logo_old;
                    this.Icon = AITool.Properties.Resources.Logo_old;
                    this.Refresh();
                    showingtrayok = true;
                    showingtrayerr = false;
                }
                else if (LogMan.ErrorCount > 0 && !showingtrayerr)
                {
                    this.notifyIcon.Icon = AITool.Properties.Resources.Logo_Error_old;
                    this.Icon = AITool.Properties.Resources.Logo_Error_old;
                    this.Refresh();
                    showingtrayok = false;
                    showingtrayerr = true;
                }

            }
            catch { }
        }



        private void UpdateStats(string Message = "")
        {
            try
            {
                UpdateErrorIcon();


                if (!this.Visible || (this.WindowState == FormWindowState.Minimized) || this.IsStatsUpdating || IsClosing)
                    return;  //save a tree

                this.IsStatsUpdating = true;

                int alerts = 0;
                int irrelevantalerts = 0;
                int falsealerts = 0;
                int newskipped = 0;
                int skipped = 0;
                foreach (Camera cam in AppSettings.Settings.CameraList)
                {
                    alerts += cam.stats_alerts;
                    irrelevantalerts += cam.stats_irrelevant_alerts;
                    falsealerts += cam.stats_false_alerts;
                    skipped += cam.stats_skipped_images;
                    newskipped += cam.stats_skipped_images_session;
                }

                Global_GUI.InvokeIFRequired(this.toolStripStatusLabelHistoryItems.GetCurrentParent(), () =>
                {

                    double hpm = 0;
                    int items = 0;
                    int removed = 0;

                    if (HistoryDB != null)
                    {
                        items = HistoryDB.HistoryDic.Count;
                        removed = HistoryDB.DeletedCount;
                    }

                    if (HistoryDB != null && HistoryDB.AddedCount > 0)
                        hpm = HistoryDB.AddedCount / (DateTime.Now - HistoryDB.InitializeTime).TotalMinutes;

                    this.toolStripStatusLabelHistoryItems.Text = $"{items} history items ({hpm.ToString("###0")}/MIN) | {removed} removed |";
                    int TriggerActionQueueCount = 0;
                    if (TriggerActionQueue != null)
                        TriggerActionQueueCount = TriggerActionQueue.Count;

                    this.toolStripStatusLabel1.Text = $"| {alerts} Alerts | {irrelevantalerts} Irrelevant | {falsealerts} False | {skipped} Skipped ({newskipped} new) | {qcalc.Count} ImgProcessed ({qcalc.ItemsPerMinute().ToString("###0")}/MIN) | {ImageProcessQueue.Count} ImgQueued (Max={scalc.Max},Avg={Math.Round(scalc.Avg, 0)}) | {TriggerActionQueueCount} ActQueued (Min={TriggerActionQueue.ActionTimeCalc.Min.Round(0)}ms/Max={TriggerActionQueue.ActionTimeCalc.Max.Round(0)}ms)";

                    this.toolStripStatusErrors.Text = $"{LogMan.ErrorCount} Errors";

                    if (!string.IsNullOrEmpty(Message))
                    {
                        this.toolStripStatusLabelInfo.Text = Message;
                    }
                    else if (ImageProcessQueue.Count > 0 && this.toolStripProgressBar1.Value == 0)
                    {
                        this.toolStripStatusLabelInfo.Text = "Working...";
                    }
                    else if (this.toolStripProgressBar1.Value == 0)
                    {
                        this.toolStripStatusLabelInfo.Text = "Idle.";
                    }

                    //if (toolStripProgressBar1.Style == ProgressBarStyle.Marquee && toolStripStatusLabelInfo.Text == "Idle.")
                    //{
                    //    toolStripProgressBar1.Style = ProgressBarStyle.Continuous;
                    //}


                    if (LogMan.ErrorCount > 0)
                    {
                        this.toolStripStatusErrors.ForeColor = Color.Black;
                        this.toolStripStatusErrors.BackColor = Color.Red;
                    }
                    else
                    {
                        this.toolStripStatusErrors.ForeColor = this.toolStripStatusLabelHistoryItems.GetCurrentParent().ForeColor;
                        this.toolStripStatusErrors.BackColor = this.toolStripStatusLabelHistoryItems.GetCurrentParent().BackColor;
                    }

                    this.toolStripStatusErrors.GetCurrentParent().Refresh();

                });

                Global_GUI.InvokeIFRequired(this.lblQueue, () =>
                {
                    this.lblQueue.Text = $"Images in queue: {ImageProcessQueue.Count}, Max: {scalc.Max} ({qcalc.Max}ms), Average: {scalc.Avg.ToString("#####")} ({qcalc.Avg.ToString("#####")}ms queue wait time, Trigger Actions Min={TriggerActionQueue.ActionTimeCalc.Min.Round(0)}ms/Max={TriggerActionQueue.ActionTimeCalc.Max.Round(0)}ms/Avg={TriggerActionQueue.ActionTimeCalc.Avg.Round(0)}ms)";

                });
                Global_GUI.InvokeIFRequired(this.lbl_errors, () =>
                {
                    if (LogMan.ErrorCount > 0)
                        this.lbl_errors.Text = $"{LogMan.ErrorCount} error(s) occurred. Click to open Log."; //update error counter label
                    else
                        this.lbl_errors.Text = "";

                });



            }
            catch (Exception ex)
            {

                Log("Error: " + ex.Msg());
            }
            finally
            {
                this.IsStatsUpdating = false;
            }
        }

        //ask before closing AI Tool to prevent accidentally closing
        private async void Shell_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (IsClosing)
                return;

            using var Trace = new Trace();  //This c# 8.0 using feature will auto dispose when the function is done.

            Log($"------Closing------- CloseReason: {e.CloseReason}");


            try
            {
                if (!Restart && !ResetSettings && !CloseImmediately && e.CloseReason != CloseReason.WindowsShutDown && AppSettings.Settings.close_instantly <= 0) //if it's either enabled or not set  -1 = not set | 0 = ask for confirmation | 1 = don't ask
                {
                    using (var form = new InputForm($"Stop and close AI Tool?", "AI Tool", false))
                    {
                        var result = form.ShowDialog();
                        if (AppSettings.Settings.close_instantly == -1)
                        {
                            //if it's the first time, ask if the confirmation dialog should ever appear again
                            using (var form1 = new InputForm($"Confirm closing AI Tool every time?", "AI Tool", false, "NO", "YES"))
                            {
                                var result1 = form1.ShowDialog();
                                if (result1 == DialogResult.Cancel)
                                {
                                    AppSettings.Settings.close_instantly = 0;
                                }
                                else
                                {
                                    AppSettings.Settings.close_instantly = 1;
                                }
                            }
                        }

                        e.Cancel = (result == DialogResult.Cancel);
                    }
                }


                if (!e.Cancel)
                {

                    if (!Restart)
                        Global_GUI.SaveWindowState(this);

                    await AppSettings.SaveAsync(true);  //save settings in any case

                    //if (AITOOL.DeepStackServerControl.IsInstalled && AITOOL.DeepStackServerControl.IsStarted && AppSettings.Settings.deepstack_autostart)
                    //    await AITOOL.DeepStackServerControl.StopAsync();

                    IsClosing = true;

                    if (!AppSettings.AlreadyRunning)
                        Global.SaveRegSetting("LastShutdownState", "graceful shutdown");

                    AITool.WebDashboard.WebDashboardServer.Stop();

                    MasterCTS.Cancel();

                    //wait a bit for the loops to cancel to avoid other threading errors on shutdown and to allow the logs to finish updating if we need to reset settings
                    Global.ResponsiveSleep(1000);

                    FrigateSource.Stop();

                    try
                    {
                        // clean MQTT disconnect so the broker publishes our last-will/offline status right away
                        await AITOOL.mqttClient.DisposeAsync();
                    }
                    catch (Exception ex)
                    {
                        Log($"Debug: MQTT disconnect on shutdown failed: {ex.Msg()}");
                    }

                    if (Restart)
                        Application.Restart();

                    if (ResetSettings)
                    {
                        //make a backup copy:
                        string cursettingsfolder = Path.GetDirectoryName(AppSettings.Settings.SettingsFileName);
                        string baksettingsfolder = cursettingsfolder + "_RESET_" + DateTime.Now.ToString("s").Replace(":", "_");
                        if (Global.DirectoryCopy(cursettingsfolder, baksettingsfolder, true, true))
                        {
                            MessageBox.Show($"Reset successful. Backup folder created.  Folder={baksettingsfolder}");
                            //delete the settings folder
                            try
                            { Directory.Delete(cursettingsfolder, true); }
                            catch { }
                            //restart so it creates all new settings
                            Global.DeleteRegSettings();
                            Application.Restart();
                        }
                        else
                        {
                            MessageBox.Show($"Error: Failed to fully copy to the backup folder.  NOT reset.  Folder={baksettingsfolder}", "Error", buttons: MessageBoxButtons.OK, icon: MessageBoxIcon.Error);
                        }
                    }

                }

            }
            catch { }



        }

        private void button1_Click(object sender, EventArgs e)
        {
            this.ShowErrors();
        }

        private void tableLayoutPanel7_Paint(object sender, PaintEventArgs e)
        {

        }

        private void button2_Click(object sender, EventArgs e)
        {
            using (CommonOpenFileDialog dialog = new CommonOpenFileDialog())
            {
                if (!string.IsNullOrEmpty(this.cmbcaminput.Text))
                {
                    dialog.InitialDirectory = this.cmbcaminput.Text;

                }
                dialog.IsFolderPicker = true;
                if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    this.cmbcaminput.Text = dialog.FileName;
                }
            }
        }

        private void Shell_DpiChanged(object sender, DpiChangedEventArgs e)
        {
            //Controls get really messed up when DPI changes when app is open.   Just trying to figure out the right way to handle....
            Log($"[System DPI Changed from {e.DeviceDpiOld} to {e.DeviceDpiNew}]");

            //we need to figure out how to force a full resize of all components - something is preventing that from happening
            //automatically upon DPI change.   A suspicion is the custom DBLayoutPanel, but its a clusterfuck to try to 
            //revert back to TableLayoutPanel for everything.

        }

        private async void toolStripButton1_Click(object sender, EventArgs e)
        {
            Debug.Print("About to run");
            try
            {
                await Task.Run(() => longrunningtask()).CancelAfter(MasterCTS.Token, "my task canceled");
                Debug.Print("After run");

            }
            catch (Exception ex)
            {
                Debug.Print("Error: " + ex.ToString());
            }
            Debug.Print("Done.");

        }


        private void longrunningtask()
        {
            Debug.Print("Starting sleep...");
            while (true)
            {
                Debug.Print(DateTime.Now + ": working");
                Thread.Sleep(2000);
            }
            Debug.Print("After sleep");  //should never get here

        }

        private void toolStripButton2_Click(object sender, EventArgs e)
        {
            Debug.Print("Canceling...");
            MasterCTS.Cancel();
            MasterCTS.Dispose();
            MasterCTS = new CancellationTokenSource();
            Debug.Print("Canceled.");
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {

        }

        private void button1_Click_1(object sender, EventArgs e)
        {
            UpdateAIURLs();

            using (Frm_AIServers frm = new Frm_AIServers())
            {

                frm.ShowDialog(this);
                //sort AIURLList so that all items enabled are at the top. Use OrderbyDescending so that the order is preserved
                AppSettings.Settings.AIURLList = AppSettings.Settings.AIURLList.OrderByDescending(x => x.Enabled).ToList();
            }

            UpdateAIURLs();
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            ShellLauncher.Open("https://docs.deepstack.cc/windows/index.html");
        }

        private void button3_Click(object sender, EventArgs e)
        {

            if (MessageBox.Show("Are you sure you want to reset ALL settings?", "RESET?", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                ResetSettings = true;
                this.Close();
            }


        }

        private void flowLayoutPanel1_Paint(object sender, PaintEventArgs e)
        {

        }

        private void dbLayoutPanel11_Paint(object sender, PaintEventArgs e)
        {

        }

        private void Shell_Activated(object sender, EventArgs e)
        {
            //bring this form to the top of all other windows:
            this.TopMost = true;
            this.TopMost = false;
            this.Show();
            Log($"Trace: App Activated. TopMost={this.TopMost}, Visible={this.Visible}, state={this.WindowState}, tbicon={this.ShowInTaskbar}, trayicon={this.notifyIcon.Visible}");


        }
    }

    //enhanced TableLayoutPanel loads faster
    public partial class DBLayoutPanel:TableLayoutPanel
    {
        public DBLayoutPanel()
        {
            this.SetStyle(ControlStyles.AllPaintingInWmPaint |
              ControlStyles.OptimizedDoubleBuffer |
              ControlStyles.UserPaint, true);
        }

        public DBLayoutPanel(IContainer container)
        {
            container.Add(this);
            this.SetStyle(ControlStyles.AllPaintingInWmPaint |
              ControlStyles.OptimizedDoubleBuffer |
              ControlStyles.UserPaint, true);
        }
    }
}
