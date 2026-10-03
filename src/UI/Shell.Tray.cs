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

        //open from tray
        private void notifyIcon_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            ShowForm();
        }

        private void btnPause_Click(object sender, EventArgs e)
        {
            using (Frm_Pause frm = new Frm_Pause())
            {

                //Camera cam = AITOOL.GetCamera(((Camera)this.FOLV_Cameras.SelectedObjects[0]).Name);
                //frm.CurrentCam = cam;
                frm.ShowDialog(this);
            }
        }

        private void bt_CheckUpdates_Click(object sender, EventArgs e)
        {
            using (Frm_UpdateCheck frm = new Frm_UpdateCheck())
            {
                if (frm.ShowDialog(this) == DialogResult.OK)
                {
                    CloseImmediately = true;
                    Application.Exit();

                }
            }
        }

        private void notifyIcon_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                this.ShowForm();
            }
            else
            {
                this.notifyIcon.ContextMenuStrip.Show();
            }
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void pauseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (Frm_Pause frm = new Frm_Pause())
            {

                //Camera cam = AITOOL.GetCamera(((Camera)this.FOLV_Cameras.SelectedObjects[0]).Name);
                //frm.CurrentCam = cam;
                frm.ShowDialog(this);
            }
        }

        private void pauseAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            double pausetime = 0;
            foreach (var cam in AppSettings.Settings.CameraList)
            {
                pausetime = cam.PauseMinutes;
                if (!cam.Paused)
                    cam.Pause();
            }
            MessageBox.Show($"Paused all cameras for {pausetime} minutes");
        }

        private void resumeAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (var cam in AppSettings.Settings.CameraList)
            {
                if (cam.Paused)
                    cam.Resume();
            }

            MessageBox.Show("Resumed all cameras.");
        }

        private void webDashboardToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (Frm_WebDashboard frm = new Frm_WebDashboard())
            {
                frm.ShowDialog(this);
            }
        }
    }
}
