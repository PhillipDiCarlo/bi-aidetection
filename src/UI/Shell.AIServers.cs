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

        private async Task SaveDeepStackTabAsync()
        {

            Global_GUI.InvokeIFRequired(this, async () =>
            {

                if (DeepStackServerControl == null)
                    DeepStackServerControl = new DeepStack(AppSettings.Settings.deepstack_adminkey, AppSettings.Settings.deepstack_apikey, AppSettings.Settings.deepstack_mode, AppSettings.Settings.deepstack_sceneapienabled, AppSettings.Settings.deepstack_faceapienabled, AppSettings.Settings.deepstack_detectionapienabled, AppSettings.Settings.deepstack_port, AppSettings.Settings.deepstack_customModelPath, AppSettings.Settings.deepstack_stopbeforestart, AppSettings.Settings.deepstack_customModelName, AppSettings.Settings.deepstack_customModelPort, AppSettings.Settings.deepstack_customModelMode, AppSettings.Settings.deepstack_customModelApiEnabled);

                DeepStackServerControl.GetDeepStackRun();

                if (this.RB_Medium.Checked)
                    AppSettings.Settings.deepstack_mode = "Medium";
                if (this.RB_Low.Checked)
                    AppSettings.Settings.deepstack_mode = "Low";
                if (this.RB_High.Checked)
                    AppSettings.Settings.deepstack_mode = "High";

                AppSettings.Settings.deepstack_detectionapienabled = this.Chk_DetectionAPI.Checked;
                AppSettings.Settings.deepstack_faceapienabled = this.Chk_FaceAPI.Checked;
                AppSettings.Settings.deepstack_sceneapienabled = this.Chk_SceneAPI.Checked;
                AppSettings.Settings.deepstack_autostart = this.Chk_AutoStart.Checked;
                AppSettings.Settings.deepstack_autoadd = this.chk_AutoAdd.Checked;
                AppSettings.Settings.deepstack_debug = this.Chk_DSDebug.Checked;
                AppSettings.Settings.deepstack_highpriority = this.chk_HighPriority.Checked;
                //AppSettings.Settings.deepstack_adminkey = this.Txt_AdminKey.Text.Trim();
                //AppSettings.Settings.deepstack_apikey = this.Txt_APIKey.Text.Trim();
                AppSettings.Settings.deepstack_installfolder = this.Txt_DeepStackInstallFolder.Text.Trim();
                AppSettings.Settings.deepstack_port = this.Txt_Port.Text.Trim();
                AppSettings.Settings.deepstack_customModelPath = this.Txt_CustomModelPath.Text.Trim();
                AppSettings.Settings.deepstack_customModelName = this.Txt_CustomModelName.Text.Trim();
                AppSettings.Settings.deepstack_customModelPort = this.Txt_CustomModelPort.Text.Trim();
                AppSettings.Settings.deepstack_customModelMode = this.Txt_CustomModelMode.Text.Trim();
                AppSettings.Settings.deepstack_customModelApiEnabled = this.Chk_CustomModelAPI.Checked;

                AppSettings.Settings.deepstack_stopbeforestart = this.chk_stopbeforestart.Checked;

                AppSettings.Settings.deepstack_autorestart = this.Chk_AutoReStart.Checked;
                AppSettings.Settings.deepstack_autorestart_fail_count = GetNumberInt(this.txt_DeepstackRestartFailCount.Text);
                AppSettings.Settings.deepstack_autorestart_minutes_between_restart_attempts = this.txt_DeepstackNoMoreOftenThanMins.Text.ToDouble();

                if (AppSettings.Settings.deepstack_autorestart_fail_count >= AppSettings.Settings.MaxQueueItemRetries)
                {
                    MessageBox.Show($"Note: Deepstack restart fail count is '{AppSettings.Settings.deepstack_autorestart_fail_count}' but the maximum \r\nnumber of times a URL can fail before being disabled is '{AppSettings.Settings.MaxQueueItemRetries}'\r\nTo change, see 'MaxQueueItemRetries' in AITOOL.SETTINGS.JSON file.");
                }

                await AppSettings.SaveAsync(true);


                if (DeepStackServerControl.IsInstalled)
                {
                    if (DeepStackServerControl.IsStarted)
                    {


                        if (DeepStackServerControl.IsActivated)
                        {
                            this.Lbl_BlueStackRunning.Text = "*RUNNING*";
                            this.Lbl_BlueStackRunning.ForeColor = Color.Green;

                            this.Btn_Start.Enabled = false;
                            this.Btn_Stop.Enabled = true;
                        }
                        else
                        {
                            this.Lbl_BlueStackRunning.Text = "*NOT ACTIVATED, RUNNING*";

                            this.Btn_Start.Enabled = false;
                            this.Btn_Stop.Enabled = true;
                        }
                    }
                    else
                    {
                        if (DeepStackServerControl.Starting)
                        {
                            this.Lbl_BlueStackRunning.Text = "STARTING...";
                            this.Lbl_BlueStackRunning.ForeColor = Color.DodgerBlue;
                            this.Btn_Start.Enabled = false;
                            this.Btn_Stop.Enabled = true;

                        }
                        else if (DeepStackServerControl.Stopping)
                        {
                            this.Lbl_BlueStackRunning.Text = "STOPPING...";
                            this.Lbl_BlueStackRunning.ForeColor = Color.DodgerBlue;
                            this.Btn_Start.Enabled = false;
                            this.Btn_Stop.Enabled = false;

                        }
                        else
                        {
                            this.Lbl_BlueStackRunning.Text = "*NOT RUNNING*";
                            this.Lbl_BlueStackRunning.ForeColor = Color.Black;

                            this.Btn_Start.Enabled = true;
                            this.Btn_Stop.Enabled = false;

                        }
                    }
                }
                else
                {
                    this.Btn_Start.Enabled = false;
                    this.Btn_Stop.Enabled = false;
                    this.Lbl_BlueStackRunning.Text = "*NOT INSTALLED*";


                }

                DeepStackServerControl.Update(AppSettings.Settings.deepstack_adminkey, AppSettings.Settings.deepstack_apikey, AppSettings.Settings.deepstack_mode, AppSettings.Settings.deepstack_sceneapienabled, AppSettings.Settings.deepstack_faceapienabled, AppSettings.Settings.deepstack_detectionapienabled, AppSettings.Settings.deepstack_port, AppSettings.Settings.deepstack_customModelPath, AppSettings.Settings.deepstack_stopbeforestart, AppSettings.Settings.deepstack_customModelName, AppSettings.Settings.deepstack_customModelPort, AppSettings.Settings.deepstack_customModelMode, AppSettings.Settings.deepstack_customModelApiEnabled);


            });

        }

        private async Task LoadDeepStackTab()
        {

            try
            {

                Global_GUI.InvokeIFRequired(this, () =>
                {

                    if (DeepStackServerControl == null)
                        DeepStackServerControl = new DeepStack(AppSettings.Settings.deepstack_adminkey, AppSettings.Settings.deepstack_apikey, AppSettings.Settings.deepstack_mode, AppSettings.Settings.deepstack_sceneapienabled, AppSettings.Settings.deepstack_faceapienabled, AppSettings.Settings.deepstack_detectionapienabled, AppSettings.Settings.deepstack_port, AppSettings.Settings.deepstack_customModelPath, AppSettings.Settings.deepstack_stopbeforestart, AppSettings.Settings.deepstack_customModelName, AppSettings.Settings.deepstack_customModelPort, AppSettings.Settings.deepstack_customModelMode, AppSettings.Settings.deepstack_customModelApiEnabled);


                    //first update the port in the deepstack_url if found
                    //string prt = Global.GetWordBetween(AppSettings.Settings.deepstack_url, ":", " |/");
                    //if (!string.IsNullOrEmpty(prt) && (Convert.ToInt32(prt) > 0))
                    //{
                    //    DeepStackServerControl.Port = prt;
                    //}

                    //This will OVERRIDE the port if the deepstack processes found running already have a different port, mode, etc:
                    DeepStackServerControl.GetDeepStackRun();

                    if (string.Equals(DeepStackServerControl.Mode, "medium", StringComparison.OrdinalIgnoreCase))
                        this.RB_Medium.Checked = true;
                    if (string.Equals(DeepStackServerControl.Mode, "low", StringComparison.OrdinalIgnoreCase))
                        this.RB_Low.Checked = true;
                    if (string.Equals(DeepStackServerControl.Mode, "high", StringComparison.OrdinalIgnoreCase))
                        this.RB_High.Checked = true;

                    this.Chk_DetectionAPI.Checked = DeepStackServerControl.DetectionAPIEnabled;
                    this.Chk_FaceAPI.Checked = DeepStackServerControl.FaceAPIEnabled;
                    this.Chk_SceneAPI.Checked = DeepStackServerControl.SceneAPIEnabled;
                    this.Chk_CustomModelAPI.Checked = DeepStackServerControl.CustomModelEnabled;

                    Global_GUI.GroupboxEnableDisable(groupBoxCustomModel, Chk_CustomModelAPI);

                    //have seen a few cases nothing is checked but it is required
                    if (!this.Chk_DetectionAPI.Checked && !this.Chk_FaceAPI.Checked && !this.Chk_SceneAPI.Checked && !this.Chk_CustomModelAPI.Checked)
                    {
                        //If NOTHING else is enabled, force regular detection to be enabled
                        this.Chk_DetectionAPI.Checked = true;
                        DeepStackServerControl.DetectionAPIEnabled = true;
                    }

                    this.Chk_AutoStart.Checked = AppSettings.Settings.deepstack_autostart;
                    this.chk_AutoAdd.Checked = AppSettings.Settings.deepstack_autoadd;
                    this.Chk_DSDebug.Checked = AppSettings.Settings.deepstack_debug;
                    this.chk_HighPriority.Checked = AppSettings.Settings.deepstack_highpriority;
                    //this.Txt_AdminKey.Text = DeepStackServerControl.AdminKey;
                    //this.Txt_APIKey.Text = DeepStackServerControl.APIKey;
                    this.Txt_DeepStackInstallFolder.Text = DeepStackServerControl.DeepStackFolder;
                    this.Txt_Port.Text = DeepStackServerControl.Port;
                    this.Txt_CustomModelPath.Text = AppSettings.Settings.deepstack_customModelPath;
                    this.Txt_CustomModelName.Text = AppSettings.Settings.deepstack_customModelName;
                    this.Txt_CustomModelPort.Text = AppSettings.Settings.deepstack_customModelPort;
                    this.Txt_CustomModelMode.Text = AppSettings.Settings.deepstack_customModelMode;
                    this.chk_stopbeforestart.Checked = AppSettings.Settings.deepstack_stopbeforestart;

                    this.Chk_AutoReStart.Checked = AppSettings.Settings.deepstack_autorestart;
                    this.txt_DeepstackRestartFailCount.Text = AppSettings.Settings.deepstack_autorestart_fail_count.ToString();
                    this.txt_DeepstackNoMoreOftenThanMins.Text = AppSettings.Settings.deepstack_autorestart_minutes_between_restart_attempts.ToString();

                    if (!DeepStackServerControl.IsNewVersion)
                    {
                        this.Txt_CustomModelPath.Enabled = false;
                        this.Txt_CustomModelName.Enabled = false;
                        this.Txt_CustomModelPort.Enabled = false;
                    }

                    this.tb_DeepstackCommandLine.Text = DeepStackServerControl.CommandLine;
                    this.tb_DeepStackURLs.Text = DeepStackServerControl.URLS;

                    //if (prt != Txt_Port.Text)
                    //{
                    //    //server:port/maybe/more/path
                    //    string serv = Global.GetWordBetween(AppSettings.Settings.deepstack_url, "", ":");
                    //    if (!string.IsNullOrEmpty(serv))
                    //    {
                    //        tbDeepstackUrl.Text = serv + ":" + Txt_Port.Text;
                    //        //AppSettings.Settings.deepstack_url = serv + ":" + Txt_Port.Text;
                    //        //AppSettings.Settings.deepstack_url = tbDeepstackUrl.Text;
                    //        //AppSettings.Save();
                    //    }
                    //}

                    if (DeepStackServerControl.IsInstalled)
                    {
                        lbl_deepstackname.Text = DeepStackServerControl.DisplayName;
                        lbl_Deepstackversion.Text = DeepStackServerControl.DisplayVersion;
                        lbl_DeepstackType.Text = DeepStackServerControl.Type.ToString();

                        if (DeepStackServerControl.IsStarted && !DeepStackServerControl.HasError)
                        {
                            if (DeepStackServerControl.IsActivated && (DeepStackServerControl.VisionDetectionRunning || DeepStackServerControl.DetectionAPIEnabled || DeepStackServerControl.CustomModelEnabled || DeepStackServerControl.FaceAPIEnabled))
                            {

                                this.Lbl_BlueStackRunning.Text = "*RUNNING*";
                                this.Lbl_BlueStackRunning.ForeColor = Color.Green;

                                this.Btn_Start.Enabled = false;
                                this.Btn_Stop.Enabled = true;
                            }
                            else if (!DeepStackServerControl.IsActivated)
                            {
                                this.Lbl_BlueStackRunning.Text = "*NOT ACTIVATED, RUNNING*";

                                this.Btn_Start.Enabled = false;
                                this.Btn_Stop.Enabled = true;
                            }
                            else if (!DeepStackServerControl.VisionDetectionRunning || DeepStackServerControl.DetectionAPIEnabled)
                            {
                                this.Lbl_BlueStackRunning.Text = "*DETECTION API NOT RUNNING*";

                                this.Btn_Start.Enabled = false;
                                this.Btn_Stop.Enabled = true;
                            }

                        }
                        else if (DeepStackServerControl.HasError)
                        {
                            this.Lbl_BlueStackRunning.Text = "*ERROR*";
                            this.Lbl_BlueStackRunning.ForeColor = Color.Red;

                            this.Btn_Start.Enabled = true;
                            this.Btn_Stop.Enabled = true;
                        }
                        else
                        {
                            if (DeepStackServerControl.Starting)
                            {
                                this.Lbl_BlueStackRunning.Text = "STARTING...";
                                this.Lbl_BlueStackRunning.ForeColor = Color.DodgerBlue;
                                this.Btn_Start.Enabled = false;
                                this.Btn_Stop.Enabled = true;

                            }
                            else if (DeepStackServerControl.Stopping)
                            {
                                this.Lbl_BlueStackRunning.Text = "STOPPING...";
                                this.Lbl_BlueStackRunning.ForeColor = Color.DodgerBlue;
                                this.Btn_Start.Enabled = false;
                                this.Btn_Stop.Enabled = false;

                            }
                            else
                            {
                                this.Lbl_BlueStackRunning.Text = "*NOT RUNNING*";
                                this.Lbl_BlueStackRunning.ForeColor = Color.Black;

                                this.Btn_Start.Enabled = true;
                                this.Btn_Stop.Enabled = false;

                            }
                            //if (this.Chk_AutoStart.Checked && StartIfNeeded)
                            //{
                            //    if (await DeepStackServerControl.StartDeepstackAsync())
                            //    {
                            //        if (DeepStackServerControl.IsStarted && !DeepStackServerControl.HasError)
                            //        {
                            //            LabelUpdate = delegate { this.Lbl_BlueStackRunning.Text = "*RUNNING*"; };
                            //            this.Invoke(LabelUpdate);
                            //            this.Btn_Start.Enabled = false;
                            //            this.Btn_Stop.Enabled = true;
                            //        }
                            //        else if (DeepStackServerControl.HasError)
                            //        {
                            //            LabelUpdate = delegate { this.Lbl_BlueStackRunning.Text = "*ERROR*"; };
                            //            this.Invoke(LabelUpdate);

                            //            this.Btn_Start.Enabled = false;
                            //            this.Btn_Stop.Enabled = true;
                            //        }

                            //    }
                            //    else
                            //    {
                            //        LabelUpdate = delegate { this.Lbl_BlueStackRunning.Text = "*ERROR*"; };
                            //        this.Invoke(LabelUpdate);

                            //        this.Btn_Start.Enabled = false;
                            //        this.Btn_Stop.Enabled = true;
                            //    }
                            //}
                        }
                    }
                    else
                    {
                        this.Btn_Start.Enabled = false;
                        this.Btn_Stop.Enabled = false;
                        this.Lbl_BlueStackRunning.Text = "*NOT INSTALLED*";


                    }

                });


            }
            catch (Exception ex)
            {

                Log(ex.Msg());
            }
        }

        private async void Btn_Start_Click(object sender, EventArgs e)
        {
            Global_GUI.InvokeIFRequired(this, () =>
            {
                this.Lbl_BlueStackRunning.Text = "STARTING...";
                this.Btn_Start.Enabled = false;
                this.Btn_Stop.Enabled = false;

            });
            await this.SaveDeepStackTabAsync();
            await DeepStackServerControl.StartDeepstackAsync();
            //MessageBox.Show("Started");
            await this.LoadDeepStackTab();

        }

        private async void Btn_Save_Click(object sender, EventArgs e)
        {
            await this.SaveDeepStackTabAsync();
        }

        private async void Btn_Stop_Click(object sender, EventArgs e)
        {
            Global_GUI.InvokeIFRequired(this, () =>
            {
                this.Lbl_BlueStackRunning.Text = "STOPPING...";
                this.Btn_Start.Enabled = false;
                this.Btn_Stop.Enabled = false;

            });
            await this.SaveDeepStackTabAsync();
            await DeepStackServerControl.StopDeepstackAsync();
            //MessageBox.Show("Stopped");
            await this.LoadDeepStackTab();
        }

        private void tbDeepstackUrl_TextChanged(object sender, EventArgs e)
        {

        }

        private void bt_DeepstackReset_Click(object sender, EventArgs e)
        {
            this.Lbl_BlueStackRunning.Text = "RESETTING...";
            this.Btn_Start.Enabled = false;
            this.Btn_Stop.Enabled = false;
            this.Btn_DeepstackReset.Enabled = false;
            this.SaveDeepStackTabAsync();
            DeepStackServerControl.ResetDeepstack();
            this.Btn_DeepstackReset.Enabled = true;
            this.LoadDeepStackTab();
        }

        private void Btn_ViewLog_Click(object sender, EventArgs e)
        {
            string errfile = Path.Combine(Environment.GetEnvironmentVariable("LOCALAPPDATA"), "DeepStack", "logs", "stderr.txt");
            if (File.Exists(errfile))
            {
                if (new FileInfo(errfile).Length > 4)
                {
                    try
                    {
                        ShellLauncher.Open(errfile);
                    }
                    catch (Exception)
                    {
                        MessageBox.Show("Error:  Please correct the WINDOWS File Association for .TXT files.");
                    }
                }
                else
                    MessageBox.Show("File has no lines " + errfile);
            }
            else
            {
                MessageBox.Show("Cannot find " + errfile);
            }
        }

        private void Txt_CustomModelName_TextChanged(object sender, EventArgs e)
        {

        }

        private void Chk_CustomModelAPI_CheckedChanged(object sender, EventArgs e)
        {
            Global_GUI.GroupboxEnableDisable(groupBoxCustomModel, Chk_CustomModelAPI);
        }
    }
}
