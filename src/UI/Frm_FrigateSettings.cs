using System;
using System.Diagnostics;
using System.Windows.Forms;

using static AITool.AITOOL;

namespace AITool
{
    public partial class Frm_FrigateSettings : Form
    {
        public Frm_FrigateSettings()
        {
            this.InitializeComponent();
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void linkLabel_FrigateUrl_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            try
            {
                string url = this.tb_Url.Text.Trim();
                if (url.IsNotEmpty())
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Log($"Debug: FrigateSource: Could not open '{this.tb_Url.Text}': {ex.Msg()}");
            }
        }

        private async void btTest_ClickAsync(object sender, EventArgs e)
        {
            this.btTest.Enabled = false;
            this.btnSave.Enabled = false;
            this.btnCancel.Enabled = false;

            try
            {
                using (Global_GUI.CursorWait cw = new Global_GUI.CursorWait())
                {
                    FrigateSource test = new FrigateSource();
                    test.GetFrigateUrl = () => this.tb_Url.Text.Trim();
                    test.GetApiKey = () => this.tb_ApiKey.Text.Trim();

                    try
                    {
                        string version = await test.GetVersionAsync();
                        MessageBox.Show($"Success! Frigate version: {version}", "Frigate Test", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed: {ex.Msg()}", "Frigate Test", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
            finally
            {
                this.btTest.Enabled = true;
                this.btnSave.Enabled = true;
                this.btnCancel.Enabled = true;
            }
        }
    }
}
