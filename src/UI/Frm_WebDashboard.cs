using System;
using System.Diagnostics;
using System.Windows.Forms;

using AITool.WebDashboard;

namespace AITool
{
    public partial class Frm_WebDashboard:Form
    {
        public Frm_WebDashboard()
        {
            InitializeComponent();
        }

        private void Frm_WebDashboard_Load(object sender, EventArgs e)
        {
            Global_GUI.RestoreWindowState(this);

            this.cb_Enabled.Checked = AppSettings.Settings.WebDashboardEnabled;
            this.tb_Port.Text = AppSettings.Settings.WebDashboardPort.ToString();
            this.cb_AllowLan.Checked = AppSettings.Settings.WebDashboardAllowLan;
            this.tb_Token.Text = AppSettings.Settings.WebDashboardToken;

            this.UpdateLanWarning();
        }

        private void UpdateLanWarning()
        {
            this.lbl_LanWarning.Visible = this.cb_AllowLan.Checked;
        }

        private string GetDashboardUrl()
        {
            string host = this.cb_AllowLan.Checked ? Environment.MachineName : "127.0.0.1";
            string port = this.tb_Port.Text.Trim();
            string token = this.tb_Token.Text.Trim();

            return $"http://{host}:{port}/?token={Uri.EscapeDataString(token)}";
        }

        private void cb_AllowLan_CheckedChanged(object sender, EventArgs e)
        {
            this.UpdateLanWarning();
        }

        private void bt_Regenerate_Click(object sender, EventArgs e)
        {
            this.tb_Token.Text = WebDashboardServer.GenerateToken();
        }

        private void bt_CopyUrl_Click(object sender, EventArgs e)
        {
            try
            {
                Clipboard.SetText(this.GetDashboardUrl());
            }
            catch (Exception ex)
            {
                AITOOL.Log($"Error: Could not copy dashboard URL to clipboard: {ex.Msg()}");
            }
        }

        private void bt_OpenInBrowser_Click(object sender, EventArgs e)
        {
            try
            {
                //Process.Start(url) throws on .NET Core unless UseShellExecute is set
                Process.Start(new ProcessStartInfo(this.GetDashboardUrl()) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                AITOOL.Log($"Error: Could not open dashboard in browser: {ex.Msg()}");
                MessageBox.Show($"Could not open the dashboard: {ex.Msg()}", "AI Tool", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            int port;
            if (!int.TryParse(this.tb_Port.Text.Trim(), out port) || port <= 0 || port > 65535)
            {
                MessageBox.Show("Please enter a valid port number (1-65535).", "AI Tool", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            AppSettings.Settings.WebDashboardEnabled = this.cb_Enabled.Checked;
            AppSettings.Settings.WebDashboardPort = port;
            AppSettings.Settings.WebDashboardAllowLan = this.cb_AllowLan.Checked;

            //auto-generate on first enable if the user didn't already press 'Regenerate'
            if (AppSettings.Settings.WebDashboardEnabled && this.tb_Token.Text.IsEmpty())
                this.tb_Token.Text = WebDashboardServer.GenerateToken();

            AppSettings.Settings.WebDashboardToken = this.tb_Token.Text.Trim();

            _ = AppSettings.SaveAsync(true);

            //apply immediately so the user doesn't have to restart AITool to pick up port/LAN/enabled changes
            WebDashboardServer.Stop();

            if (AppSettings.Settings.WebDashboardEnabled)
                WebDashboardServer.Start();

            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void Frm_WebDashboard_FormClosing(object sender, FormClosingEventArgs e)
        {
            Global_GUI.SaveWindowState(this);
        }
    }
}
