using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Management;
using System.Media;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;

using Innovative.SolarCalculator;

using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

using NAudio.Wave;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

using static AITool.AITOOL;

namespace AITool
{
    public static partial class Global
    {
        public static void SendMessage(MessageType MT, string Descript = "", object Payload = null, [CallerMemberName] string memberName = null)
        {
            if (progress == null)
            {
                //TODO: trying to catch a logging failure, should take out messagebox later
                if (!warnedlog && !Global.IsService)
                {
                    warnedlog = true;
                    string member = "unknown";
                    if (memberName != null)
                        member = memberName;

                    MessageBox.Show($"SendMessage {MT.ToString()} Debug warning: Progress event logger is null? calling function='{member}'");
                }
                return;
            }

            ClsMessage msg = new ClsMessage(MT, Descript, Payload, memberName);

            progress.Report(msg);

        }

        public static async void TelegramControlMessage(string Message, [CallerMemberName] string memberName = null)
        {

            try
            {
                //PAUSE|STOP [CAMNAME] [MINUTES]
                //STOP 30   <<---Stops/pauses all cameras for 30 minutes
                //PAUSE CAMERANAME 30
                //START|RESUME [CAMNAME]
                //RESUME
                //RESUME CAMERANAME

                if (Message.StartsWith("play:", StringComparison.OrdinalIgnoreCase))
                {
                    string file = Message.GetWord("play:", "");
                    string soundfile = Global.FindSoundFile(file);
                    if (soundfile.IsNotNull())
                    {
                        AITOOL.Telegram.SendTextMessageAsync(AppSettings.Settings.telegram_chatids.GetStrAtIndex(0), $"Playing voice file {soundfile}...");

                        if (soundfile.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
                        {
                            PlayOOG(soundfile);
                        }
                        else
                        {
                            Log($"Debug:   Playing sound: {soundfile}...");
                            using SoundPlayer sp = new SoundPlayer(soundfile);
                            sp.PlaySync();
                        }
                    }
                    else
                    {
                        Log($"Error: Sound file not found: {soundfile}");
                        AITOOL.Telegram.SendTextMessageAsync(AppSettings.Settings.telegram_chatids.GetStrAtIndex(0), $"Error: Could not find {soundfile}...");
                    }
                    return;
                }

                List<string> parts = Message.SplitStr(" ", TrimChars: " []");

                bool pause = false;
                bool resume = false;
                double howlong = 525600;  //let it default to the number of minutes in a year
                string camname = "";

                if (parts.Count > 0)
                {
                    if (parts[0].EqualsIgnoreCase("pause") || parts[0].EqualsIgnoreCase("stop"))
                        pause = true;
                    else if (parts[0].EqualsIgnoreCase("resume") || parts[0].EqualsIgnoreCase("start"))
                        resume = true;
                    else if (parts[0].EqualsIgnoreCase("restartcomputer") || parts[0].EqualsIgnoreCase("restartpc") || parts[0].EqualsIgnoreCase("reboot") || parts[0].EqualsIgnoreCase("rebootcomputer") || parts[0].EqualsIgnoreCase("rebootpc"))
                    {
                        await AITOOL.Telegram.SendTextMessageAsync(AppSettings.Settings.telegram_chatids.GetStrAtIndex(0), $"Computer restarting in 10 seconds...");

                        using (Process prc = System.Diagnostics.Process.Start("shutdown.exe", "-r -f -t 10")) { }
                        Application.Exit();
                    }
                    else if (parts[0].EqualsIgnoreCase("shutdowncomputer") || parts[0].EqualsIgnoreCase("shutdownpc"))
                    {
                        await AITOOL.Telegram.SendTextMessageAsync(AppSettings.Settings.telegram_chatids.GetStrAtIndex(0), $"Computer shutting down in 10 seconds...");
                        using (Process prc = System.Diagnostics.Process.Start("shutdown.exe", "-s -f -t 10")) { }

                        Application.Exit();
                    }
                    else if (parts[0].EqualsIgnoreCase("restart") || parts[0].EqualsIgnoreCase("restartaitool"))
                    {
                        await AITOOL.Telegram.SendTextMessageAsync(AppSettings.Settings.telegram_chatids.GetStrAtIndex(0), $"Restarting AITOOL...");
                        Restart = true;
                        Application.Exit();

                    }
                    else if (parts[0].EqualsIgnoreCase("mute"))
                    {
                        await AITOOL.Telegram.SendTextMessageAsync(AppSettings.Settings.telegram_chatids.GetStrAtIndex(0), $"Muting.");
                        Log("Muting.");
                        Mute();
                    }
                    else if (parts[0].EqualsIgnoreCase("unmute"))
                    {
                        await AITOOL.Telegram.SendTextMessageAsync(AppSettings.Settings.telegram_chatids.GetStrAtIndex(0), $"UnMuting.");
                        Log("UnMuting");
                        UnMute();
                    }
                    else if (parts[0].EqualsIgnoreCase("volumeup"))
                    {
                        await AITOOL.Telegram.SendTextMessageAsync(AppSettings.Settings.telegram_chatids.GetStrAtIndex(0), $"VolumeUp.");
                        Log("Volume Up");
                        VolUp();
                    }
                    else if (parts[0].EqualsIgnoreCase("volumedown"))
                    {
                        await AITOOL.Telegram.SendTextMessageAsync(AppSettings.Settings.telegram_chatids.GetStrAtIndex(0), $"VolumeDown.");
                        Log("Volume Down");
                        VolDown();
                    }
                    else if (parts[0].EqualsIgnoreCase("volumeset"))
                    {
                        float num = parts.GetStrAtIndex(1).ToFloat();
                        await AITOOL.Telegram.SendTextMessageAsync(AppSettings.Settings.telegram_chatids.GetStrAtIndex(0), $"VolumeSet {num}");
                        Log($"Volume set {num}");
                        VolSet(num);
                    }
                    else if (parts[0].EqualsIgnoreCase("screenshot"))
                    {
                        await AITOOL.Telegram.SendTextMessageAsync(AppSettings.Settings.telegram_chatids.GetStrAtIndex(0), $"Taking screenshot...");
                        Log($"Debug: Taking screenshot of primary screen @ {Screen.PrimaryScreen.Bounds.Width}x{Screen.PrimaryScreen.Bounds.Height}");

                        using (Bitmap bitmap = new Bitmap(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height))
                        {
                            using (Graphics g = Graphics.FromImage(bitmap))
                            {
                                g.CopyFromScreen(0, 0, 0, 0, bitmap.Size);
                            }

                            //string outfile = Path.Combine(GetTempFolder(), $"Screenshot_{DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss")}.jpg");


                            using (MemoryStream ms = new MemoryStream())
                            {
                                bitmap.Save(ms, ImageFormat.Jpeg);
                                ms.Seek(0, SeekOrigin.Begin); //go back to start
                                await AITOOL.Telegram.SendPhotoAsync(AppSettings.Settings.telegram_chatids.GetStrAtIndex(0), ms, "", "screenshot.jpg", "");
                            }

                            //ClsImageQueueItem img = new ClsImageQueueItem(outfile, 0, false);

                        }
                    }
                    else
                    {
                        Log($"Debug: Unknown AITOOL Telegram control command '{parts[0]}'");
                        AITOOL.Telegram.SendTextMessageAsync(AppSettings.Settings.telegram_chatids.GetStrAtIndex(0), $"Debug: Unknown Telegram control command '{parts[0]}'");
                    }

                    if (pause || resume)
                    {
                        //check if the second parameter is a camera name or number
                        if (parts.Count > 1)
                        {
                            if (parts[1].IsNumeric())
                            {
                                howlong = parts[1].ToDouble();
                            }
                            else
                            {
                                camname = parts[1];
                            }
                        }

                        if (parts.Count > 2 && parts[2].IsNumeric())
                        {
                            howlong = parts[2].ToDouble();
                        }

                        if (camname.IsNotEmpty())
                        {
                            Camera cam = AITOOL.GetCamera(camname);
                            if (cam != null)
                            {
                                if (pause)
                                {
                                    cam.PauseMinutes = howlong;
                                    cam.PauseFileMon = true;
                                    cam.PauseMQTT = true;
                                    cam.PausePushover = true;
                                    cam.PauseTelegram = true;
                                    cam.PauseURL = true;
                                    cam.Pause();
                                    AITOOL.Telegram.SendTextMessageAsync(AppSettings.Settings.telegram_chatids.GetStrAtIndex(0), $"Camera '{camname}' paused {howlong} minutes.");
                                }
                                else
                                {
                                    cam.PauseFileMon = false;
                                    cam.PauseMQTT = false;
                                    cam.PausePushover = false;
                                    cam.PauseTelegram = false;
                                    cam.PauseURL = false;
                                    cam.Resume();
                                    AITOOL.Telegram.SendTextMessageAsync(AppSettings.Settings.telegram_chatids.GetStrAtIndex(0), $"Camera '{camname}' resumed.");
                                }
                            }
                            else
                            {
                                Log($"Error: Camera '{camname}' not found.");
                                AITOOL.Telegram.SendTextMessageAsync(AppSettings.Settings.telegram_chatids.GetStrAtIndex(0), $"Error: Camera '{camname}' not found.");
                            }
                        }
                        else
                        {
                            string pausemsg = "";
                            if (pause)
                                pausemsg = $"All cameras paused {howlong} minutes.";
                            else
                                pausemsg = $"All cameras resumed.";


                            foreach (var cam in AppSettings.Settings.CameraList)
                            {
                                if (pause)
                                {
                                    cam.PauseMinutes = howlong;
                                    cam.PauseFileMon = true;
                                    cam.PauseMQTT = true;
                                    cam.PausePushover = true;
                                    cam.PauseTelegram = true;
                                    cam.PauseURL = true;
                                    cam.Pause();
                                }
                                else
                                {
                                    cam.PauseFileMon = false;
                                    cam.PauseMQTT = false;
                                    cam.PausePushover = false;
                                    cam.PauseTelegram = false;
                                    cam.PauseURL = false;
                                    cam.Resume();
                                }
                            }

                            AITOOL.Telegram.SendTextMessageAsync(AppSettings.Settings.telegram_chatids.GetStrAtIndex(0), pausemsg);

                        }

                    }

                }
                else
                {
                    Log("Debug: empty Telegram control message?");
                }

            }
            catch (Exception ex)
            {
                Log($"Error: {ex.Msg()}");
            }


        }
        public static void DeleteHistoryItem(string filename, [CallerMemberName] string memberName = null)
        {
            if (progress == null)
            {
                //TODO: trying to catch a logging failure, should take out messagebox later
                if (!warnedlog && !Global.IsService)
                {
                    warnedlog = true;
                    string member = "unknown";
                    if (memberName != null)
                        member = memberName;

                    MessageBox.Show($"DeleteHistoryItem Debug warning: Progress event logger is null? calling function='{member}'");
                }
                return;
            }

            ClsMessage msg = new ClsMessage(MessageType.DeleteHistoryItem, filename, null, memberName);

            progress.Report(msg);

        }
        public static void UpdateProgressBar(string label, int CurVal = -1, int MinVal = -1, int MaxVal = -1, [CallerMemberName] string memberName = null)
        {
            if (progress == null)
            {
                //TODO: trying to catch a logging failure, should take out messagebox later
                if (!warnedlog && !Global.IsService)
                {
                    warnedlog = true;
                    string member = "unknown";
                    if (memberName != null)
                        member = memberName;

                    MessageBox.Show($"UpdateProgressBar Debug warning: Progress event logger is null? calling function='{member}'");
                }
                return;
            }

            ClsMessage msg = new ClsMessage(MessageType.UpdateProgressBar, label, null, memberName, CurVal, MinVal, MaxVal);

            progress.Report(msg);

        }

        public static void CreateHistoryItem(History hist, [CallerMemberName] string memberName = null)
        {
            if (progress == null)
            {
                //TODO: trying to catch a logging failure, should take out messagebox later
                if (!warnedlog && !Global.IsService)
                {
                    warnedlog = true;
                    string member = "unknown";
                    if (memberName != null)
                        member = memberName;

                    MessageBox.Show($"CreateHistoryItem Debug warning: Progress event logger is null? calling function='{member}'");
                }
                return;
            }

            ClsMessage msg = new ClsMessage(MessageType.CreateHistoryItem, "", hist, memberName);

            progress.Report(msg);

        }

        public static void UpdateLabel(string Message, string LabelControlName, [CallerMemberName] string memberName = null)
        {
            if (progress == null)
            {
                //TODO: trying to catch a logging failure, should take out messagebox later
                if (!warnedlog && !Global.IsService)
                {
                    warnedlog = true;
                    string member = "unknown";
                    if (memberName != null)
                        member = memberName;

                    MessageBox.Show($"UpdateLabel Debug warning: Progress event logger is null? calling function='{member}'");
                }
                return;
            }

            ClsMessage msg = new ClsMessage(MessageType.UpdateLabel, Message, LabelControlName, memberName);

            progress.Report(msg);

        }
        private static bool warnedlog = false;
        public static void LogMessage(string Message, [CallerMemberName] string memberName = null)
        {
            if (progress == null)
            {
                //TODO: trying to catch a logging failure, should take out messagebox later
                if (!warnedlog && !Global.IsService)
                {
                    warnedlog = true;
                    string member = "unknown";
                    if (memberName != null)
                        member = memberName;

                    MessageBox.Show($"Log Debug warning: Progress event logger is null? calling function='{member}'");
                }
                return;
            }

            ClsMessage msg = new ClsMessage(MessageType.LogEntry, "", null, memberName);

            //this is for logging in non-gui classes.  Reports back to real logger
            //progress needs to be subscribed to in main gui
            string mn = "";
            if (memberName != null && !string.IsNullOrEmpty(memberName))
            {
                mn = $"{memberName}>> ";
            }
            msg.Description = $"{mn}{Message}";

            SaveRegSetting("LastLogEntry", msg.Description);
            Global.SaveRegSetting("LastShutdownState", $"checkpoint: Global.Log: {DateTime.Now}");


            progress.Report(msg);

        }
    }
}
