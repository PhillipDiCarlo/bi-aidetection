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

        private async Task UpdateLogAddedRemovedAsync(bool Follow = false, bool Force = false)
        {
            using var Trace = new Trace();  //This c# 8.0 using feature will auto dispose when the function is done.

            if (IsLoading)
                return;

            if (!this.IsLogListUpdating && Force ||
                !this.IsLogListUpdating &&
                (this.tabControl1.SelectedTab == this.tabControl1.TabPages["tabLog"]) &&
                this.Visible &&
                !(this.WindowState == FormWindowState.Minimized) &&
                LogMan != null)
            {
                //run in another thread so gui doesnt freeze
                //await Task.Run(() =>
                //{
                Stopwatch sw = Stopwatch.StartNew();

                this.IsLogListUpdating = true;

                Global_GUI.InvokeIFRequired(this.folv_log, async () =>
                {
                    if (this.folv_log.Items.Count == 0)
                        this.folv_log.EmptyListMsg = "Loading log...";

                    List<ClsLogItm> added = LogMan.GetRecentlyAdded();
                    List<ClsLogItm> removed = LogMan.GetRecentlyDeleted();

                    if (added.Count > 2000 || this.folv_log.Items.Count == 0)
                    {
                        //do it all in one update so it is faster:
                        using var cw = new Global_GUI.CursorWait();
                        // run in another thread so gui doesnt freeze
                        await Task.Run(() =>
                        {
                            Global_GUI.UpdateFOLV(this.folv_log, LogMan.Values, (Follow || AppSettings.Settings.Autoscroll_log), FullRefresh: true);
                        });
                    }
                    else
                    {
                        if (removed.Count > 0)
                            this.folv_log.RemoveObjects(removed);

                        if (added.Count > 0)
                            Global_GUI.UpdateFOLV(this.folv_log, added, (Follow || AppSettings.Settings.Autoscroll_log));
                    }


                    if (sw.ElapsedMilliseconds > 5000 && sw.ElapsedMilliseconds < 10000)
                    {
                        Log($"Debug: ---- Log window update took {sw.ElapsedMilliseconds}ms to load. {added.Count} added, {removed.Count} removed, {this.folv_history.Items.Count} total. Consider lowering the '{nameof(AppSettings.Settings.MaxGUILogItems)}' setting in JSON file (Currently {AppSettings.Settings.MaxGUILogItems}) ");
                    }
                    else if (sw.ElapsedMilliseconds > 10000)
                    {
                        Log($"Warn: ---- Log window update took {sw.ElapsedMilliseconds}ms to load. {added.Count} added, {removed.Count} removed, {this.folv_history.Items.Count} total. Consider lowering the '{nameof(AppSettings.Settings.MaxGUILogItems)}' setting in JSON file (Currently {AppSettings.Settings.MaxGUILogItems}) ");
                    }

                    this.UpdateStats();


                });

                //});

                this.IsLogListUpdating = false;

            }
            else
            {

            }
        }

        //open log button
        private void btn_open_log_Click(object sender, EventArgs e)
        {
            OpenLogFile();

        }


        private void btnViewLog_Click(object sender, EventArgs e)
        {
            OpenLogFile();
        }

        private void Chk_AutoScroll_CheckedChanged(object sender, EventArgs e)
        {
            AppSettings.Settings.Autoscroll_log = this.Chk_AutoScroll.Checked;
        }

        private void tabLog_Click(object sender, EventArgs e)
        {

        }

        private async void LogUpdateListTimer_Tick(object sender, EventArgs e)
        {
            await this.UpdateLogAddedRemovedAsync();
        }

        private void Chk_AutoScroll_Click(object sender, EventArgs e)
        {
            AppSettings.Settings.Autoscroll_log = this.Chk_AutoScroll.Checked;
        }



        private void Chk_AutoScroll_Click_1(object sender, EventArgs e)
        {
            AppSettings.Settings.Autoscroll_log = this.Chk_AutoScroll.Checked;
        }

        private void chk_filterErrors_Click(object sender, EventArgs e)
        {
            this.FilterLogErrors();
        }

        private async void FilterLogErrors()
        {
            if (IsLoading)
                return;


            if (this.chk_filterErrors.Checked)
            {
                //filter
                Global_GUI.InvokeIFRequired(this.folv_log, () =>
                {
                    using var cw = new Global_GUI.CursorWait();
                    this.folv_log.ModelFilter = new BrightIdeasSoftware.ModelFilter((object x) =>
                    {
                        ClsLogItm CLI = (ClsLogItm)x;
                        return (CLI.Level == LogLevel.Error || CLI.Level == LogLevel.Warn || CLI.Level == LogLevel.Fatal);
                    });
                });
            }
            else
            {
                this.folv_log.ModelFilter = null;
                await this.UpdateLogAddedRemovedAsync(true);
            }

        }

        private void folv_log_FormatRow(object sender, BrightIdeasSoftware.FormatRowEventArgs e)
        {
            if (e.Model != null)
                this.FormatLogRow(sender, e);
        }
        private void FormatLogRow(object Sender, BrightIdeasSoftware.FormatRowEventArgs e)
        {
            try
            {
                ClsLogItm li = (ClsLogItm)e.Model;

                // If SPI IsNot Nothing Then
                if (li.FromFile)
                {
                    e.Item.BackColor = Color.Black;
                }
            }



            catch (Exception)
            {
            }
            finally
            {
            }
        }

        private void folv_log_FormatCell(object sender, BrightIdeasSoftware.FormatCellEventArgs e)
        {
            if (e.Model != null)
                this.FormatCellLog(sender, e);
        }

        private void FormatCellLog(object sender, BrightIdeasSoftware.FormatCellEventArgs e)
        {
            if (e.Column.Name == nameof(ClsLogItm.Detail))
            {
                ClsLogItm li = (ClsLogItm)e.Model;
                if (li.Level == LogLevel.Error || li.Level == LogLevel.Fatal)
                {
                    e.SubItem.ForeColor = Color.White;
                    e.SubItem.BackColor = Color.Red;
                }
                else if (li.Level == LogLevel.Warn)
                {
                    e.SubItem.ForeColor = Color.Red;
                    e.SubItem.BackColor = ((FastObjectListView)sender).BackColor;
                }
                else if (li.Level == LogLevel.Trace || li.Level == LogLevel.Debug)
                {
                    e.SubItem.ForeColor = Color.Gray;
                }
                else if (!string.IsNullOrEmpty(li.Color))
                {
                    e.SubItem.ForeColor = Color.FromName(li.Color);
                }
                else
                {
                    e.SubItem.ForeColor = Color.White;
                }
            }
            else
            {
                e.SubItem.ForeColor = Color.DarkGray;
            }
        }

        private void mnu_highlight_CheckStateChanged(object sender, EventArgs e)
        {
            this.filter_CheckStateChanged(sender, e);

        }

        private void filter_CheckStateChanged(object sender, EventArgs e)
        {

            if (IsLoading)
                return;

            ToolStripMenuItem currentItem = (ToolStripMenuItem)sender;
            ToolStripDropDownButton parentItem = (ToolStripDropDownButton)currentItem.OwnerItem;
            if (currentItem.Checked)
            {
                foreach (ToolStripMenuItem sibling in parentItem.DropDownItems)
                {
                    if (sibling != currentItem)
                    {
                        sibling.Checked = false;
                    }
                }

                if (!this.mnu_Filter.Checked || !this.mnu_Highlight.Checked)
                    this.mnu_Filter.Checked = true;

                AppSettings.Settings.log_mnu_Filter = this.mnu_Filter.Checked;
                AppSettings.Settings.log_mnu_Highlight = this.mnu_Highlight.Checked;

                if (!IsLoading && Global.IsRegexPatternValid(this.ToolStripComboBoxSearch.Text))
                {
                    bool Filter = false;
                    if (this.mnu_Filter.Checked && !this.mnu_Highlight.Checked)
                        Filter = true;
                    else
                        Filter = false;

                    Global_GUI.FilterFOLV(this.folv_log, this.ToolStripComboBoxSearch.Text, Filter);

                }
            }

        }

        private void Log_Filter_CheckStateChanged(object sender, EventArgs e)
        {

            if (IsLoading)
                return;

            ToolStripMenuItem currentItem = (ToolStripMenuItem)sender;
            ToolStripMenuItem parentItem = (ToolStripMenuItem)currentItem.OwnerItem;
            if (currentItem.Checked)
            {
                //uncheck everything else
                foreach (ToolStripMenuItem sibling in parentItem.DropDownItems)
                {
                    if (sibling != currentItem)
                    {
                        sibling.Checked = false;
                    }
                }

                if (!IsLoading)
                {
                    AppSettings.Settings.LogLevel = currentItem.Text;

                    LogMan.UpdateNLog(LogLevel.FromString(AppSettings.Settings.LogLevel), AppSettings.Settings.LogFileName, AppSettings.Settings.MaxLogFileSize, AppSettings.Settings.MaxLogFileAgeDays, AppSettings.Settings.MaxGUILogItems);
                }

                Log($"Debug: Logging level changed to '{currentItem.Text}'");
            }

        }
        private void mnu_Filter_CheckStateChanged(object sender, EventArgs e)
        {
            this.filter_CheckStateChanged(sender, e);
        }

        private void ToolStripComboBoxSearch_Leave(object sender, EventArgs e)
        {
        }

        private void ToolStripComboBoxSearch_TextChanged(object sender, EventArgs e)
        {
            if (IsLoading)
                return;

            if (!this.tmr.Enabled)
            {
                this.tmr.Enabled = true;
                this.tmr.Start();
            }

            this.TimeSinceType = DateTime.Now;

        }

        private void mnu_Filter_Click(object sender, EventArgs e)
        {

        }

        private void openToolStripButton_Click(object sender, EventArgs e)
        {
            OpenLogFile();
        }

        private void OpenLogFile()
        {
            if (System.IO.File.Exists(LogMan.GetCurrentLogFileName()))
            {
                ShellLauncher.Open(LogMan.GetCurrentLogFileName());
                this.lbl_errors.Text = "";
            }
            else
            {
                MessageBox.Show("log missing");
            }
        }

        private void mnu_log_filter_off_Click(object sender, EventArgs e)
        {

        }

        private void mnu_log_filter_off_CheckStateChanged(object sender, EventArgs e)
        {
            this.Log_Filter_CheckStateChanged(sender, e);
        }

        private void mnu_log_filter_fatal_CheckStateChanged(object sender, EventArgs e)
        {
            this.Log_Filter_CheckStateChanged(sender, e);

        }

        private void mnu_log_filter_error_CheckStateChanged(object sender, EventArgs e)
        {
            this.Log_Filter_CheckStateChanged(sender, e);

        }

        private void mnu_log_filter_warn_CheckStateChanged(object sender, EventArgs e)
        {
            this.Log_Filter_CheckStateChanged(sender, e);

        }

        private void mnu_log_filter_info_CheckStateChanged(object sender, EventArgs e)
        {
            this.Log_Filter_CheckStateChanged(sender, e);

        }

        private void mnu_log_filter_debug_CheckStateChanged(object sender, EventArgs e)
        {
            this.Log_Filter_CheckStateChanged(sender, e);

        }

        private void mnu_log_filter_trace_CheckStateChanged(object sender, EventArgs e)
        {
            this.Log_Filter_CheckStateChanged(sender, e);

        }

        private void clearRecentErrorsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            LogMan.ErrorCount = 0;
        }

        private void toolStripButtonPauseLog_Click(object sender, EventArgs e)
        {
            this.StartPauseLog();
        }

        private void StartPauseLog()
        {
            if (IsLoading)
                return;

            if (!this.toolStripButtonPauseLog.Checked)
            {
                this.LogUpdateListTimer.Enabled = true;
                this.LogUpdateListTimer.Start();
                Log("Started auto log refresh");
            }
            else
            {
                this.LogUpdateListTimer.Stop();
                this.LogUpdateListTimer.Enabled = false;
                Log("Stopped auto log refresh");
            }
        }

        private async void toolStripButtonReload_ClickAsync(object sender, EventArgs e)
        {
            this.ReloadLog();
        }

        private async void ReloadLog()
        {
            if (IsLoading)
                return;

            using var cw = new Global_GUI.CursorWait();
            this.chk_filterErrors.Checked = false;
            this.chk_filterErrorsAll.Checked = false;
            LogMan.Clear();
            this.folv_log.ClearObjects();
            this.folv_log.ModelFilter = null;
            await LogMan.LoadLogFileAsync(LogMan.GetCurrentLogFileName(), true, false);
            Log($"Loaded {LogMan.Values.Count} lines in {LogMan.LastLoadTimeMS}ms from {LogMan.GetCurrentLogFileName()}.");
            await this.UpdateLogAddedRemovedAsync(true);
            this.toolStripButtonPauseLog.Checked = false;

        }

        private void toolStripComboBoxFiles_Click(object sender, EventArgs e)
        {

        }

        private void toolStripComboBoxFiles_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private async void toolStripButtonLoad_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {

                string LastFile = Global.GetRegSetting("LastLoadedLogFile", LogMan.GetCurrentLogFileName());

                ofd.InitialDirectory = Path.GetDirectoryName(LastFile);
                ofd.FileName = LastFile;
                ofd.Title = "Browse for AITOOL Log Files";
                ofd.CheckFileExists = true;
                ofd.CheckPathExists = true;
                ofd.DefaultExt = "log";
                ofd.Filter = "LOG Files (AITOOL.[*.log;AITOOL.[*.zip)|AITOOL.[*.log;AITOOL.[*.zip";
                ofd.FilterIndex = 1;
                ofd.RestoreDirectory = true;

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    using var cw = new Global_GUI.CursorWait();
                    this.toolStripButtonPauseLog.Checked = true;
                    this.chk_filterErrors.Checked = false;
                    Global.SaveRegSetting("LastLoadedLogFile", ofd.FileName);
                    LogMan.Clear();
                    this.folv_log.ClearObjects();
                    this.folv_log.ModelFilter = null;
                    await LogMan.LoadLogFileAsync(ofd.FileName, true, false);
                    Log($"Loaded {LogMan.Values.Count} lines in {LogMan.LastLoadTimeMS}ms from {ofd.FileName}.");
                    this.UpdateLogAddedRemovedAsync(true);
                }


            }

        }

        private void chk_filterErrors_Click_1(object sender, EventArgs e)
        {
            this.FilterLogErrors();
        }

        private async void chk_filterErrorsAll_Click(object sender, EventArgs e)
        {
            if (IsLoading)
                return;

            if (!this.chk_filterErrorsAll.Checked)
            {
                this.ReloadLog();
                return;
            }

            try
            {

                this.toolStripButtonPauseLog.Checked = true; //pause for a bit, and stay paused if results found

                this.folv_log.ClearObjects();
                this.folv_log.ModelFilter = null;
                this.folv_log.EmptyListMsg = "Searching...";

                this.StartPauseLog();

                using var cw = new Global_GUI.CursorWait();

                Stopwatch sw = new Stopwatch();

                List<ClsLogItm> found = new List<ClsLogItm>();

                //AITool.[2020-10-19].log
                //AITool.[2020-10-19.1].log.zip
                List<FileInfo> files = Global.GetFiles(AppSettings.Settings.LogFileName, "AITOOL.[*].LOG|AITOOL.[*].LOG.ZIP", SearchOption.TopDirectoryOnly, DateTime.Now.AddDays(-2), DateTime.Now.AddMinutes(1));

                //sort by date so newest files are searched first
                files = files.OrderByDescending((d) => d.LastWriteTime).ToList();

                int cur = 0;
                foreach (var fi in files)
                {
                    try
                    {

                        cur++;


                        //load into memory
                        Log($"Debug: Searching back 2 days - {cur} of {files.Count}: {fi.Name}...", "None", "None", "None");

                        this.UpdateProgressBar($"Searching {cur} of {files.Count}: {fi.Name}...", 1, 1, 1);

                        List<ClsLogItm> curlist = await LogMan.LoadLogFileAsync(fi.FullName, false, false);

                        this.UpdateProgressBar($"Searching {cur} of {files.Count}: {fi.Name}...", 1, 1, curlist.Count);


                        int fnd = 0;
                        int cnt = 0;
                        int CurListCount = curlist.Count;
                        int HalfList = CurListCount / 2;
                        long LastMS = sw.ElapsedMilliseconds;
                        foreach (var CLI in curlist)
                        {
                            cnt++;

                            //if (cnt == 1 || cnt == HalfList || cnt > (CurListCount - 5) || (sw.ElapsedMilliseconds - LastMS >= 500))
                            //{
                            //    Global.UpdateProgressBar($"Searching {cur} of {files.Count}: {fi.Name}...", cnt, 1, CurListCount);
                            //    LastMS = sw.ElapsedMilliseconds;
                            //}

                            if (CLI.Level == LogLevel.Error || CLI.Level == LogLevel.Warn || CLI.Level == LogLevel.Fatal)
                            {
                                fnd++;
                                found.Add(CLI);
                            }

                        }

                        Log($"Debug: ...Found {fnd} of {curlist.Count} lines that had an error for a total of {found.Count} lines in {fi.Name}...");

                    }
                    catch (Exception ex)
                    {

                        Log("Error: " + ex.Msg());
                    }

                }

                Log($"Found {found.Count} errors in {sw.ElapsedMilliseconds}ms");

                if (found.Count > 0)
                {
                    LogMan.Clear();
                    LogMan.AddRange(found);
                    this.UpdateLogAddedRemovedAsync(false);
                }
                else
                {
                    MessageBox.Show($"Could not find any error log entries in {files.Count} files.");
                    this.chk_filterErrorsAll.Checked = false;
                    this.toolStripButtonPauseLog.Checked = false; //start
                    this.StartPauseLog();
                }

            }
            catch (Exception ex)
            {

                Log("Error: " + ex.Msg());
            }
            finally
            {
                this.UpdateProgressBar($"", 0, 0, 0);
            }
        }
    }
}
