namespace AITool
{
    partial class Frm_WebDashboard
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnSave = new System.Windows.Forms.Button();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.lbl_LanWarning = new System.Windows.Forms.Label();
            this.cb_AllowLan = new System.Windows.Forms.CheckBox();
            this.cb_Enabled = new System.Windows.Forms.CheckBox();
            this.label1 = new System.Windows.Forms.Label();
            this.tb_Port = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.tb_Token = new System.Windows.Forms.TextBox();
            this.bt_Regenerate = new System.Windows.Forms.Button();
            this.bt_CopyUrl = new System.Windows.Forms.Button();
            this.bt_OpenInBrowser = new System.Windows.Forms.Button();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            //
            // btnCancel
            //
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(431, 288);
            this.btnCancel.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(70, 30);
            this.btnCancel.TabIndex = 8;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            //
            // btnSave
            //
            this.btnSave.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSave.Location = new System.Drawing.Point(353, 288);
            this.btnSave.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(70, 30);
            this.btnSave.TabIndex = 7;
            this.btnSave.Text = "Save";
            this.btnSave.UseVisualStyleBackColor = true;
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);
            //
            // groupBox1
            //
            this.groupBox1.Controls.Add(this.bt_OpenInBrowser);
            this.groupBox1.Controls.Add(this.bt_CopyUrl);
            this.groupBox1.Controls.Add(this.bt_Regenerate);
            this.groupBox1.Controls.Add(this.tb_Token);
            this.groupBox1.Controls.Add(this.label2);
            this.groupBox1.Controls.Add(this.tb_Port);
            this.groupBox1.Controls.Add(this.label1);
            this.groupBox1.Controls.Add(this.cb_Enabled);
            this.groupBox1.Controls.Add(this.cb_AllowLan);
            this.groupBox1.Controls.Add(this.lbl_LanWarning);
            this.groupBox1.Location = new System.Drawing.Point(8, 12);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(493, 268);
            this.groupBox1.TabIndex = 6;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Web Dashboard";
            //
            // cb_Enabled
            //
            this.cb_Enabled.AutoSize = true;
            this.cb_Enabled.Location = new System.Drawing.Point(12, 25);
            this.cb_Enabled.Name = "cb_Enabled";
            this.cb_Enabled.Size = new System.Drawing.Size(164, 19);
            this.cb_Enabled.TabIndex = 0;
            this.cb_Enabled.Text = "Enable web dashboard";
            this.cb_Enabled.UseVisualStyleBackColor = true;
            //
            // label1
            //
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(10, 56);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(34, 15);
            this.label1.TabIndex = 1;
            this.label1.Text = "Port:";
            //
            // tb_Port
            //
            this.tb_Port.Location = new System.Drawing.Point(79, 53);
            this.tb_Port.Name = "tb_Port";
            this.tb_Port.Size = new System.Drawing.Size(80, 23);
            this.tb_Port.TabIndex = 1;
            //
            // cb_AllowLan
            //
            this.cb_AllowLan.AutoSize = true;
            this.cb_AllowLan.Location = new System.Drawing.Point(12, 88);
            this.cb_AllowLan.Name = "cb_AllowLan";
            this.cb_AllowLan.Size = new System.Drawing.Size(360, 19);
            this.cb_AllowLan.TabIndex = 2;
            this.cb_AllowLan.Text = "Allow LAN access (otherwise only this PC can connect)";
            this.cb_AllowLan.UseVisualStyleBackColor = true;
            this.cb_AllowLan.CheckedChanged += new System.EventHandler(this.cb_AllowLan_CheckedChanged);
            //
            // lbl_LanWarning
            //
            this.lbl_LanWarning.ForeColor = System.Drawing.Color.Firebrick;
            this.lbl_LanWarning.Location = new System.Drawing.Point(12, 110);
            this.lbl_LanWarning.Name = "lbl_LanWarning";
            this.lbl_LanWarning.Size = new System.Drawing.Size(469, 36);
            this.lbl_LanWarning.TabIndex = 3;
            this.lbl_LanWarning.Text = "Warning: LAN mode exposes the dashboard to anyone on your local network who kno" +
    "ws (or guesses) the port. Only enable it on a network you trust, and keep the " +
    "token secret.";
            this.lbl_LanWarning.Visible = false;
            //
            // label2
            //
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(10, 157);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(74, 15);
            this.label2.TabIndex = 4;
            this.label2.Text = "Access token:";
            //
            // tb_Token
            //
            this.tb_Token.Font = new System.Drawing.Font("Consolas", 8.25F);
            this.tb_Token.Location = new System.Drawing.Point(12, 175);
            this.tb_Token.Name = "tb_Token";
            this.tb_Token.ReadOnly = true;
            this.tb_Token.Size = new System.Drawing.Size(378, 23);
            this.tb_Token.TabIndex = 5;
            //
            // bt_Regenerate
            //
            this.bt_Regenerate.Location = new System.Drawing.Point(396, 174);
            this.bt_Regenerate.Name = "bt_Regenerate";
            this.bt_Regenerate.Size = new System.Drawing.Size(85, 25);
            this.bt_Regenerate.TabIndex = 6;
            this.bt_Regenerate.Text = "Regenerate";
            this.bt_Regenerate.UseVisualStyleBackColor = true;
            this.bt_Regenerate.Click += new System.EventHandler(this.bt_Regenerate_Click);
            //
            // bt_CopyUrl
            //
            this.bt_CopyUrl.Location = new System.Drawing.Point(12, 212);
            this.bt_CopyUrl.Name = "bt_CopyUrl";
            this.bt_CopyUrl.Size = new System.Drawing.Size(110, 28);
            this.bt_CopyUrl.TabIndex = 7;
            this.bt_CopyUrl.Text = "Copy URL";
            this.bt_CopyUrl.UseVisualStyleBackColor = true;
            this.bt_CopyUrl.Click += new System.EventHandler(this.bt_CopyUrl_Click);
            //
            // bt_OpenInBrowser
            //
            this.bt_OpenInBrowser.Location = new System.Drawing.Point(128, 212);
            this.bt_OpenInBrowser.Name = "bt_OpenInBrowser";
            this.bt_OpenInBrowser.Size = new System.Drawing.Size(140, 28);
            this.bt_OpenInBrowser.TabIndex = 8;
            this.bt_OpenInBrowser.Text = "Open in browser";
            this.bt_OpenInBrowser.UseVisualStyleBackColor = true;
            this.bt_OpenInBrowser.Click += new System.EventHandler(this.bt_OpenInBrowser_Click);
            //
            // Frm_WebDashboard
            //
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Inherit;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(513, 332);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnSave);
            this.Font = new System.Drawing.Font("Segoe UI", 8.25F);
            this.Name = "Frm_WebDashboard";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Web Dashboard Settings";
            this.Load += new System.EventHandler(this.Frm_WebDashboard_Load);
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Frm_WebDashboard_FormClosing);
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.CheckBox cb_Enabled;
        private System.Windows.Forms.Label label1;
        public System.Windows.Forms.TextBox tb_Port;
        private System.Windows.Forms.CheckBox cb_AllowLan;
        private System.Windows.Forms.Label lbl_LanWarning;
        private System.Windows.Forms.Label label2;
        public System.Windows.Forms.TextBox tb_Token;
        private System.Windows.Forms.Button bt_Regenerate;
        private System.Windows.Forms.Button bt_CopyUrl;
        private System.Windows.Forms.Button bt_OpenInBrowser;
    }
}
