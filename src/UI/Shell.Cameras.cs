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

        //event: show history list filters button clicked
        //private void cb_showFilters_CheckedChanged(object sender, EventArgs e)
        //{
        //    if (cb_showFilters.Checked)
        //    {
        //        cb_showFilters.Text = "˅ Filter";
        //        splitContainer1.Panel2Collapsed = false;
        //    }
        //    else
        //    {
        //        cb_showFilters.Text = "˄ Filter";
        //        splitContainer1.Panel2Collapsed = true;
        //    }

        //    ResizeListViews();

        //}

        //event: filter "only revelant alerts" checked or unchecked



        //----------------------------------------------------------------------------------------------------------
        //CAMERAS TAB
        //----------------------------------------------------------------------------------------------------------

        //BASIC METHODS

        // load cameras to camera list
        public void LoadCameras()
        {
            using var Trace = new Trace();  //This c# 8.0 using feature will auto dispose when the function is done.

            try
            {
                //Global_GUI.InvokeIFRequired(this.FOLV_Cameras, () =>
                //{
                //start by getting last selected camera if any
                string oldnamecameras = "";
                if (this.FOLV_Cameras.SelectedObjects != null && this.FOLV_Cameras.SelectedObjects.Count > 0)
                    oldnamecameras = ((Camera)this.FOLV_Cameras.SelectedObjects[0]).Name;

                //if nothing was selected, then select the last saved camera:
                if (string.IsNullOrEmpty(oldnamecameras))
                    oldnamecameras = Global.GetRegSetting("LastSelectedCamera", "");

                if (string.IsNullOrEmpty(oldnamecameras))
                    oldnamecameras = "default";

                string oldnamefilters = "";
                if (this.comboBox_filter_camera.Items.Count > 0)
                    oldnamefilters = this.comboBox_filter_camera.Text;

                string oldnamestats = "";
                if (this.comboBox1.Items.Count > 0)
                    oldnamestats = this.comboBox1.Text;

                this.comboBox1.Items.Clear();
                this.comboBox1.Items.Add("All Cameras");
                this.comboBox_filter_camera.Items.Clear();
                this.comboBox_filter_camera.Items.Add("All Cameras");

                int i = 0;
                int oldidxcameras = 0;
                int oldidxfilters = 0;
                int oldidxstats = 0;
                Camera selectedcam = null;
                foreach (Camera cam in AppSettings.Settings.CameraList)
                {
                    //item.Tag = file; //tag is not used anywhere I can see
                    //add camera to combobox on overview tab and to camera filter combobox in the History tab 
                    this.comboBox1.Items.Add($"   {cam.Name}");
                    this.comboBox_filter_camera.Items.Add($"   {cam.Name}");
                    if (string.Equals(oldnamecameras.Trim(), cam.Name.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        oldidxcameras = i;
                        selectedcam = cam;
                    }
                    if (string.Equals(oldnamefilters.Trim(), cam.Name.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        oldidxfilters = i + 1;
                    }
                    if (string.Equals(oldnamestats.Trim(), cam.Name.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        oldidxstats = i + 1;
                    }
                    i++;

                }

                if (selectedcam == null && AppSettings.Settings.CameraList.Count > 0)
                    selectedcam = AppSettings.Settings.CameraList[0];

                Global_GUI.UpdateFOLV(FOLV_Cameras, AppSettings.Settings.CameraList, false, ColumnHeaderAutoResizeStyle.ColumnContent, true, true, selectedcam);

                if (this.comboBox_filter_camera.Items.Count > 0)
                    this.comboBox_filter_camera.SelectedIndex = oldidxfilters;

                if (this.comboBox1.Items.Count > 0)
                    this.comboBox1.SelectedIndex = oldidxstats;

                //});

            }
            catch (Exception ex)
            {
                Log("ERROR: LoadCameras() failed: " + ex.Msg());
                MessageBox.Show("ERROR: LoadCameras() failed: " + ex.Msg());
            }

        }

        //load existing camera (settings file exists) into CameraList, into Stats dropdown and into History filter dropdown 
        //private string LoadCamera(string config_path)
        //{
        //    //check if camera with specified name or its prefix already exists. If yes, then abort.
        //    foreach (Camera c in AppSettings.Settings.CameraList)
        //    {
        //        if (c.name == Path.GetFileNameWithoutExtension(config_path))
        //        {
        //            return ($"ERROR: Camera name must be unique,{Path.GetFileNameWithoutExtension(config_path)} already exists.");
        //        }
        //        if (c.prefix == System.IO.File.ReadAllLines(config_path)[2].Split('"')[1])
        //        {
        //            return ($"ERROR: Every camera must have a unique prefix ('Input file begins with'), but the prefix of {Path.GetFileNameWithoutExtension(config_path)} equals the prefix of the existing camera {c.name} .");
        //        }
        //    }
        //    Camera cam = new Camera(); //create new camera object
        //    Log("read config");
        //    cam.ReadConfig(config_path); //read camera's config from file
        //    Log("add");
        //    AppSettings.Settings.CameraList.Add(cam); //add created camera object to CameraList

        //    //add camera to combobox on overview tab and to camera filter combobox in the History tab 
        //    comboBox1.Items.Add($"   {cam.name}");
        //    comboBox_filter_camera.Items.Add($"   {cam.name}");

        //    return ($"SUCCESS: {Path.GetFileNameWithoutExtension(config_path)} loaded.");
        //}

        //add camera
        private string AddCamera(Camera cam) //, int history_mins, int mask_create_counter, int mask_remove_counter, double percent_variance)
        {
            using var Trace = new Trace();  //This c# 8.0 using feature will auto dispose when the function is done.

            //check if camera with specified name already exists. If yes, then abort.
            foreach (Camera c in AppSettings.Settings.CameraList)
            {
                if (string.Equals(c.Name.Trim(), cam.Name.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show($"ERROR: Camera name must be unique,{cam.Name} already exists.");
                    return ($"ERROR: Camera name must be unique,{cam.Name} already exists.");
                }
            }

            //check if name is empty
            if (cam.Name == "")
            {
                MessageBox.Show($"ERROR: Camera name may not be empty.");
                return ($"ERROR: Camera name may not be empty.");
            }


            if (AITOOL.BlueIrisInfo.Result == BlueIrisResult.Valid)
            {
                //http://10.0.1.99:81/admin?trigger&camera=BACKFOSCAM&user=AITools&pw=haha&memo=[summary]
                cam.trigger_urls_as_string = "[BlueIrisURL]/admin?camera=[camera]&trigger&user=[Username]&pw=[Password]&flagalert=2&memo=[summary]&jpeg=[ImagePathEscaped]";
            }

            //I dont think this is used anywhere
            cam.triggering_objects = cam.triggering_objects_as_string.SplitStr(",").ToArray();   //triggering_objects_as_string.Split(','); //split the row of triggering objects between every ','

            //Split by cr/lf or other common delimiters
            cam.trigger_urls = cam.trigger_urls_as_string.SplitStr("\r\n|").ToArray();  //all trigger urls in an array
            cam.cancel_urls = cam.cancel_urls_as_string.SplitStr("\r\n|").ToArray();  //all trigger urls in an array

            cam.BICamName = cam.Name;

            cam.MaskFileName = $"{cam.Name}.bmp";

            AppSettings.Settings.CameraList.Add(cam); //add created camera object to CameraList

            this.LoadCameras();

            return ($"SUCCESS: {cam.Name} created.");
        }



        //remove camera
        private void RemoveCamera(System.Collections.IList objs)
        {
            using var Trace = new Trace();  //This c# 8.0 using feature will auto dispose when the function is done.

            for (int i = 0; i < objs.Count; i++)
            {
                Log($"Removing camera {((Camera)objs[i]).Name}...");

                AppSettings.Settings.CameraList.Remove((Camera)objs[i]);

            }
            if (this.FOLV_Cameras.Items.Count > 0)
                this.FOLV_Cameras.SelectedIndex = 0;
            AppSettings.SaveAsync(true);
            this.LoadCameras();

        }

        //display camera settings for selected camera
        private void DisplayCameraSettings()
        {
            using var Trace = new Trace();  //This c# 8.0 using feature will auto dispose when the function is done.

            try
            {
                if (this.FOLV_Cameras.SelectedObjects != null && this.FOLV_Cameras.SelectedObjects.Count > 0)
                {
                    Camera cam = ((Camera)this.FOLV_Cameras.SelectedObjects[0]);

                    if (cam.last_image_file.IsNotEmpty() && File.Exists(cam.last_image_file))
                    {
                        using (var img = new Bitmap(cam.last_image_file))
                        {
                            this.pictureBoxCamera.Image = new Bitmap(img); //load mask as overlay
                        }
                    }
                    else
                    {
                        this.pictureBoxCamera.Image = null; //if file does not exist, empty mask overlay (from possible overlays of previous images)
                    }

                    UpdateActionsLabel(cam);

                    Lbl_PredictionTolerances.Text = $"Threshold: {cam.threshold_lower}-{cam.threshold_upper}, Size: {cam.PredSizeMinPercentOfImage.ToPercent()}-{cam.PredSizeMaxPercentOfImage.ToPercent()} ; Width: {cam.PredSizeMinWidth}-{cam.PredSizeMaxWidth}, Height: {cam.PredSizeMinHeight}-{cam.PredSizeMaxHeight}, PredictionMatch: {cam.MergePredictionsMinMatchPercent.ToPercent()}, Loiter: {(cam.LoiterSecondsRequired > 0 ? cam.LoiterSecondsRequired + "s" : "off")}";

                    this.tableLayoutPanel6.Enabled = true;

                    this.tbName.Text = cam.Name; //load name textbox from name in list2
                    this.tbBiCamName.Text = cam.BICamName;
                    this.tbCustomMaskFile.Text = cam.MaskFileName;

                    //load cameras stats
                    string stats = $"Alerts: {cam.stats_alerts.ToString()} | Irrelevant Alerts: {cam.stats_irrelevant_alerts.ToString()} | False Alerts: {cam.stats_false_alerts.ToString()}";

                    if (cam.maskManager.MaskingEnabled)
                    {
                        stats += $" | Mask History Count: {cam.maskManager.LastPositionsHistory.Count} | Current Dynamic Masks: {cam.maskManager.MaskedPositions.Count}";
                    }
                    this.lbl_camstats.Text = stats;

                    //load if ai detection is active for the camera
                    if (cam.enabled == true)
                    {
                        this.cb_enabled.Checked = true;
                    }
                    else
                    {
                        this.cb_enabled.Checked = false;
                    }
                    this.tbPrefix.Text = cam.Prefix; //load 'input file begins with'
                    this.lbl_prefix.Text = this.tbPrefix.Text + ".××××××.jpg"; //prefix live preview

                    this.cmbcaminput.Text = cam.input_path;
                    this.cmbcaminput.Items.Clear();
                    foreach (string pth in AITOOL.BlueIrisInfo.ClipPaths)
                    {
                        this.cmbcaminput.Items.Add(pth);
                    }

                    this.cb_monitorCamInputfolder.Checked = cam.input_path_includesubfolders;

                    this.tb_camera_telegram_chatid.Text = cam.telegram_chatid;

                    //load is masking enabled 
                    this.cb_masking_enabled.Checked = cam.maskManager.MaskingEnabled;


                    this.lbl_RelevantObjects.Text = cam.DefaultTriggeringObjects.ToString();
                    ////load triggering objects
                    ////first create arrays with all checkboxes stored in
                    //CheckBox[] cbarray = new CheckBox[] { this.cb_airplane, this.cb_bear, this.cb_bicycle, this.cb_bird, this.cb_boat, this.cb_bus, this.cb_car, this.cb_cat, this.cb_cow, this.cb_dog, this.cb_horse, this.cb_motorcycle, this.cb_person, this.cb_sheep, this.cb_truck };
                    ////create array with strings of the triggering_objects related to the checkboxes in the same order
                    //string[] cbstringarray = new string[] { "Airplane", "Bear", "Bicycle", "Bird", "Boat", "Bus", "Car", "Cat", "Cow", "Dog", "Horse", "Motorcycle", "Person", "Sheep", "Truck" };

                    ////clear all checkmarks
                    //foreach (CheckBox cb in cbarray)
                    //{
                    //    cb.Checked = false;
                    //}

                    ////check for every triggering_object string if it is active in the settings file. If yes, check according checkbox
                    //for (int j = 0; j < cbarray.Length; j++)
                    //{
                    //    if (cam.triggering_objects_as_string.IndexOf(cbstringarray[j], StringComparison.OrdinalIgnoreCase) >= 0)
                    //    {
                    //        cbarray[j].Checked = true;
                    //    }
                    //}

                    //this.tbAdditionalRelevantObjects.Text = cam.additional_triggering_objects_as_string;


                }

            }
            catch (Exception ex)
            {

                string err = $"Error: While displaying camera settings, got error: {ex.Msg()}";
                Log(err);
                MessageBox.Show(err);
            }
        }



        // SPECIAL METHODS

        //input file begins with live preview
        private void tbPrefix_TextChanged(object sender, EventArgs e)
        {
            this.lbl_prefix.Text = this.tbPrefix.Text + ".××××××.jpg";
        }



        //event: camera list another item selected


        //event: camera add button
        private void btnCameraAdd_Click(object sender, EventArgs e)
        {

            using (Frm_CameraAdd frm = new Frm_CameraAdd())
            {

                //add only cameras not already installed
                foreach (string camstr in AITOOL.BlueIrisInfo.Cameras)
                {
                    if (GetCamera(camstr, false) == null)
                        frm.checkedListBoxCameras.Items.Add(camstr, false);

                }

                if (frm.ShowDialog() == DialogResult.OK)
                {
                    int added = 0;
                    List<string> cams = frm.tb_Cameras.Text.SplitStr("\r\n|;,");
                    Camera DefCam = GetCamera("default", true);

                    foreach (var cs in cams)
                    {

                        string cn = cs;

                        //if (cn.StartsWith("ai", StringComparison.OrdinalIgnoreCase))
                        //{
                        //    cn = Name.Substring(2).TrimStart(@"_-".ToCharArray()).Trim();  //if using dupe cam that may start with AICAMNAME or AI_CAMNAME
                        //    string maskfile = AITOOL.GetMaskFile(cn);
                        //    string newfile = AITOOL.GetMaskFile(cs);
                        //    if (File.Exists(maskfile) && !File.Exists(newfile))
                        //        File.Move(maskfile, newfile);
                        //}

                        if (GetCamera(cn, false) == null)
                        {

                            Camera cam = null;   // new Camera(cn);

                            //Try to use the default camera settings when creating a new camera
                            if (!DefCam.IsNull())
                            {
                                cam = DefCam.CloneJson();
                                cam.Name = cn;
                                cam.BICamName = cn;
                                cam.Prefix = cn;
                                cam.UpdateCamera();
                            }
                            else
                            {
                                cam = new Camera(cn);
                            }

                            string camresult = this.AddCamera(cam);

                            Log(camresult);
                            if (camresult.StartsWith("success", StringComparison.OrdinalIgnoreCase))
                            {
                                added++;
                                Global.SaveRegSetting("LastSelectedCamera", cam.Name);
                            }

                        }
                        else
                        {
                            Log($"Error: Camera already existed {cs}.");
                        }

                    }

                    MessageBox.Show($"Added {added} out of {cams.Count}.  See log for details.");

                }
            }

            //using (var form = new InputForm("Camera Name:", "New Camera", cbitems: BlueIrisInfo.Cameras))
            //{
            //    var result = form.ShowDialog();
            //    if (result == DialogResult.OK)
            //    {
            //        Camera cam = new Camera(form.text);

            //        string camresult = this.AddCamera(cam);

            //        // Old way...
            //        //string name, string prefix, string trigger_urls_as_string, string triggering_objects_as_string, bool telegram_enabled, bool enabled, double cooldown_time, int threshold_lower, int threshold_upper,
            //        //                                 string _input_path, bool _input_path_includesubfolders,
            //        //                                 bool masking_enabled,
            //        //                                 bool trigger_cancels

            //        MessageBox.Show(camresult);
            //    }
            //}
        }

        //event: save camera settings button
        private void btnCameraSave_Click_1(object sender, EventArgs e)
        {

            this.CameraSave(false);
        }

        private void CameraSave(bool SaveTo)
        {
            using var Trace = new Trace();  //This c# 8.0 using feature will auto dispose when the function is done.

            try
            {
                if (this.FOLV_Cameras.SelectedObjects.Count > 0)
                {
                    //check if name is empty
                    if (String.IsNullOrWhiteSpace(this.tbName.Text))
                    {
                        this.DisplayCameraSettings(); //reset displayed settings
                        MessageBox.Show($"WARNING: Camera name may not be empty.", "", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    if (!string.Equals(((Camera)this.FOLV_Cameras.SelectedObjects[0]).Name.Trim(), this.tbName.Text.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        //camera renamed, make sure name doesnt exist
                        Camera CamCheck = AITOOL.GetCamera(this.tbName.Text, false);
                        if (CamCheck != null)
                        {
                            //Its a dupe
                            MessageBox.Show($"WARNING: Camera name must be unique, but new camera name '{this.tbName.Text}' already exists.", "", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            this.DisplayCameraSettings(); //reset displayed settings
                            return;
                        }
                    }

                    Camera cam = AITOOL.GetCamera(((Camera)this.FOLV_Cameras.SelectedObjects[0]).Name, false);

                    if (cam == null)
                    {
                        //should not happen, but...
                        MessageBox.Show($"WARNING: Camera not found???  '{((Camera)this.FOLV_Cameras.SelectedObjects[0]).Name}'", "", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        this.DisplayCameraSettings(); //reset displayed settings
                        return;
                    }

                    if (cam.input_path.IsNotNull() && cam.Action_network_folder.IsNotNull() && cam.input_path.TrimEnd("\\".ToCharArray()).EqualsIgnoreCase(cam.Action_network_folder.TrimEnd("\\".ToCharArray())))
                    {
                        //You dont want to watch, then copy the same file back to the same folder
                        MessageBox.Show($"WARNING: Input path ({cam.input_path}) & 'Copy alert images to folder' path may not be the same for camera '{cam.Name}'", "", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        this.DisplayCameraSettings(); //reset displayed settings
                        return;
                    }

                    //1. GET SETTINGS INPUTTED
                    //all checkboxes in one array

                    //person,   bicycle,   car,   motorcycle,   airplane,
                    //bus,   train,   truck,   boat,   traffic light,   fire hydrant,   stop_sign,
                    //parking meter,   bench,   bird,   cat,   dog,   horse,   sheep,   cow,   elephant,
                    //bear,   zebra, giraffe,   backpack,   umbrella,   handbag,   tie,   suitcase,
                    //frisbee,   skis,   snowboard, sports ball,   kite,   baseball bat,   baseball glove,
                    //skateboard,   surfboard,   tennis racket, bottle,   wine glass,   cup,   fork,
                    //knife,   spoon,   bowl,   banana,   apple,   sandwich,   orange, broccoli,   carrot,
                    //hot dog,   pizza,   donot,   cake,   chair,   couch,   potted plant,   bed, dining table,
                    //toilet,   tv,   laptop,   mouse,   remote,   keyboard,   cell phone,   microwave,
                    //oven,   toaster,   sink,   refrigerator,   book,   clock,   vase,   scissors,   teddy bear,
                    //hair dryer, toothbrush.

                    //CheckBox[] cbarray = new CheckBox[] { this.cb_airplane, this.cb_bear, this.cb_bicycle, this.cb_bird, this.cb_boat, this.cb_bus, this.cb_car, this.cb_cat, this.cb_cow, this.cb_dog, this.cb_horse, this.cb_motorcycle, this.cb_person, this.cb_sheep, this.cb_truck };
                    ////create array with strings of the triggering_objects related to the checkboxes in the same order
                    //string[] cbstringarray = new string[] { "Airplane", "Bear", "Bicycle", "Bird", "Boat", "Bus", "Car", "Cat", "Cow", "Dog", "Horse", "Motorcycle", "Person", "Sheep", "Truck" };

                    ////go through all checkboxes and write all triggering_objects in one string
                    //cam.triggering_objects_as_string = "";
                    //for (int i = 0; i < cbarray.Length; i++)
                    //{
                    //    if (cbarray[i].Checked == true)
                    //    {
                    //        cam.triggering_objects_as_string += $"{cbstringarray[i].Trim()}, ";
                    //    }
                    //}


                    //cam.triggering_objects = cam.triggering_objects_as_string.Split(",").ToArray();   //triggering_objects_as_string.Split(','); //split the row of triggering objects between every ','

                    //cam.additional_triggering_objects_as_string = this.tbAdditionalRelevantObjects.Text.Trim();

                    cam.trigger_urls = cam.trigger_urls_as_string.SplitStr("\r\n|").ToArray();  //all trigger urls in an array
                    cam.cancel_urls = cam.cancel_urls_as_string.SplitStr("\r\n|").ToArray();

                    if (cam.Name != this.tbName.Text.Trim())
                    {
                        string Oldmaskfile = cam.GetMaskFile(false);
                        if (!string.IsNullOrEmpty(Oldmaskfile) && File.Exists(Oldmaskfile))
                        {
                            string ext = Path.GetExtension(Oldmaskfile);
                            string pth = Path.GetDirectoryName(Oldmaskfile);
                            string NewMaskFile = Path.Combine(pth, this.tbName.Text.Trim() + ext);
                            File.Move(Oldmaskfile, NewMaskFile);
                            cam.MaskFileName = NewMaskFile;
                        }
                        Log($"Renaming Camera '{cam.Name}' to '{this.tbName.Text}'");
                        cam.Name = this.tbName.Text.Trim();  //just in case we needed to rename it
                    }

                    cam.BICamName = this.tbBiCamName.Text.Trim();
                    cam.MaskFileName = this.tbCustomMaskFile.Text.Trim();

                    cam.Prefix = this.tbPrefix.Text.Trim();
                    cam.enabled = this.cb_enabled.Checked;
                    cam.maskManager.MaskingEnabled = this.cb_masking_enabled.Checked;
                    cam.input_path = this.cmbcaminput.Text.Trim();
                    cam.input_path_includesubfolders = this.cb_monitorCamInputfolder.Checked;

                    cam.telegram_chatid = this.tb_camera_telegram_chatid.Text.Trim();

                    int ccnt = 0;

                    if (SaveTo)
                    {
                        using (Frm_ApplyCameraTo frm = new Frm_ApplyCameraTo())
                        {
                            foreach (Camera ccam in AppSettings.Settings.CameraList)
                            {
                                if (ccam.Name != cam.Name)
                                    frm.checkedListBoxCameras.Items.Add(ccam.Name, false);
                            }

                            if (frm.ShowDialog() == DialogResult.OK)
                            {
                                for (int i = 0; i < frm.checkedListBoxCameras.Items.Count; i++)
                                {
                                    if (frm.checkedListBoxCameras.GetItemChecked(i))
                                    {
                                        Camera icam = AITOOL.GetCamera(frm.checkedListBoxCameras.Items[i].ToString(), false);
                                        if (icam != null)
                                        {
                                            ccnt++;

                                            Log($"Updating camera '{cam.Name}' with settings from '{icam.Name}'...");

                                            //icam.BICamName = cam.BICamName;


                                            if (frm.cb_apply_confidence_limits.Checked)
                                            {
                                                icam.threshold_lower = cam.threshold_lower;
                                                icam.threshold_upper = cam.threshold_upper;
                                                icam.MergePredictionsMinMatchPercent = cam.MergePredictionsMinMatchPercent;
                                                icam.PredSizeMinHeight = cam.PredSizeMinHeight;
                                                icam.PredSizeMinWidth = cam.PredSizeMinWidth;
                                                icam.PredSizeMaxHeight = cam.PredSizeMaxHeight;
                                                icam.PredSizeMaxWidth = cam.PredSizeMaxWidth;
                                                icam.PredSizeMaxPercentOfImage = cam.PredSizeMaxPercentOfImage;
                                                icam.PredSizeMinPercentOfImage = cam.PredSizeMinPercentOfImage;
                                                icam.LoiterSecondsRequired = cam.LoiterSecondsRequired;
                                            }
                                            if (frm.cb_apply_objects.Checked)
                                            {
                                                icam.triggering_objects_as_string = cam.triggering_objects_as_string;
                                                icam.additional_triggering_objects_as_string = cam.additional_triggering_objects_as_string;
                                                icam.triggering_objects = cam.triggering_objects_as_string.SplitStr(",").ToArray();   //triggering_objects_as_string.Split(','); //split the row of triggering objects between every ','

                                                icam.Action_pushover_triggering_objects = cam.Action_pushover_triggering_objects;
                                                icam.DefaultTriggeringObjects.ObjectList = cam.DefaultTriggeringObjects.CloneObjectList();
                                                icam.TelegramTriggeringObjects.ObjectList = cam.TelegramTriggeringObjects.CloneObjectList();
                                                icam.MQTTTriggeringObjects.ObjectList = cam.MQTTTriggeringObjects.CloneObjectList();
                                                icam.PushoverTriggeringObjects.ObjectList = cam.PushoverTriggeringObjects.CloneObjectList();
                                                icam.maskManager.MaskTriggeringObjects.ObjectList = cam.maskManager.MaskTriggeringObjects.CloneObjectList();

                                            }
                                            if (frm.cb_apply_actions.Checked)
                                            {
                                                icam.trigger_urls_as_string = cam.trigger_urls_as_string;
                                                icam.trigger_urls = cam.trigger_urls;
                                                icam.Action_TriggerURL_Enabled = cam.Action_TriggerURL_Enabled;
                                                icam.Action_CancelURL_Enabled = cam.Action_CancelURL_Enabled;
                                                icam.cancel_urls_as_string = cam.cancel_urls_as_string;
                                                icam.cancel_urls = cam.cancel_urls;
                                                icam.cooldown_time_seconds = cam.cooldown_time_seconds;

                                                icam.DetectionDisplayFormat = cam.DetectionDisplayFormat;


                                                icam.telegram_enabled = cam.telegram_enabled;
                                                icam.telegram_caption = cam.telegram_caption;
                                                icam.telegram_chatid = cam.telegram_chatid;
                                                icam.telegram_active_time_range = cam.telegram_active_time_range;

                                                icam.Action_image_copy_enabled = cam.Action_image_copy_enabled;
                                                icam.Action_network_folder = cam.Action_network_folder;
                                                icam.Action_network_folder_filename = cam.Action_network_folder_filename;
                                                icam.Action_RunProgram = cam.Action_RunProgram;
                                                icam.Action_RunProgramString = cam.Action_RunProgramString;
                                                icam.Action_RunProgramArgsString = cam.Action_RunProgramArgsString;
                                                icam.Action_PlaySounds = cam.Action_PlaySounds;
                                                icam.Action_Sounds = cam.Action_Sounds;

                                                icam.Action_mqtt_enabled = cam.Action_mqtt_enabled;
                                                icam.Action_mqtt_payload = cam.Action_mqtt_payload;
                                                icam.Action_mqtt_topic = cam.Action_mqtt_topic;
                                                icam.Action_mqtt_payload_cancel = cam.Action_mqtt_payload_cancel;
                                                icam.Action_mqtt_topic_cancel = cam.Action_mqtt_topic_cancel;

                                                icam.Action_image_merge_detections = cam.Action_image_merge_detections;
                                                icam.Action_image_merge_jpegquality = cam.Action_image_merge_jpegquality;

                                                icam.Action_pushover_enabled = cam.Action_pushover_enabled;
                                                icam.Action_pushover_title = cam.Action_pushover_title;
                                                icam.Action_pushover_message = cam.Action_pushover_message;
                                                icam.Action_pushover_device = cam.Action_pushover_device;
                                                icam.Action_pushover_Sound = cam.Action_pushover_Sound;
                                                icam.Action_pushover_Priority = cam.Action_pushover_Priority;
                                                icam.Action_pushover_retrycallback_url = cam.Action_pushover_retrycallback_url;
                                                icam.Action_pushover_SupplementaryUrl = cam.Action_pushover_SupplementaryUrl;
                                                icam.Action_pushover_expire_seconds = cam.Action_pushover_expire_seconds;
                                                icam.Action_pushover_retry_seconds = cam.Action_pushover_retry_seconds;

                                                icam.Action_pushover_active_time_range = cam.Action_pushover_active_time_range;

                                                icam.Action_queued = cam.Action_queued;
                                            }
                                            if (frm.cb_apply_mask_settings.Checked)
                                            {
                                                icam.maskManager.MaskingEnabled = cam.maskManager.MaskingEnabled;

                                                icam.maskManager.HistorySaveMins = cam.maskManager.HistorySaveMins;
                                                icam.maskManager.HistoryThresholdCount = cam.maskManager.HistoryThresholdCount;
                                                icam.maskManager.MaskRemoveThreshold = cam.maskManager.MaskRemoveThreshold;
                                                icam.maskManager.MaskRemoveMins = cam.maskManager.MaskRemoveMins;

                                                icam.maskManager.PercentMatch = cam.maskManager.PercentMatch;

                                                icam.maskManager.ScaleConfig.IsScaledObject = cam.maskManager.ScaleConfig.IsScaledObject;
                                                icam.maskManager.ScaleConfig.MediumObjectMatchPercent = cam.maskManager.ScaleConfig.MediumObjectMatchPercent;
                                                icam.maskManager.ScaleConfig.MediumObjectMaxPercent = cam.maskManager.ScaleConfig.MediumObjectMaxPercent;
                                                icam.maskManager.ScaleConfig.SmallObjectMatchPercent = cam.maskManager.ScaleConfig.SmallObjectMatchPercent;
                                                icam.maskManager.ScaleConfig.SmallObjectMaxPercent = cam.maskManager.ScaleConfig.SmallObjectMaxPercent;

                                            }

                                        }
                                    }

                                }

                            }
                        }
                    }
                    else
                    {
                        ccnt = 1;
                    }

                    this.LoadCameras();

                    AppSettings.SaveAsync(true);

                    UpdateWatchers(true);

                    string saved = $"{ccnt} Camera(s) saved.";
                    Log(saved);

                    MessageBox.Show(saved, "", MessageBoxButtons.OK, MessageBoxIcon.Information);


                    ////2. UPDATE SETTINGS
                    //// save new camera settings, display result in MessageBox
                    //string result = UpdateCamera(list2.SelectedItems[0].Text, tbName.Text, tbPrefix.Text, tbTriggerUrl.Text, triggering_objects_as_string, cb_telegram.Checked, cb_enabled.Checked, cooldown_time, threshold_lower, threshold_upper,
                    //                             cmbcaminput.Text, cb_monitorCamInputfolder.Checked,
                    //                             cb_masking_enabled.Checked,
                    //                             cb_TriggerCancels.Checked); //, history_mins, mask_create_counter, mask_remove_counter, percent_variance);



                    //1     2       3                             4                       5                 6        7              8                9
                    //name, prefix, triggering_objects_as_string, trigger_urls_as_string, telegram_enabled, enabled, cooldown_time, threshold_lower, threshold_upper,
                    //                                                               10           11  
                    //                                                               _input_path, _input_path_includesubfolders,
                    //                                                          12   masking_enabled,
                    //                                                          13   trigger_cancel



                }
                this.DisplayCameraSettings();

            }
            catch (Exception ex)
            {

                string err = $"Error: While saving camera, got error: {ex.Msg()}";
                Log(err);
                MessageBox.Show(err);
            }

        }
        //event: delete camera button
        private void btnCameraDel_Click(object sender, EventArgs e)
        {
            if (this.FOLV_Cameras.SelectedObjects.Count > 0)
            {
                using (var form = new InputForm($"Delete camera {((Camera)this.FOLV_Cameras.SelectedObjects[0]).Name} ?", "Delete Camera?", false))
                {
                    var result = form.ShowDialog();
                    if (result == DialogResult.OK)
                    {
                        //Log("about to del cam");
                        this.RemoveCamera(this.FOLV_Cameras.SelectedObjects);
                    }
                }
            }
        }

        //event: DELETE key pressed
        private void list2_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete)
            {
                if (this.FOLV_Cameras.SelectedObjects.Count > 0)
                {
                    using (var form = new InputForm($"Delete camera {((Camera)this.FOLV_Cameras.SelectedObjects[0]).Name} ?", "Delete Camera?", false))
                    {
                        var result = form.ShowDialog();
                        if (result == DialogResult.OK)
                        {
                            this.RemoveCamera(this.FOLV_Cameras.SelectedObjects);
                        }
                    }
                }
            }
        }

        private void FOLV_Cameras_SelectionChanged(object sender, EventArgs e)
        {
            if (FOLV_Cameras.SelectedObjects.Count > 0)
                Global.SaveRegSetting("LastSelectedCamera", ((Camera)FOLV_Cameras.SelectedObjects[0]).Name);

            DisplayCameraSettings();
        }

        private void FOLV_Cameras_FormatRow(object sender, FormatRowEventArgs e)
        {
            this.FormatCameraRow(sender, e);
        }
    }
}
