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
        async Task UpdateHistoryAddedRemoved()
        {
            using var Trace = new Trace();  //This c# 8.0 using feature will auto dispose when the function is done.
            //Log("===Enter");
            //this should be a quicker full list update
            if (AppSettings.Settings.HistoryAutoRefresh &&
                !this.IsHistoryListUpdating &&
                this.tabControl1.SelectedIndex == 2 &&
                this.Visible &&
                !(this.WindowState == FormWindowState.Minimized) &&
                (DateTime.Now - this.LastListUpdate).TotalMilliseconds >= AppSettings.Settings.TimeBetweenListRefreshsMS &&
                this.DatabaseInitialized &&
                !IsLoading &&
                await HistoryDB.HasUpdates())
            {
                // run in another thread so gui doesnt freeze
                await Task.Run(() =>
                {

                    this.IsHistoryListUpdating = true;

                    Global_GUI.InvokeIFRequired(this.folv_history, () =>
                    {

                        //Log($"Debug:  Updating list...({AddedHistoryItems.Count} added, {DeletedHistoryItems.Count} deleted)");

                        //UpdateToolstrip("Updating list...");

                        List<History> added = HistoryDB.GetRecentlyAdded();
                        List<History> removed = HistoryDB.GetRecentlyDeleted();

                        if (removed.Count > 0)
                            this.folv_history.RemoveObjects(removed);

                        if (added.Count > 0)
                        {
                            //find the last item that is unfiltered:
                            History lasthist = null;
                            for (int i = added.Count - 1; i >= 0; i--)
                            {
                                if (this.checkListFilters(added[i]))
                                {
                                    lasthist = added[i];
                                    break;
                                }
                            }

                            Global_GUI.UpdateFOLV(this.folv_history, added, AppSettings.Settings.HistoryFollow, UseSelected: true, SelectObject: lasthist);

                        }

                        if (AppSettings.Settings.HistoryFollow && this.folv_history.SelectedObject == null && this.folv_history.GetItemCount() > 0)
                        {
                            if (this.folv_history.IsFiltering)
                            {
                                //use the last filtered object as the selected object
                                this.folv_history.SelectedObject = this.folv_history.FilteredObjects.Cast<object>().Last();
                            }
                            else if (!this.folv_history.IsFiltering)
                            {
                                this.folv_history.SelectedObject = this.folv_history.Objects.Cast<object>().Last();
                            }

                            if (this.folv_history.SelectedObject != null)
                                this.folv_history.EnsureModelVisible(this.folv_history.SelectedObject);

                        }

                        this.LastListUpdate = DateTime.Now;

                        this.IsHistoryListUpdating = false;

                        //UpdateToolstrip("");

                    });

                });

            }
            else
            {
                //Log($"Debug: List not updated - Refresh={AppSettings.Settings.HistoryAutoRefresh}, Visible={tabControl1.SelectedIndex == 2 && this.Visible && !(this.WindowState == FormWindowState.Minimized)}, IsListUpdating={this.IsListUpdating}, LastListUpdateMS={(DateTime.Now - this.LastListUpdate).TotalMilliseconds}");
            }
            //Log("===Exit");


        }

        private void HistoryStartStop()
        {
            if (AppSettings.Settings.HistoryAutoRefresh)
            {
                this.HistoryUpdateListTimer.Enabled = true;
                this.HistoryUpdateListTimer.Start();
                Log("Debug: History update timer started.");
            }
            else
            {
                this.HistoryUpdateListTimer.Enabled = false;
                this.HistoryUpdateListTimer.Stop();
                Log("Debug: History update timer stopped.");
            }
        }
        //load stored entries in history CSV into history ListView
        private async Task LoadHistoryAsync(bool FilterChanged, bool Follow)
        {

            using var Trace = new Trace();  //This c# 8.0 using feature will auto dispose when the function is done.


            //if (IsLoading)  //when you set checkboxes during init, it may trigger the event to load the history
            //{
            //    //Log("---Exit (still loading)");
            //    return;
            //}

            //make sure only one thread updating at a time
            //await Semaphore_List_Updating.WaitAsync();

            if (this.IsHistoryListUpdating)
            {
                Log("Trace: ---Exit (already updating)");
                return;
            }

            Stopwatch semsw = Stopwatch.StartNew();

            this.IsHistoryListUpdating = true;
            this.LastListUpdate = DateTime.Now;

            Global_GUI.CursorWait cw = null;

            try
            {
                if (semsw.ElapsedMilliseconds >= AppSettings.Settings.loop_delay_ms)
                    Log($"debug: Waited {semsw.ElapsedMilliseconds}ms while waiting for other threads to finish.");

                //wait a bit for the list to be available
                Stopwatch sw = Stopwatch.StartNew();
                bool displayed = false;
                do
                {
                    if (HistoryDB != null && HistoryDB.HistoryDic != null && this.folv_history != null && this.DatabaseInitialized)
                        break;
                    else if (!displayed)
                    {

                        Log("debug: Waiting for database to finish initializing...");
                        displayed = true;
                    }
                    await Task.Delay(AppSettings.Settings.loop_delay_ms);

                } while (sw.ElapsedMilliseconds < 60000);

                if (displayed)
                    Log($"...debug: Waited {sw.ElapsedMilliseconds}ms for the database to finish initializing/cleaning.");

                //Dont update list unless we are on the tab and it is visible for performance reasons.
                if (FilterChanged || this.folv_history.Items.Count == 0 || (this.tabControl1.SelectedIndex == 2 && this.Visible && !(this.WindowState == FormWindowState.Minimized)))
                {


                    if (this.Visible && !(this.WindowState == FormWindowState.Minimized))
                        this.UpdateStats("Updating History List...");

                    if (FilterChanged)
                        cw = new Global_GUI.CursorWait();

                    if (await HistoryDB.HasUpdates() || FilterChanged)
                    {

                        Global_GUI.InvokeIFRequired(this.folv_history, async () =>
                        {

                            List<History> histlist = HistoryDB.GetAllValues();
                            //find the last item that is unfiltered:
                            History lasthist = null;
                            for (int i = histlist.Count - 1; i >= 0; i--)
                            {
                                if (this.checkListFilters(histlist[i]))
                                {
                                    lasthist = histlist[i];
                                    break;
                                }
                            }
                            // run in another thread so gui doesnt freeze
                            await Task.Run(() =>
                            {
                                Global_GUI.UpdateFOLV(this.folv_history, histlist, Follow || AppSettings.Settings.HistoryFollow, UseSelected: true, SelectObject: lasthist, FullRefresh: true);
                            });

                            //reset any that snuck in while waiting since we just did a full list update
                            HistoryDB.GetRecentlyAdded();
                            HistoryDB.GetRecentlyDeleted();

                            if (FilterChanged)
                            {

                                if (this.comboBox_filter_camera.Text != "All Cameras" || this.cb_filter_animal.Checked || this.cb_filter_nosuccess.Checked || this.cb_filter_person.Checked || this.cb_filter_success.Checked || this.cb_filter_vehicle.Checked || this.cb_filter_skipped.Checked)
                                {
                                    //filter
                                    this.folv_history.ModelFilter = new BrightIdeasSoftware.ModelFilter((object x) =>
                                {
                                    History hist = (History)x;
                                    return this.checkListFilters(hist);
                                });
                                }
                                else
                                {
                                    this.folv_history.ModelFilter = null;
                                }


                            }

                            if (Follow && this.folv_history.SelectedObject == null && this.folv_history.GetItemCount() > 0)
                            {
                                if (this.folv_history.IsFiltering)
                                {
                                    //use the last filtered object as the selected object
                                    this.folv_history.SelectedObject = this.folv_history.FilteredObjects.Cast<object>().Last();
                                }
                                else if (!this.folv_history.IsFiltering)
                                {
                                    this.folv_history.SelectedObject = this.folv_history.Objects.Cast<object>().Last();
                                }

                                if (this.folv_history.SelectedObject != null)
                                    this.folv_history.EnsureModelVisible(this.folv_history.SelectedObject);

                            }

                        });
                    }
                    else
                    {
                        //Log("debug: No history file updates.");
                    }

                    if (this.Visible && !(this.WindowState == FormWindowState.Minimized))
                        this.UpdateStats("Idle.");

                }
                else
                {
                    //Log("debug: Not updating history, window not visible or history tab not selected.");
                }

            }
            catch (Exception ex)
            {
                Log("Error: " + ex.Msg());
            }
            finally
            {
                if (cw != null)
                    cw.Dispose();
                this.LastListUpdate = DateTime.Now;
                this.IsHistoryListUpdating = false;
                //Semaphore_List_Updating.Release();
                //Log("---Exit");

            }

        }

        //check if a filter applies on given string of history list entry 
        private bool checkListFilters(History hist)   //string cameraname, string success, string objects_and_confidence
        {

            bool ret = true;

            if (!hist.Success && this.cb_filter_success.Checked)
                ret = false;

            if (hist.Success && this.cb_filter_nosuccess.Checked)
                ret = false;

            if (!hist.WasSkipped && this.cb_filter_skipped.Checked)
                ret = false;

            if (!hist.WasMasked && this.cb_filter_masked.Checked)
                ret = false;

            if (!hist.IsPerson && this.cb_filter_person.Checked)
                ret = false;

            if (!hist.IsVehicle && this.cb_filter_vehicle.Checked)
                ret = false;

            if (!hist.IsAnimal && this.cb_filter_animal.Checked)
                ret = false;

            bool CameraValid = ((string.Equals(this.comboBox_filter_camera.Text.Trim(), "All Cameras", StringComparison.OrdinalIgnoreCase)) || string.Equals(hist.Camera.Trim(), this.comboBox_filter_camera.Text.Trim(), StringComparison.OrdinalIgnoreCase));

            if (!CameraValid)
                ret = false;

            return ret;


        }



        //EVENTS

        private void BeginProcessImage(string image_path)
        {

            string filename = Path.GetFileName(image_path);

            Global_GUI.InvokeIFRequired(this.label2, () => { this.label2.Text = $"Processing {filename}..."; });

            this.UpdateStats();

        }

        private void EndProcessImage(string image_path)
        {

            string filename = Path.GetFileName(image_path);


            //output Running on Overview Tab
            Global_GUI.InvokeIFRequired(this.label2, () => { this.label2.Text = $"Idle"; });

            //only update charts if stats tab is open

            if (this.tabControl1.SelectedIndex == 1)
            {
                Global_GUI.InvokeIFRequired(this, () => { this.UpdatePieChart(); this.UpdateTimeline(); this.UpdateConfidenceChart(); });
            }

            //load updated cameara stats info in camera tab if a camera is selected
            Global_GUI.InvokeIFRequired(this, () =>
            {
                if (this.FOLV_Cameras.SelectedObjects != null && this.FOLV_Cameras.SelectedObjects.Count > 0)
                {


                    //load only stats from Camera.cs object
                    //all camera objects are stored in the list CameraList, so firstly the position (stored in the second column for each entry) is gathered
                    Camera cam = AITOOL.GetCamera(((Camera)this.FOLV_Cameras.SelectedObjects[0]).Name);

                    //load cameras stats
                    string stats = $"Alerts: {cam.stats_alerts.ToString()} | Irrelevant Alerts: {cam.stats_irrelevant_alerts.ToString()} | False Alerts: {cam.stats_false_alerts.ToString()}";
                    if (cam.maskManager.MaskingEnabled)
                    {
                        stats += $" | Mask History Count: {cam.maskManager.LastPositionsHistory.Count} | Current Dynamic Masks: {cam.maskManager.MaskedPositions.Count}";
                    }
                    this.lbl_camstats.Text = stats;
                }

            });


            this.UpdateStats();

        }


        //event: load selected image to picturebox


        //event: show mask button clicked
        private void cb_showMask_CheckedChanged(object sender, EventArgs e)
        {
            if (this.folv_history.SelectedObjects != null && this.folv_history.SelectedObjects.Count > 0)
            {
                this.showHideMask();
            }
        }

        //event: show objects button clicked
        private void cb_showObjects_MouseUp(object sender, MouseEventArgs e)
        {
            if (this.folv_history.SelectedObjects != null && this.folv_history.SelectedObjects.Count > 0)
            {
                this.pictureBox1.Refresh();
            }

        }

        private void folv_history_SelectionChanged(object sender, EventArgs e)
        {

            if (IsClosing)
                return;

            string filename = "";

            try
            {
                if (this.folv_history.SelectedObjects != null && this.folv_history.SelectedObjects.Count > 0)
                {
                    History hist = (History)this.folv_history.SelectedObjects[0];

                    if (hist == null)
                        return;

                    filename = hist.Filename;

                    if (!filename.IsEmpty() && filename.Contains("\\") && File.Exists(filename))
                    {
                        this.lbl_objects.Text = $"Loading Image {filename}...";

                        using ClsImageQueueItem imgq = new ClsImageQueueItem(filename, 0);
                        if (imgq.IsValid())
                        {
                            this.pictureBox1.BackgroundImage = imgq.ToImage(); //load actual image as background, so that an overlay can be added as the image
                        }
                        this.showHideMask();
                        this.lbl_objects.Text = hist.Detections;
                    }
                    else
                    {
                        Log("Removing missing file from database: " + filename);
                        HistoryDB.DeleteHistoryQueue(filename);
                        this.lbl_objects.Text = "Image not found";
                        this.pictureBox1.BackgroundImage = null;
                    }

                    if (!string.IsNullOrEmpty(hist.PredictionsJSON))
                    {
                        this.toolStripButtonDetails.Enabled = true;
                    }
                    else
                    {
                        this.toolStripButtonDetails.Enabled = false;
                    }

                    this.toolStripButtonEditImageMask.Enabled = true;
                    this.toolStripButtonMaskDetails.Enabled = true;
                    this.toolStripButtonEditURL.Enabled = true;

                }
                else
                {
                    this.lbl_objects.Text = "No selection";
                    this.pictureBox1.BackgroundImage = null;
                    this.toolStripButtonDetails.Enabled = false;
                    this.toolStripButtonEditImageMask.Enabled = false;
                    this.toolStripButtonMaskDetails.Enabled = false;
                    this.toolStripButtonEditURL.Enabled = false;
                }

            }
            catch (Exception ex)
            {
                Log($"Error: {ex.Msg()} - Hist.Filename={filename}");

            }



        }

        private void folv_history_FormatRow(object sender, BrightIdeasSoftware.FormatRowEventArgs e)
        {
            this.FormatHistoryRow(sender, e);
        }

        private void FormatHistoryRow(object Sender, BrightIdeasSoftware.FormatRowEventArgs e)
        {
            try
            {
                History hist = (History)e.Model;

                // If SPI IsNot Nothing Then
                if (hist.Success && !hist.HasError)
                    e.Item.ForeColor = Color.Green;
                else if (hist.HasError)
                {
                    e.Item.ForeColor = Color.Black;
                    e.Item.BackColor = Color.Red;
                }
                else if (hist.WasSkipped)
                    e.Item.ForeColor = Color.Red;
                else if (hist.WasMasked)
                {
                    e.Item.ForeColor = Color.Black;
                    e.Item.BackColor = Color.LightGray;
                }
                else if (!hist.Success && hist.Detections.IndexOf("false alert", StringComparison.OrdinalIgnoreCase) >= 0)
                    e.Item.ForeColor = Color.Gray;
                else
                    e.Item.ForeColor = Color.Black;
            }


            catch (Exception)
            {
            }
            // Log("Error: " & ex.Msg())
            finally
            {
            }
        }

        private void FormatCameraRow(object Sender, BrightIdeasSoftware.FormatRowEventArgs e)
        {
            try
            {
                Camera cam = (Camera)e.Model;


                if (!cam.enabled)
                    e.Item.ForeColor = Color.Gray;
                else if (cam.Name.Equals("default", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(cam.Prefix))
                    e.Item.ForeColor = Color.Brown;
                else
                    e.Item.ForeColor = Color.Black;
            }


            catch (Exception)
            {
            }
            // Log("Error: " & ex.Msg())
            finally
            {
            }
        }
        private void btn_resetstats_Click(object sender, EventArgs e)
        {

            if (string.Equals(this.comboBox1.Text.Trim(), "All Cameras", StringComparison.OrdinalIgnoreCase))
            {
                foreach (Camera cam in AppSettings.Settings.CameraList)
                {
                    cam.stats_alerts = 0;
                    cam.stats_irrelevant_alerts = 0;
                    cam.stats_false_alerts = 0;
                    cam.stats_skipped_images = 0;
                    cam.stats_skipped_images_session = 0;
                }
            }
            else
            {

                Camera cam = AITOOL.GetCamera(this.comboBox1.Text);  //int i = AppSettings.Settings.CameraList.FindIndex(x => x.name.ToLower().Trim() == comboBox1.Text.ToLower().Trim());
                if (cam != null)
                {
                    cam.stats_alerts = 0;
                    cam.stats_irrelevant_alerts = 0;
                    cam.stats_false_alerts = 0;
                    cam.stats_skipped_images = 0;
                    cam.stats_skipped_images_session = 0;
                }
            }

            fcalc.Clear();
            icalc.Clear();
            lcalc.Clear();
            qcalc.Clear();
            scalc.Clear();
            tcalc.Clear();

            LogMan.ErrorCount = 0;

            AppSettings.SaveAsync(true);

            this.UpdatePieChart(); this.UpdateTimeline(); this.UpdateConfidenceChart();

            this.UpdateStats();
        }

        private async void cb_filter_skipped_CheckedChanged(object sender, EventArgs e)
        {
            await this.LoadHistoryAsync(true, AppSettings.Settings.HistoryFollow).ConfigureAwait(false);
        }

        private async void HistoryUpdateListTimer_Tick(object sender, EventArgs e)
        {
            if (IsClosing)
                return;

            if (!AppSettings.AlreadyRunning)
                Global.SaveRegSetting("LastShutdownState", $"checkpoint: HistoryUpdateTimer: {DateTime.Now}");

            await this.UpdateHistoryAddedRemoved();

            this.UpdateStats();
        }

        private void folv_history_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void toolStripStatusErrors_Click(object sender, EventArgs e)
        {
            this.ShowErrors();
            LogMan.ErrorCount = 0;
        }

        private async void cb_follow_CheckedChanged(object sender, EventArgs e)
        {
            await this.LoadHistoryAsync(true, AppSettings.Settings.HistoryFollow).ConfigureAwait(false);
        }

        private async void comboBox_filter_camera_SelectionChangeCommitted(object sender, EventArgs e)
        {
            await this.LoadHistoryAsync(true, AppSettings.Settings.HistoryFollow).ConfigureAwait(false);
        }

        private void comboBox1_SelectionChangeCommitted(object sender, EventArgs e)
        {
            if (this.tabControl1.SelectedIndex == 1)
            {

                this.UpdatePieChart(); this.UpdateTimeline(); this.UpdateConfidenceChart();
            }
        }

        private void SaveFilters()
        {
            AppSettings.Settings.HistoryFilterAnimals = cb_filter_animal.Checked;
            AppSettings.Settings.HistoryFilterMasked = cb_filter_masked.Checked;
            AppSettings.Settings.HistoryFilterNoSuccess = cb_filter_nosuccess.Checked;
            AppSettings.Settings.HistoryFilterPeople = cb_filter_person.Checked;
            AppSettings.Settings.HistoryFilterSkipped = cb_filter_skipped.Checked;
            AppSettings.Settings.HistoryFilterRelevant = cb_filter_success.Checked;
            AppSettings.Settings.HistoryFilterVehicles = cb_filter_vehicle.Checked;

            AppSettings.SaveAsync(true);
        }

        private async void cb_filter_success_Click(object sender, EventArgs e)
        {
            SaveFilters();
            await this.LoadHistoryAsync(true, AppSettings.Settings.HistoryFollow).ConfigureAwait(false);
        }

        private async void cb_filter_nosuccess_Click(object sender, EventArgs e)
        {
            SaveFilters();
            await this.LoadHistoryAsync(true, AppSettings.Settings.HistoryFollow).ConfigureAwait(false);
            AppSettings.SaveAsync(true);
        }

        private async void cb_filter_person_Click(object sender, EventArgs e)
        {
            SaveFilters();
            await this.LoadHistoryAsync(true, AppSettings.Settings.HistoryFollow).ConfigureAwait(false);
            AppSettings.SaveAsync(true);
        }

        private async void cb_filter_animal_Click(object sender, EventArgs e)
        {
            SaveFilters();
            await this.LoadHistoryAsync(true, AppSettings.Settings.HistoryFollow).ConfigureAwait(false);
        }

        private async void cb_filter_vehicle_Click(object sender, EventArgs e)
        {
            SaveFilters();
            await this.LoadHistoryAsync(true, AppSettings.Settings.HistoryFollow).ConfigureAwait(false);
        }

        private async void cb_filter_skipped_Click(object sender, EventArgs e)
        {
            SaveFilters();
            await this.LoadHistoryAsync(true, AppSettings.Settings.HistoryFollow).ConfigureAwait(false);
        }

        private async void cb_filter_masked_Click(object sender, EventArgs e)
        {
            SaveFilters();
            await this.LoadHistoryAsync(true, AppSettings.Settings.HistoryFollow).ConfigureAwait(false);
        }

        private async void comboBox_filter_camera_DropDownClosed(object sender, EventArgs e)
        {
            SaveFilters();
            await this.LoadHistoryAsync(true, AppSettings.Settings.HistoryFollow).ConfigureAwait(false);
        }

        private void cb_showMask_Click(object sender, EventArgs e)
        {
            AppSettings.Settings.HistoryShowMask = this.cb_showMask.Checked;
            AppSettings.SaveAsync(true);
            this.showHideMask();
        }

        private void cb_showObjects_CheckedChanged(object sender, EventArgs e)
        {
            if (this.folv_history.SelectedObjects != null && this.folv_history.SelectedObjects.Count > 0)
            {
                this.pictureBox1.Refresh();
            }
        }

        private void automaticallyRefreshToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AppSettings.Settings.HistoryAutoRefresh = this.automaticallyRefreshToolStripMenuItem.Checked;
            AppSettings.SaveAsync(true);
            this.HistoryStartStop();

        }

        private void cb_showObjects_Click(object sender, EventArgs e)
        {
            AppSettings.Settings.HistoryShowObjects = this.cb_showObjects.Checked;
            AppSettings.SaveAsync(true);
            this.pictureBox1.Refresh();
        }

        private void cb_follow_Click(object sender, EventArgs e)
        {
            AppSettings.Settings.HistoryFollow = this.cb_follow.Checked;
            AppSettings.SaveAsync(true);
        }

        private void toolStripButtonDetails_Click(object sender, EventArgs e)
        {
            this.ViewPredictionDetails();
        }

        private void ViewPredictionDetails()
        {
            try
            {
                List<ClsPrediction> allpredictions = new List<ClsPrediction>();
                string filename = "";
                foreach (History hist in this.folv_history.SelectedObjects)
                {
                    List<ClsPrediction> predictions = hist.Predictions();

                    if (predictions.Count > 0)
                    {
                        allpredictions.AddRange(predictions);
                        filename = hist.Filename;
                    }
                    else
                    {
                        Log($"debug: No predictions for image {hist.Filename}: Json='{hist.PredictionsJSON}'");
                    }

                }

                Frm_ObjectDetail frm = new Frm_ObjectDetail();
                frm.PredictionObjectDetailsList = allpredictions;
                frm.ImageFileName = filename;
                frm.Show();

            }
            catch (Exception ex)
            {

                Log("Error: " + ex.Msg());
            }
        }



        private void testDetectionAgainToolStripMenuItem_Click(object sender, EventArgs e)
        {


            if (this.folv_history.SelectedObjects != null && this.folv_history.SelectedObjects.Count > 0)
            {
                Log("----------------------- TESTING TRIGGERS ----------------------------");

                foreach (History hist in this.folv_history.SelectedObjects)
                {
                    if (!string.IsNullOrEmpty(hist.Filename) && File.Exists(hist.Filename))
                    {
                        //test by copying the file as a new file into the watched folder'
                        string folder = Path.GetDirectoryName(hist.Filename);
                        string filename = Path.GetFileNameWithoutExtension(hist.Filename);
                        //strip out anything after the first _AITOOLTEST_ in the filename
                        filename = filename.GetWord("", "_AITOOLTEST_");
                        string ext = Path.GetExtension(hist.Filename);
                        string testfile = Path.Combine(folder, $"{filename}_AITOOLTEST_{DateTime.Now.TimeOfDay.TotalSeconds.Round(0)}{ext}");
                        File.Copy(hist.Filename, testfile, true);
                        string str = "Created test image file based on last detected object for the camera: " + testfile;
                        Log(str);
                    }
                    else
                    {
                        Log("Error: File does not exist for testing: " + hist.Filename);

                    }


                }

                Log("---------------------- DONE TESTING TRIGGERS -------------------------");

            }

        }

        private void detailsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.ViewPredictionDetails();
        }

        private async void refreshToolStripMenuItem_Click(object sender, EventArgs e)
        {
            await this.LoadHistoryAsync(false, AppSettings.Settings.HistoryFollow).ConfigureAwait(false);
        }

        private void folv_history_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            this.ViewPredictionDetails();
        }

        private void toolStripButtonMaskDetails_Click(object sender, EventArgs e)
        {

            if (this.folv_history.SelectedObjects != null && this.folv_history.SelectedObjects.Count > 0)
            {
                History hist = (History)this.folv_history.SelectedObjects[0];
                this.ShowMaskDetailsDialog(hist.Camera);

            }

        }

        private void dynamicMaskDetailsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (this.folv_history.SelectedObjects != null && this.folv_history.SelectedObjects.Count > 0)
            {
                History hist = (History)this.folv_history.SelectedObjects[0];
                this.ShowMaskDetailsDialog(hist.Camera);

            }
        }

        private void toolStripButtonEditImageMask_Click(object sender, EventArgs e)
        {
            if (this.folv_history.SelectedObjects != null && this.folv_history.SelectedObjects.Count > 0)
            {
                History hist = (History)this.folv_history.SelectedObjects[0];
                this.ShowEditImageMaskDialog(hist.Camera);

            }
        }

        private void storeFalseAlertsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AppSettings.Settings.HistoryStoreFalseAlerts = this.storeFalseAlertsToolStripMenuItem.Checked;
            AppSettings.SaveAsync(true);
        }

        private void storeMaskedAlertsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AppSettings.Settings.HistoryStoreMaskedAlerts = this.storeMaskedAlertsToolStripMenuItem.Checked;
            AppSettings.SaveAsync(true);

        }

        private void showOnlyRelevantObjectsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AppSettings.Settings.HistoryOnlyDisplayRelevantObjects = this.showOnlyRelevantObjectsToolStripMenuItem.Checked;
            AppSettings.SaveAsync(true);
            this.pictureBox1.Refresh();

        }

        private void btnSaveTo_Click(object sender, EventArgs e)
        {
            this.CameraSave(true);
        }

        private async Task<bool> FilterHistItem(History hist)
        {
            if (IsLoading)
                return false;

            bool ret = false;

            try
            {

                this.toolStripButtonPauseLog.Checked = true; //pause for a bit, and stay paused if results found

                using var cw = new Global_GUI.CursorWait();

                Stopwatch sw = new Stopwatch();

                //first search directly through log files (the history entry may not be from todays log)
                string justfile = Path.GetFileName(hist.Filename);

                List<ClsLogItm> found = new List<ClsLogItm>();

                //AITool.[2020-10-19].log
                //AITool.[2020-10-19.1].log.zip
                List<FileInfo> files = Global.GetFiles(Path.GetDirectoryName(AppSettings.Settings.LogFileName), "AITOOL.[*].LOG|AITOOL.[*].LOG.ZIP", SearchOption.TopDirectoryOnly);

                //sort by date so newest files are searched first
                files = files.OrderByDescending((d) => d.LastWriteTime).ToList();

                string imagemaskkey = "";
                string matchedmaskkey = "";

                foreach (var fi in files)
                {
                    //AITool.[2020-10-19.1].log.zip
                    //        ------------
                    string date = fi.FullName.GetWord("[", "]");
                    //AITool.[2020-10-19.1].log.zip
                    //        ----------
                    date = date.GetWord("", ".");

                    if (string.IsNullOrEmpty(date))
                    {
                        Log("Error: Date in filename is unexpected: " + fi.FullName);
                        continue;
                    }

                    DateTime DATE = DateTime.MinValue;

                    if (Global.GetDateStrict(date, ref DATE, "yyyy-MM-dd"))
                    {
                        if (DATE.ToString("yyyy-MM-dd") == hist.Date.ToString("yyyy-MM-dd") || fi.LastWriteTime.ToString("yyyy-MM-dd") == hist.Date.ToString("yyyy-MM-dd") || fi.CreationTime.ToString("yyyy-MM-dd") == hist.Date.ToString("yyyy-MM-dd"))
                        {
                            //load into memory
                            Log($"Debug: Searching log file (namedate='{DATE.ToString("yyyy-MM-dd")}',moddate='{fi.LastWriteTime.ToString("yyyy-MM-dd")}',createdate='{fi.CreationTime.ToString("yyyy-MM-dd")}'): {fi.Name}...");

                            this.UpdateProgressBar($"Searching {fi.Name}...", 1, 1, 1);

                            List<ClsLogItm> curlist = await LogMan.LoadLogFileAsync(fi.FullName, false, false);

                            this.UpdateProgressBar($"Searching {fi.Name}...", 1, 1, curlist.Count);

                            DateTime FirstSeen = DateTime.MinValue;

                            int fnd = 0;
                            int cnt = 0;
                            foreach (var CLI in curlist)
                            {
                                cnt++;
                                //this.UpdateProgressBar($"Searching {fi.Name}...", cnt, 1, curlist.Count);

                                if (CLI.Detail.IndexOf(justfile, StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    if (FirstSeen == DateTime.MinValue)
                                        FirstSeen = CLI.Date;
                                    fnd++;

                                    if (!found.Contains(CLI))
                                        found.Add(CLI);

                                    // Current object detected: key=2249882, name=Person, xmin=1789,
                                    if (CLI.Detail.IndexOf("Current object detected:", StringComparison.OrdinalIgnoreCase) >= 0)
                                    {
                                        imagemaskkey = "key=" + CLI.Detail.GetWord("key=", ",| ");
                                        Log("Debug: " + imagemaskkey);
                                    }
                                    //   Found 'Person' (Key=191457) in last_positions_history: key=198932, name=Person, xmin=1108
                                    else if (CLI.Detail.IndexOf("last_positions_history: key=", StringComparison.OrdinalIgnoreCase) >= 0)
                                    {
                                        matchedmaskkey = "key=" + CLI.Detail.GetWord("history: key=", ",| ");
                                        Log("Debug: " + matchedmaskkey);
                                    }
                                }
                                else if (CLI.Detail.Contains(imagemaskkey) && ((CLI.Date - FirstSeen).TotalMinutes <= 20 || CLI.Func.StartsWith("CleanUpExpired")))
                                {
                                    if (!found.Contains(CLI))
                                    {
                                        fnd++;
                                        found.Add(CLI);
                                    }
                                }
                                else if (CLI.Detail.Contains(matchedmaskkey) && ((CLI.Date - FirstSeen).TotalMinutes <= 20 || CLI.Func.StartsWith("CleanUpExpired")))
                                {
                                    if (!found.Contains(CLI))
                                    {
                                        fnd++;
                                        found.Add(CLI);
                                    }
                                }
                                else if (string.Equals(CLI.Image, justfile, StringComparison.OrdinalIgnoreCase))
                                {
                                    if (!found.Contains(CLI))
                                    {
                                        fnd++;
                                        found.Add(CLI);
                                    }
                                }
                            }

                            if (curlist.Count > 0)
                                Log($"Debug: ...Found {fnd} out of {curlist.Count} line matches for a total of {found.Count} in {fi.Name}...");
                            else
                                Log("Error: Log may be corrupt or an old format since no lines where returned: " + fi.Name);

                        }
                        else
                        {
                            Log($"Debug: Skipping file because Dates dont match: file ('{DATE.ToString("yyyy-MM-dd")}' != history '{hist.Date.ToString("yyyy-MM-dd")}') :{fi.Name}");
                        }
                    }
                    else
                    {
                        Log($"Error: Could not parse date '{date}' in filename, Hist.Date='{hist.Date.ToString("yyyy-MM-dd")}': " + fi.FullName);
                    }

                }

                Log($"Found {found.Count} total matched lines in {sw.ElapsedMilliseconds}ms for image '{justfile}'.");

                if (found.Count > 0)
                {
                    LogMan.Clear();
                    LogMan.AddRange(found);
                    String search = justfile;
                    if (!string.IsNullOrEmpty(imagemaskkey))
                        search += "|" + imagemaskkey;
                    if (!string.IsNullOrEmpty(matchedmaskkey))
                        search += "|" + matchedmaskkey;
                    this.tabControl1.SelectedTab = this.tabLog;
                    this.ToolStripComboBoxSearch.Text = search;  //this should trigger textchanged and filer in a second.
                }
                else
                {
                    this.toolStripButtonPauseLog.Checked = false; //start
                    MessageBox.Show($"Could not find matching log entries for '{justfile}'?  See log for details. Note this function only works well if the DEBUG logging mode has been enabled.");
                }

                ret = true;
            }
            catch (Exception ex)
            {

                Log("Error: " + ex.Msg());
            }
            finally
            {
                this.UpdateProgressBar($"", 0, 0, 0);
            }

            return ret;
        }

        private async void locateInLogToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (this.folv_history.SelectedObjects != null && this.folv_history.SelectedObjects.Count > 0)
            {
                History hist = (History)this.folv_history.SelectedObjects[0];
                await this.FilterHistItem(hist);

            }
        }

        private void cb_person_CheckedChanged(object sender, EventArgs e)
        {

        }

        private async void manuallyAddImagesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {

                string LastFile = Global.GetRegSetting("LastLoadedImageFile", "");

                if (!string.IsNullOrEmpty(LastFile))
                    ofd.InitialDirectory = Path.GetDirectoryName(LastFile);

                ofd.Multiselect = true;
                ofd.FileName = LastFile;
                ofd.Title = "Browse for image files for AI to process";
                ofd.CheckFileExists = true;
                ofd.CheckPathExists = true;
                ofd.DefaultExt = "jpg";
                ofd.Filter = "Image Files (*.jpg)|*.jpg|All files (*.*)|*.*";
                ofd.FilterIndex = 1;
                ofd.RestoreDirectory = true;

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    // Read the files
                    foreach (String file in ofd.FileNames)
                    {
                        Global.SaveRegSetting("LastLoadedImageFile", file);
                        AddImageToQueue(file);
                        //small delay
                        await Task.Delay(AppSettings.Settings.loop_delay_ms);
                    }
                }


            }
        }

        private void restrictThresholdAtSourceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AppSettings.Settings.HistoryRestrictMinThresholdAtSource = this.restrictThresholdAtSourceToolStripMenuItem.Checked;
            AppSettings.SaveAsync(true);
        }

        private void viewImageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (this.folv_history.SelectedObjects != null && this.folv_history.SelectedObjects.Count > 0)
            {
                History hist = (History)this.folv_history.SelectedObjects[0];
                ShellLauncher.Open(hist.Filename);

            }
        }

        private void jumpToImageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (this.folv_history.SelectedObjects != null && this.folv_history.SelectedObjects.Count > 0)
            {
                History hist = (History)this.folv_history.SelectedObjects[0];

                // combine the arguments together
                // it doesn't matter if there is a space after ','
                string argument = "/select, \"" + hist.Filename + "\"";

                Process.Start("explorer.exe", argument);
            }

        }

        private void BtnPredictionSize_Click(object sender, EventArgs e)
        {
            if (this.FOLV_Cameras.SelectedObjects.Count == 0)
            {
                MessageBox.Show("Select a camera first");
                return;
            }

            using (Frm_PredSizeLimits frm = new Frm_PredSizeLimits())
            {

                Camera cam = AITOOL.GetCamera(((Camera)this.FOLV_Cameras.SelectedObjects[0]).Name);

                frm.tb_ConfidenceLower.Text = cam.threshold_lower.ToString();
                frm.tb_ConfidenceUpper.Text = cam.threshold_upper.ToString();

                frm.tb_maxheight.Text = cam.PredSizeMaxHeight.ToString();
                frm.tb_maxwidth.Text = cam.PredSizeMaxWidth.ToString();
                frm.tb_minwidth.Text = cam.PredSizeMinWidth.ToString();
                frm.tb_minheight.Text = cam.PredSizeMinHeight.ToString();
                frm.tb_maxpercent.Text = cam.PredSizeMaxPercentOfImage.ToString();
                frm.tb_MinPercent.Text = cam.PredSizeMinPercentOfImage.ToString();

                frm.tb_duplicatepercent.Text = cam.MergePredictionsMinMatchPercent.ToString();

                frm.tb_loiterseconds.Text = cam.LoiterSecondsRequired.ToString();

                if (frm.ShowDialog() == DialogResult.OK)
                {
                    cam.threshold_lower = GetNumberInt(frm.tb_ConfidenceLower.Text);
                    cam.threshold_upper = GetNumberInt(frm.tb_ConfidenceUpper.Text);
                    cam.PredSizeMaxHeight = GetNumberInt(frm.tb_maxheight.Text);
                    cam.PredSizeMaxWidth = GetNumberInt(frm.tb_maxwidth.Text);
                    cam.PredSizeMinWidth = GetNumberInt(frm.tb_minwidth.Text);
                    cam.PredSizeMinHeight = GetNumberInt(frm.tb_minheight.Text);
                    cam.PredSizeMaxPercentOfImage = frm.tb_maxpercent.Text.ToDouble();
                    cam.PredSizeMinPercentOfImage = frm.tb_MinPercent.Text.ToDouble();
                    cam.MergePredictionsMinMatchPercent = frm.tb_duplicatepercent.Text.ToDouble();
                    cam.LoiterSecondsRequired = GetNumberInt(frm.tb_loiterseconds.Text);

                    Lbl_PredictionTolerances.Text = $"Threshold: {cam.threshold_lower}-{cam.threshold_upper}, Size: {cam.PredSizeMinPercentOfImage.ToPercent()}-{cam.PredSizeMaxPercentOfImage.ToPercent()} ; Width: {cam.PredSizeMinWidth}-{cam.PredSizeMaxWidth}, Height: {cam.PredSizeMinHeight}-{cam.PredSizeMaxHeight}, PredictionMatch: {cam.MergePredictionsMinMatchPercent.ToPercent()}, Loiter: {(cam.LoiterSecondsRequired > 0 ? cam.LoiterSecondsRequired + "s" : "off")}";

                    AppSettings.SaveAsync(true);
                }
            }

        }

        private void tb_threshold_lower_TextChanged(object sender, EventArgs e)
        {

        }

        private void BtnRelevantObjects_Click(object sender, EventArgs e)
        {
            if (this.FOLV_Cameras.SelectedObjects.Count == 0)
            {
                MessageBox.Show("Select a camera first");
                return;
            }

            using (Frm_RelevantObjects frm = new Frm_RelevantObjects())
            {
                Camera cam = AITOOL.GetCamera(((Camera)this.FOLV_Cameras.SelectedObjects[0]).Name);
                frm.ROMName = $"{cam.Name}\\{cam.DefaultTriggeringObjects.TypeName}";
                if (frm.ShowDialog(this) == DialogResult.OK)
                {
                    DisplayCameraSettings();
                }
            }
        }

        private void toolStripButtonAdjustAnno_Click(object sender, EventArgs e)
        {
            using (Frm_AnnoAdjust frm = new Frm_AnnoAdjust())
            {
                frm.ShowDialog();
            }
        }

        private void mergeDuplicatePredictionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AppSettings.Settings.HistoryMergeDuplicatePredictions = this.mergeDuplicatePredictionsToolStripMenuItem.Checked;
            AppSettings.SaveAsync(true);
        }
    }
}
