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
        private void LoadSettingsTab()
        {

            // fill settings tab with stored settings

            this.cmbInput.Text = AppSettings.Settings.input_path;
            this.cb_inputpathsubfolders.Checked = AppSettings.Settings.input_path_includesubfolders;
            this.cmbInput.Items.Clear();
            foreach (string pth in AITOOL.BlueIrisInfo.ClipPaths)
            {
                this.cmbInput.Items.Add(pth);

            }

            //this.tbDeepstackUrl.Text = AppSettings.Settings.deepstack_url;
            this.cb_DeepStackURLsQueued.Checked = AppSettings.Settings.deepstack_urls_are_queued;

            this.Chk_AutoScroll.Checked = AppSettings.Settings.Autoscroll_log;

            this.tb_telegram_chatid.Text = String.Join(",", AppSettings.Settings.telegram_chatids);
            this.tb_telegram_token.Text = AppSettings.Settings.telegram_token;
            this.tb_telegram_cooldown.Text = AppSettings.Settings.telegram_cooldown_seconds.ToString();

            this.cb_send_telegram_errors.Checked = AppSettings.Settings.send_telegram_errors;
            this.cb_send_pushover_errors.Checked = AppSettings.Settings.send_pushover_errors;
            this.cbStartWithWindows.Checked = AppSettings.Settings.startwithwindows;
            this.cbMinimizeToTray.Checked = AppSettings.Settings.MinimizeToTray;

            this.tb_username.Text = AppSettings.Settings.DefaultUserName;
            this.tb_password.Text = AppSettings.Settings.DefaultPasswordEncrypted.Decrypt();
            this.cb_BlueIrisUseSessionLogin.Checked = AppSettings.Settings.BlueIrisUseSessionLogin;

            this.tb_BlueIrisServer.Text = AppSettings.Settings.BlueIrisServer;


            if (AITOOL.BlueIrisInfo.Result == BlueIrisResult.Valid)
            {
                lbl_blueirisserver.Text = "BI Config: " + AITOOL.BlueIrisInfo.Result;
                lbl_blueirisserver.Text += $";  WebServer is configured for {AITOOL.BlueIrisInfo.URL}";
                lbl_blueirisserver.ForeColor = Color.DodgerBlue;
                if (Global.IsValidIPAddress(AppSettings.Settings.BlueIrisServer, out IPAddress foundip) && !AppSettings.Settings.BlueIrisServer.EqualsIgnoreCase(AITOOL.BlueIrisInfo.ServerName) && AppSettings.Settings.BlueIrisServer != "127.0.0.1")
                {
                    Log($"Warning: BlueIris Settings > Web Server > Local IP address is set to a different IP: AITOOL={AppSettings.Settings.BlueIrisServer}, BI={AITOOL.BlueIrisInfo.ServerName}");
                }
            }
            else if (!AppSettings.Settings.BlueIrisServer.IsEmpty())
            {
                lbl_blueirisserver.Text = "BI Config: Error - " + AITOOL.BlueIrisInfo.Result;
                lbl_blueirisserver.ForeColor = Color.MediumPurple;
            }
            else
            {
                lbl_blueirisserver.Text = "";
            }

            this.tb_Pushover_APIKey.Text = AppSettings.Settings.pushover_APIKey;
            this.tb_Pushover_UserKey.Text = AppSettings.Settings.pushover_UserKey;
            this.tb_Pushover_Cooldown.Text = AppSettings.Settings.pushover_cooldown_seconds.ToString();

        }


        //----------------------------------------------------------------------------------------------------------
        //SETTING TAB
        //----------------------------------------------------------------------------------------------------------



        //settings save button
        private async void BtnSettingsSave_Click_1(object sender, EventArgs e)
        {
            using var Trace = new Trace();  //This c# 8.0 using feature will auto dispose when the function is done.

            Global_GUI.InvokeIFRequired(this.BtnSettingsSave, () =>
            {
                BtnSettingsSave.Enabled = false;
                BtnSettingsSave.Text = "Saving...";
            });


            Application.DoEvents();

            Log($"Saving settings to {AppSettings.Settings.SettingsFileName}");
            //save inputted settings into App.settings
            AppSettings.Settings.input_path = this.cmbInput.Text.Trim();
            AppSettings.Settings.input_path_includesubfolders = this.cb_inputpathsubfolders.Checked;
            AppSettings.Settings.deepstack_urls_are_queued = this.cb_DeepStackURLsQueued.Checked;
            AppSettings.Settings.telegram_chatids = this.tb_telegram_chatid.Text.SplitStr("|;,", true, true);
            AppSettings.Settings.telegram_token = this.tb_telegram_token.Text.Trim();
            AppSettings.Settings.telegram_cooldown_seconds = GetNumberInt(this.tb_telegram_cooldown.Text.Trim());
            AppSettings.Settings.send_telegram_errors = this.cb_send_telegram_errors.Checked;
            AppSettings.Settings.send_pushover_errors = this.cb_send_pushover_errors.Checked;
            AppSettings.Settings.startwithwindows = this.cbStartWithWindows.Checked;
            AppSettings.Settings.MinimizeToTray = this.cbMinimizeToTray.Checked;

            AppSettings.Settings.DefaultUserName = this.tb_username.Text.Trim();
            AppSettings.Settings.DefaultPasswordEncrypted = this.tb_password.Text.Trim().Encrypt();
            AppSettings.Settings.BlueIrisUseSessionLogin = this.cb_BlueIrisUseSessionLogin.Checked;

            AppSettings.Settings.BlueIrisServer = this.tb_BlueIrisServer.Text.Trim();

            AppSettings.Settings.pushover_APIKey = this.tb_Pushover_APIKey.Text.Trim();
            AppSettings.Settings.pushover_UserKey = this.tb_Pushover_UserKey.Text.Trim();
            AppSettings.Settings.pushover_cooldown_seconds = GetNumberInt(this.tb_Pushover_Cooldown.Text.Trim());

            Global.Startup(AppSettings.Settings.startwithwindows);

            UpdateAIURLs();

            if (await AppSettings.SaveAsync(true))
            {
                Log("...Saved.");
            }
            else
            {
                Log("...Not saved.  No changes?");
            }
            //update variables
            //input_path = AppSettings.Settings.input_path;
            //deepstack_url = AppSettings.Settings.deepstack_url;
            //telegram_chatid = AppSettings.Settings.telegram_chatid;
            //telegram_chatids = telegram_chatid.Replace(" ", "").Split(','); //for multiple Telegram chats that receive alert images
            //telegram_token = AppSettings.Settings.telegram_token;
            //log_everything = AppSettings.Settings.log_everything;
            //send_errors = AppSettings.Settings.send_errors;

            Application.DoEvents();

            //update fswatcher to watch new input folder
            UpdateWatchers(true);

            await FrigateSource.RestartAsync();

            //Update blue iris info
            Application.DoEvents();

            await AITOOL.BlueIrisInfo.RefreshBIInfoAsync(AppSettings.Settings.BlueIrisServer);

            AITOOL.UpdateLatLong();

            Application.DoEvents();

            await AITOOL.Telegram.TryStartTelegram();

            this.cmbInput.Items.Clear();
            foreach (string pth in AITOOL.BlueIrisInfo.ClipPaths)
                this.cmbInput.Items.Add(pth);


            this.LoadSettingsTab();

            if (AITOOL.BlueIrisInfo.Result != BlueIrisResult.Valid && AITOOL.BlueIrisInfo.Result != BlueIrisResult.NotInstalled)
                MessageBox.Show($"Error: Could not connect to BlueIris server: '{AITOOL.BlueIrisInfo.Result}'.  See log for more detail.", "Error", MessageBoxButtons.OK, icon: MessageBoxIcon.Error);

            Global_GUI.InvokeIFRequired(this.BtnSettingsSave, () =>
            {
                BtnSettingsSave.Enabled = true;
                BtnSettingsSave.Text = "Save";
            });

            Application.DoEvents();


        }

        //input path select dialog button
        private void btn_input_path_Click(object sender, EventArgs e)
        {
            using (CommonOpenFileDialog dialog = new CommonOpenFileDialog())
            {
                if (!string.IsNullOrEmpty(this.cmbInput.Text))
                {
                    dialog.InitialDirectory = this.cmbInput.Text;

                }
                dialog.IsFolderPicker = true;
                if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    this.cmbInput.Text = dialog.FileName;
                }
            }
        }

        private async void btn_TestBlueIrisLogin_Click(object sender, EventArgs e)
        {
            using var Trace = new Trace();  //This c# 8.0 using feature will auto dispose when the function is done.

            string server = this.tb_BlueIrisServer.Text.Trim();
            string username = this.tb_username.Text.Trim();
            string password = this.tb_password.Text.Trim();

            if (server.IsEmpty())
            {
                MessageBox.Show("Enter a BlueIris server name/IP first.", "BlueIris Session Login", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            //prefer the port BlueIris itself reported via the registry (AITOOL.BlueIrisInfo), fall back to the conventional default port
            string baseurl;
            if (AITOOL.BlueIrisInfo != null && AITOOL.BlueIrisInfo.Result == BlueIrisResult.Valid && AITOOL.BlueIrisInfo.ServerName.EqualsIgnoreCase(server) && AITOOL.BlueIrisInfo.URL.IsNotEmpty())
                baseurl = AITOOL.BlueIrisInfo.URL;
            else
                baseurl = $"http://{server}:81";

            this.btn_TestBlueIrisLogin.Enabled = false;
            try
            {
                BlueIrisSession.InvalidateSession(baseurl); //always test with the currently typed-in credentials, don't reuse a cached session
                await BlueIrisSession.GetSessionIdAsync(baseurl, username, password);
                string info = BlueIrisSession.GetLastLoginInfo(baseurl);
                MessageBox.Show($"Login succeeded for '{baseurl}'.{(info.IsNotEmpty() ? "\n" + info : "")}", "BlueIris Session Login", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Login failed for '{baseurl}':\n{ex.Msg()}", "BlueIris Session Login", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                this.btn_TestBlueIrisLogin.Enabled = true;
            }
        }


        private void BtnDynamicMaskingSettings_Click(object sender, EventArgs e)
        {
            if (this.FOLV_Cameras.SelectedObjects.Count == 0)
            {
                MessageBox.Show("Select a camera first");
                return;
            }

            using (Frm_DynamicMasking frm = new Frm_DynamicMasking())
            {
                Camera cam = AITOOL.GetCamera(((Camera)this.FOLV_Cameras.SelectedObjects[0]).Name);
                frm.cam = cam;
                frm.Text = "Dynamic Masking Settings - " + cam.Name;

                //Merge ClassObject's code
                frm.num_history_mins.Value = cam.maskManager.HistorySaveMins;//load minutes to retain history objects that have yet to become masks
                frm.num_mask_create.Value = cam.maskManager.HistoryThresholdCount; // load mask create counter
                frm.num_mask_remove.Value = cam.maskManager.MaskRemoveMins; //load mask remove counter
                frm.num_percent_var.Value = (decimal)cam.maskManager.PercentMatch;
                frm.numMaskThreshold.Value = cam.maskManager.MaskRemoveThreshold;

                frm.num_max_unused.Value = cam.maskManager.MaxMaskUnusedDays;

                frm.cb_enabled.Checked = this.cb_masking_enabled.Checked;

                frm.lbl_objects.Text = cam.maskManager.MaskTriggeringObjects.ToString();

                if (frm.ShowDialog() == DialogResult.OK)
                {
                    ////get masking values from textboxes

                    cam.maskManager.HistorySaveMins = frm.num_history_mins.Text.ToInt();
                    cam.maskManager.HistoryThresholdCount = frm.num_mask_create.Text.ToInt();
                    cam.maskManager.MaskRemoveMins = frm.num_mask_remove.Text.ToInt();
                    cam.maskManager.MaskRemoveThreshold = frm.numMaskThreshold.Text.ToInt();
                    cam.maskManager.PercentMatch = frm.num_percent_var.Text.ToDouble();
                    cam.maskManager.MaxMaskUnusedDays = frm.num_max_unused.Text.ToInt();

                    this.cb_masking_enabled.Checked = frm.cb_enabled.Checked;
                    cam.maskManager.MaskingEnabled = this.cb_masking_enabled.Checked;
                    //cam.maskManager.Objects = frm.tb_objects.Text.Trim();

                    AppSettings.SaveAsync(true);
                }
            }
        }

        private void btnDetails_Click(object sender, EventArgs e)
        {
            if (this.FOLV_Cameras.SelectedObjects.Count == 0)
            {
                MessageBox.Show("Select a camera first");
                return;
            }
            this.ShowMaskDetailsDialog(((Camera)this.FOLV_Cameras.SelectedObjects[0]).Name);
        }

        private void ShowMaskDetailsDialog(string cameraname)
        {
            using (Frm_DynamicMaskDetails frm = new Frm_DynamicMaskDetails())
            {

                Camera CurCam = GetCamera(cameraname);
                frm.cam = CurCam;

                frm.ShowDialog();

                this.cb_masking_enabled.Checked = CurCam.maskManager.MaskingEnabled;

            }

        }





        private void btnCustomMask_Click(object sender, EventArgs e)
        {
            if (this.FOLV_Cameras.SelectedObjects.Count == 0)
            {
                MessageBox.Show("Select a camera first");
                return;
            }
            this.ShowEditImageMaskDialog(((Camera)this.FOLV_Cameras.SelectedObjects[0]).Name);
        }

        private void ShowEditImageMaskDialog(string cameraname)
        {
            using (Frm_CustomMasking frm = new Frm_CustomMasking())
            {
                Camera cam = AITOOL.GetCamera(cameraname);
                frm.Cam = cam;

                if (frm.ShowDialog() == DialogResult.OK)
                {
                    cam.mask_brush_size = frm.BrushSize;
                }
            }

        }

        //private void btnActions_Click(object sender, EventArgs e)
        //{
        //    using (Frm_LegacyActions frm = new Frm_LegacyActions())
        //    {


        //        Camera cam = AITOOL.GetCamera(list2.SelectedItems[0].Text);
        //        frm.cam = cam;

        //        frm.tbTriggerUrl.Text = string.Join("\r\n", Global.Split(cam.trigger_urls_as_string, "\r\n|;,"));
        //        frm.tbCancelUrl.Text = string.Join("\r\n", Global.Split(cam.cancel_urls_as_string, "\r\n|;,"));
        //        frm.tb_cooldown.Text = cam.cooldown_time.ToString(); //load cooldown time
        //        //load telegram image sending on/off option
        //        frm.cb_telegram.Checked = cam.telegram_enabled;

        //        frm.cb_copyAlertImages.Checked = cam.Action_image_copy_enabled;
        //        frm.tb_network_folder_filename.Text = cam.Action_network_folder_filename;
        //        frm.tb_network_folder.Text = cam.Action_network_folder;
        //        frm.cb_RunProgram.Checked = cam.Action_RunProgram;

        //        if (frm.ShowDialog() == DialogResult.OK)
        //        {
        //            cam.trigger_urls_as_string = string.Join(",", Global.Split(frm.tbTriggerUrl.Text.Trim(), "\r\n|;,"));
        //            cam.cancel_urls_as_string = string.Join(",", Global.Split(frm.tbCancelUrl.Text.Trim(), "\r\n|;,"));
        //            cam.cancel_urls = Global.Split(cam.cancel_urls_as_string, "\r\n|;,").ToArray();
        //            cam.cooldown_time = Convert.ToDouble(frm.tb_cooldown.Text.Trim());
        //            cam.telegram_enabled = frm.cb_telegram.Checked;
        //            cam.Action_image_copy_enabled = frm.cb_copyAlertImages.Checked;
        //            cam.Action_network_folder = frm.tb_network_folder.Text.Trim();
        //            cam.Action_network_folder_filename = frm.tb_network_folder_filename.Text;
        //            cam.Action_RunProgram = frm.cb_RunProgram.Checked;
        //            cam.Action_RunProgramString = frm.tb_RunExternalProgram.Text;

        //            AppSettings.Save();

        //        }
        //    }
        //}

        private void btnActions_Click_1(object sender, EventArgs e)
        {
            using var Trace = new Trace();  //This c# 8.0 using feature will auto dispose when the function is done.

            if (this.FOLV_Cameras.SelectedObjects.Count == 0)
            {
                MessageBox.Show("Select a camera first");
                return;
            }

            using (Frm_LegacyActions frm = new Frm_LegacyActions())
            {

                Camera cam = AITOOL.GetCamera(((Camera)this.FOLV_Cameras.SelectedObjects[0]).Name);

                frm.cam = cam;

                frm.tb_DetectionFormat.Text = cam.DetectionDisplayFormat;
                frm.lbl_DetectionFormat.Text = AITOOL.ReplaceParams(cam, null, null, frm.tb_DetectionFormat.Text, Global.IPType.Path);

                frm.tb_ConfidenceFormat.Text = AppSettings.Settings.DisplayPercentageFormat;
                frm.lbl_Confidence.Text = string.Format(frm.tb_ConfidenceFormat.Text, 99.123);


                if (cam.cancel_urls_as_string.IsEmpty())
                    cam.Action_CancelURL_Enabled = false;
                if (cam.trigger_urls_as_string.IsEmpty())
                    cam.Action_TriggerURL_Enabled = false;

                frm.cb_UrlTriggerEnabled.Checked = cam.Action_TriggerURL_Enabled;
                frm.cb_UrlCancelEnabled.Checked = cam.Action_CancelURL_Enabled;

                frm.tbTriggerUrl.Text = cam.trigger_urls_as_string.SplitStr("\r\n|").JoinStr("\r\n");
                frm.tbCancelUrl.Text = cam.cancel_urls_as_string.SplitStr("\r\n|").JoinStr("\r\n");

                frm.tb_ActionDelayMS.Text = AppSettings.Settings.ActionDelayMS.ToString();
                frm.tb_cooldown.Text = cam.cooldown_time_seconds.ToString(); //load cooldown time
                frm.tb_sound_cooldown.Text = cam.sound_cooldown_time_seconds.ToString(); //load cooldown time

                frm.tb_NetworkFolderCleanupDays.Text = cam.Action_network_folder_purge_older_than_days.ToString();

                //load telegram image sending on/off option
                frm.cb_telegram.Checked = cam.telegram_enabled;
                frm.tb_telegram_caption.Text = cam.telegram_caption;
                //frm.tb_telegram_triggering_objects.Text = cam.telegram_triggering_objects;

                frm.cb_telegram_active_time.Text = cam.telegram_active_time_range;

                frm.cb_copyAlertImages.Checked = cam.Action_image_copy_enabled;
                frm.tb_network_folder_filename.Text = cam.Action_network_folder_filename;
                frm.tb_network_folder.Text = cam.Action_network_folder;

                frm.cb_RunProgram.Checked = cam.Action_RunProgram;
                frm.tb_RunExternalProgram.Text = cam.Action_RunProgramString;
                frm.tb_RunExternalProgramArgs.Text = cam.Action_RunProgramArgsString;

                frm.cb_PlaySound.Checked = cam.Action_PlaySounds;
                frm.tb_Sounds.Text = cam.Action_Sounds;

                frm.cb_MQTT_enabled.Checked = cam.Action_mqtt_enabled;
                frm.tb_MQTT_Payload.Text = cam.Action_mqtt_payload;
                frm.tb_MQTT_Topic.Text = cam.Action_mqtt_topic;
                frm.tb_MQTT_Payload_cancel.Text = cam.Action_mqtt_payload_cancel;
                frm.tb_MQTT_Topic_Cancel.Text = cam.Action_mqtt_topic_cancel;
                frm.cb_MQTT_SendImage.Checked = cam.Action_mqtt_send_image;

                frm.cb_Webhook_enabled.Checked = cam.Action_webhook_enabled;
                frm.tb_Webhook_Url.Text = cam.Action_webhook_url;
                frm.tb_Webhook_Method.Text = cam.Action_webhook_method;
                frm.tb_Webhook_ContentType.Text = cam.Action_webhook_content_type;
                frm.tb_Webhook_Headers.Text = cam.Action_webhook_headers;
                frm.tb_Webhook_Body.Text = cam.Action_webhook_body;
                frm.cb_Webhook_SendImage.Checked = cam.Action_webhook_send_image;
                frm.tb_Webhook_CancelUrl.Text = cam.Action_webhook_cancel_url;
                frm.tb_Webhook_CancelBody.Text = cam.Action_webhook_cancel_body;

                frm.cb_Pushover_Enabled.Checked = cam.Action_pushover_enabled;
                frm.tb_Pushover_Title.Text = cam.Action_pushover_title;
                frm.tb_Pushover_Message.Text = cam.Action_pushover_message;
                frm.tb_Pushover_Device.Text = cam.Action_pushover_device;
                //frm.tb_pushover_triggering_objects.Text = cam.Action_pushover_triggering_objects;
                frm.tb_Pushover_Priority.Text = cam.Action_pushover_Priority;
                frm.tb_Pushover_sound.Text = cam.Action_pushover_Sound;
                frm.cb_pushover_active_time.Text = cam.Action_pushover_active_time_range;

                frm.cb_queue_actions.Checked = cam.Action_queued;

                frm.cb_ActivateBlueIrisWindow.Checked = cam.Action_ActivateBlueIrisWindow;

                frm.cb_mergeannotations.Checked = cam.Action_image_merge_detections;

                frm.tb_jpeg_merge_quality.Text = cam.Action_image_merge_jpegquality.ToString();

                Global_GUI.GroupboxEnableDisable(frm.groupBoxPushover, frm.cb_Pushover_Enabled);
                Global_GUI.GroupboxEnableDisable(frm.groupBoxTelegram, frm.cb_telegram);
                Global_GUI.GroupboxEnableDisable(frm.groupBoxMQTT, frm.cb_MQTT_enabled);
                Global_GUI.GroupboxEnableDisable(frm.groupBoxWebhook, frm.cb_Webhook_enabled);
                Global_GUI.GroupboxEnableDisable(frm.groupBoxUrlTrigger, frm.cb_UrlTriggerEnabled);
                Global_GUI.GroupboxEnableDisable(frm.groupBoxUrlCancel, frm.cb_UrlCancelEnabled);

                frm.tb_Sounds.Enabled = frm.cb_PlaySound.Checked;
                frm.tb_RunExternalProgram.Enabled = frm.cb_RunProgram.Checked;
                frm.tb_RunExternalProgramArgs.Enabled = frm.cb_RunProgram.Checked;
                frm.tb_network_folder.Enabled = frm.cb_copyAlertImages.Checked;
                frm.tb_network_folder_filename.Enabled = frm.cb_copyAlertImages.Checked;

                frm.tb_ActionCancelSecs.Text = AppSettings.Settings.ActionCancelSeconds.ToString();

                frm.cb_ShowOnlyRelevant.Checked = AppSettings.Settings.HistoryOnlyDisplayRelevantObjects;

                if (frm.cb_mergeannotations.Checked)
                    frm.tb_jpeg_merge_quality.Enabled = true;
                else
                    frm.tb_jpeg_merge_quality.Enabled = false;

                if (frm.ShowDialog() == DialogResult.OK)
                {
                    cam.DetectionDisplayFormat = frm.tb_DetectionFormat.Text.Trim();
                    AppSettings.Settings.DisplayPercentageFormat = frm.tb_ConfidenceFormat.Text.Trim();

                    //clean up the trigger lists by splitting and re-joining 
                    cam.trigger_urls_as_string = frm.tbTriggerUrl.Text.Trim().SplitStr("\r\n|").JoinStr("|");
                    cam.trigger_urls = cam.trigger_urls_as_string.SplitStr("\r\n|").ToArray();

                    cam.cancel_urls_as_string = frm.tbCancelUrl.Text.Trim().SplitStr("\r\n|").JoinStr("|");
                    cam.cancel_urls = cam.cancel_urls_as_string.SplitStr("\r\n|").ToArray();

                    cam.Action_TriggerURL_Enabled = frm.cb_UrlTriggerEnabled.Checked;
                    cam.Action_CancelURL_Enabled = frm.cb_UrlCancelEnabled.Checked;

                    if (cam.cancel_urls.Count() == 0)
                        cam.Action_CancelURL_Enabled = false;
                    if (cam.trigger_urls.Count() == 0)
                        cam.Action_TriggerURL_Enabled = false;

                    AppSettings.Settings.ActionDelayMS = GetNumberInt(frm.tb_ActionDelayMS.Text.Trim());
                    cam.cooldown_time_seconds = GetNumberInt(frm.tb_cooldown.Text.Trim());
                    cam.sound_cooldown_time_seconds = GetNumberInt(frm.tb_sound_cooldown.Text.Trim());
                    cam.telegram_enabled = frm.cb_telegram.Checked;
                    cam.telegram_caption = frm.tb_telegram_caption.Text.Trim();
                    //cam.telegram_triggering_objects = frm.tb_telegram_triggering_objects.Text.Trim();

                    cam.Action_network_folder_purge_older_than_days = GetNumberInt(frm.tb_NetworkFolderCleanupDays.Text.Trim());

                    cam.telegram_active_time_range = frm.cb_telegram_active_time.Text.Trim();

                    cam.Action_image_copy_enabled = frm.cb_copyAlertImages.Checked;
                    cam.Action_network_folder = frm.tb_network_folder.Text.Trim();
                    cam.Action_network_folder_filename = frm.tb_network_folder_filename.Text;

                    cam.Action_RunProgram = frm.cb_RunProgram.Checked;
                    cam.Action_RunProgramString = frm.tb_RunExternalProgram.Text.Trim();
                    cam.Action_RunProgramArgsString = frm.tb_RunExternalProgramArgs.Text.Trim();

                    cam.Action_PlaySounds = frm.cb_PlaySound.Checked;
                    cam.Action_Sounds = frm.tb_Sounds.Text.Trim();

                    cam.Action_mqtt_enabled = frm.cb_MQTT_enabled.Checked;
                    cam.Action_mqtt_payload = frm.tb_MQTT_Payload.Text.Trim();
                    cam.Action_mqtt_topic = frm.tb_MQTT_Topic.Text.Trim();
                    cam.Action_mqtt_payload_cancel = frm.tb_MQTT_Payload_cancel.Text.Trim();
                    cam.Action_mqtt_topic_cancel = frm.tb_MQTT_Topic_Cancel.Text.Trim();
                    cam.Action_mqtt_send_image = frm.cb_MQTT_SendImage.Checked;

                    cam.Action_webhook_enabled = frm.cb_Webhook_enabled.Checked;
                    cam.Action_webhook_url = frm.tb_Webhook_Url.Text.Trim();
                    cam.Action_webhook_method = frm.tb_Webhook_Method.Text.Trim();
                    cam.Action_webhook_content_type = frm.tb_Webhook_ContentType.Text.Trim();
                    cam.Action_webhook_headers = frm.tb_Webhook_Headers.Text.Trim();
                    cam.Action_webhook_body = frm.tb_Webhook_Body.Text.Trim();
                    cam.Action_webhook_send_image = frm.cb_Webhook_SendImage.Checked;
                    cam.Action_webhook_cancel_url = frm.tb_Webhook_CancelUrl.Text.Trim();
                    cam.Action_webhook_cancel_body = frm.tb_Webhook_CancelBody.Text.Trim();

                    cam.Action_pushover_enabled = frm.cb_Pushover_Enabled.Checked;
                    cam.Action_pushover_title = frm.tb_Pushover_Title.Text.Trim();
                    cam.Action_pushover_message = frm.tb_Pushover_Message.Text.Trim();
                    cam.Action_pushover_device = frm.tb_Pushover_Device.Text.Trim();
                    //cam.Action_pushover_triggering_objects = frm.tb_pushover_triggering_objects.Text.Trim();
                    cam.Action_pushover_Sound = frm.tb_Pushover_sound.Text.Trim();
                    cam.Action_pushover_Priority = frm.tb_Pushover_Priority.Text.Trim();
                    cam.Action_pushover_active_time_range = frm.cb_pushover_active_time.Text.Trim();

                    cam.Action_image_merge_detections = frm.cb_mergeannotations.Checked;

                    cam.Action_image_merge_jpegquality = Convert.ToInt64(frm.tb_jpeg_merge_quality.Text);

                    cam.Action_queued = frm.cb_queue_actions.Checked;
                    cam.Action_ActivateBlueIrisWindow = frm.cb_ActivateBlueIrisWindow.Checked;

                    AppSettings.Settings.ActionCancelSeconds = GetNumberInt(frm.tb_ActionCancelSecs.Text);
                    AppSettings.Settings.HistoryOnlyDisplayRelevantObjects = frm.cb_ShowOnlyRelevant.Checked;


                    cam.UpdateCamera();

                    UpdateActionsLabel(cam);

                    AppSettings.SaveAsync(true);

                }
            }
        }

        private void UpdateActionsLabel(Camera cam)
        {
            Lbl_Actions.Text = "";
            if (cam.Action_TriggerURL_Enabled)
                Lbl_Actions.Text += "TriggerURL";
            if (cam.Action_CancelURL_Enabled)
                Lbl_Actions.Text += ", CancelURL";
            if (cam.telegram_enabled)
                Lbl_Actions.Text += ", Telegram";
            if (cam.Action_pushover_enabled)
                Lbl_Actions.Text += ", Pushover";
            if (cam.Action_mqtt_enabled)
                Lbl_Actions.Text += ", MQTT";
            if (cam.Action_webhook_enabled)
                Lbl_Actions.Text += ", Webhook";
            if (cam.Action_RunProgram)
                Lbl_Actions.Text += ", Run";
            if (cam.Action_PlaySounds)
                Lbl_Actions.Text += ", Sound";
            if (cam.Action_image_copy_enabled)
                Lbl_Actions.Text += ", Copy";

            if (Lbl_Actions.Text.IsEmpty())
                Lbl_Actions.Text = "No Actions enabled.";
            else
                Lbl_Actions.Text = Lbl_Actions.Text.Trim(", ".ToCharArray());

        }

        private void btn_enabletelegram_Click(object sender, EventArgs e)
        {
            AppSettings.Settings.send_telegram_errors = cb_send_telegram_errors.Checked;
            AppSettings.Settings.send_pushover_errors = cb_send_pushover_errors.Checked;

            foreach (Camera cam in AppSettings.Settings.CameraList)
            {
                if (AppSettings.Settings.send_telegram_errors && !cam.telegram_enabled)
                {
                    cam.telegram_enabled = true;
                    Log($"Enabled Telegram on camera '{cam.Name}'.");
                }
                if (AppSettings.Settings.send_pushover_errors && !cam.Action_pushover_enabled)
                {
                    cam.Action_pushover_enabled = true;
                    Log($"Enabled Pushover on camera '{cam.Name}'.");
                }
            }
            AppSettings.SaveAsync(true);
        }

        private void btn_disabletelegram_Click(object sender, EventArgs e)
        {
            AppSettings.Settings.send_telegram_errors = cb_send_telegram_errors.Checked;
            AppSettings.Settings.send_pushover_errors = cb_send_pushover_errors.Checked;

            foreach (Camera cam in AppSettings.Settings.CameraList)
            {
                if (AppSettings.Settings.send_telegram_errors && cam.telegram_enabled)
                {
                    cam.telegram_enabled = false;
                    Log($"Disabled Telegram on camera '{cam.Name}'.");
                }
                if (AppSettings.Settings.send_pushover_errors && cam.Action_pushover_enabled)
                {
                    cam.Action_pushover_enabled = false;
                    Log($"Disabled Pushover on camera '{cam.Name}'.");
                }
            }
            AppSettings.SaveAsync(true);
        }

        private void toolStripButtonEditURL_Click(object sender, EventArgs e)
        {
            using (Frm_AIServerDeepstackEdit frm = new Frm_AIServerDeepstackEdit())
            {
                string srv = ((History)this.folv_history.SelectedObjects[0]).AIServer;
                ClsURLItem url = AITOOL.GetURL(srv, false, false);
                if (url != null)
                {
                    frm.CurURL = url;
                    frm.ShowDialog();
                }
            }
        }
    }
}
