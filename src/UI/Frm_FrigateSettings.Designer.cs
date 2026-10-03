namespace AITool
{
    partial class Frm_FrigateSettings
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
            this.toolTip1 = new System.Windows.Forms.ToolTip();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnSave = new System.Windows.Forms.Button();
            this.btTest = new System.Windows.Forms.Button();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.cb_Enabled = new System.Windows.Forms.CheckBox();
            this.label1 = new System.Windows.Forms.Label();
            this.tb_Url = new System.Windows.Forms.TextBox();
            this.linkLabel_FrigateUrl = new System.Windows.Forms.LinkLabel();
            this.label2 = new System.Windows.Forms.Label();
            this.tb_TopicPrefix = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.tb_Cameras = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.tb_Labels = new System.Windows.Forms.TextBox();
            this.label5 = new System.Windows.Forms.Label();
            this.tb_SnapshotFolder = new System.Windows.Forms.TextBox();
            this.label6 = new System.Windows.Forms.Label();
            this.tb_ApiKey = new System.Windows.Forms.TextBox();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            //
            // btnCancel
            //
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(431, 272);
            this.btnCancel.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(70, 30);
            this.btnCancel.TabIndex = 3;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            //
            // btnSave
            //
            this.btnSave.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSave.Location = new System.Drawing.Point(353, 272);
            this.btnSave.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(70, 30);
            this.btnSave.TabIndex = 2;
            this.btnSave.Text = "Save";
            this.btnSave.UseVisualStyleBackColor = true;
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);
            //
            // btTest
            //
            this.btTest.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btTest.Location = new System.Drawing.Point(276, 272);
            this.btTest.Name = "btTest";
            this.btTest.Size = new System.Drawing.Size(70, 30);
            this.btTest.TabIndex = 1;
            this.btTest.Text = "Test";
            this.toolTip1.SetToolTip(this.btTest, "Fetches <Frigate URL>/api/version to confirm the server is reachable.");
            this.btTest.UseVisualStyleBackColor = true;
            this.btTest.Click += new System.EventHandler(this.btTest_ClickAsync);
            //
            // groupBox1
            //
            this.groupBox1.Controls.Add(this.cb_Enabled);
            this.groupBox1.Controls.Add(this.label1);
            this.groupBox1.Controls.Add(this.tb_Url);
            this.groupBox1.Controls.Add(this.linkLabel_FrigateUrl);
            this.groupBox1.Controls.Add(this.label2);
            this.groupBox1.Controls.Add(this.tb_TopicPrefix);
            this.groupBox1.Controls.Add(this.label3);
            this.groupBox1.Controls.Add(this.tb_Cameras);
            this.groupBox1.Controls.Add(this.label4);
            this.groupBox1.Controls.Add(this.tb_Labels);
            this.groupBox1.Controls.Add(this.label5);
            this.groupBox1.Controls.Add(this.tb_SnapshotFolder);
            this.groupBox1.Controls.Add(this.label6);
            this.groupBox1.Controls.Add(this.tb_ApiKey);
            this.groupBox1.Location = new System.Drawing.Point(8, 12);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(493, 250);
            this.groupBox1.TabIndex = 0;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Frigate (frigate.video)";
            //
            // cb_Enabled
            //
            this.cb_Enabled.AutoSize = true;
            this.cb_Enabled.ForeColor = System.Drawing.Color.DodgerBlue;
            this.cb_Enabled.Location = new System.Drawing.Point(9, 22);
            this.cb_Enabled.Name = "cb_Enabled";
            this.cb_Enabled.Size = new System.Drawing.Size(130, 19);
            this.cb_Enabled.TabIndex = 0;
            this.cb_Enabled.Text = "Use Frigate as a source";
            this.cb_Enabled.UseVisualStyleBackColor = true;
            //
            // label1
            //
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(6, 54);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(67, 15);
            this.label1.TabIndex = 0;
            this.label1.Text = "Frigate URL:";
            //
            // tb_Url
            //
            this.tb_Url.Location = new System.Drawing.Point(110, 51);
            this.tb_Url.Name = "tb_Url";
            this.tb_Url.Size = new System.Drawing.Size(320, 23);
            this.tb_Url.TabIndex = 1;
            this.toolTip1.SetToolTip(this.tb_Url, "Frigate's base URL, e.g. http://frigate:5000 - unauthenticated API port by default.");
            //
            // linkLabel_FrigateUrl
            //
            this.linkLabel_FrigateUrl.AutoSize = true;
            this.linkLabel_FrigateUrl.Location = new System.Drawing.Point(436, 54);
            this.linkLabel_FrigateUrl.Name = "linkLabel_FrigateUrl";
            this.linkLabel_FrigateUrl.Size = new System.Drawing.Size(32, 15);
            this.linkLabel_FrigateUrl.TabIndex = 2;
            this.linkLabel_FrigateUrl.TabStop = true;
            this.linkLabel_FrigateUrl.Text = "Open";
            this.linkLabel_FrigateUrl.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabel_FrigateUrl_LinkClicked);
            //
            // label2
            //
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(6, 83);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(98, 15);
            this.label2.TabIndex = 0;
            this.label2.Text = "MQTT Topic Prefix:";
            //
            // tb_TopicPrefix
            //
            this.tb_TopicPrefix.Location = new System.Drawing.Point(110, 80);
            this.tb_TopicPrefix.Name = "tb_TopicPrefix";
            this.tb_TopicPrefix.Size = new System.Drawing.Size(150, 23);
            this.tb_TopicPrefix.TabIndex = 3;
            this.toolTip1.SetToolTip(this.tb_TopicPrefix, "Matches Frigate's mqtt.topic_prefix config option (default \'frigate\'). Subscribes to <prefix>/events.");
            //
            // label3
            //
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(6, 112);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(145, 15);
            this.label3.TabIndex = 0;
            this.label3.Text = "Cameras (blank = all):";
            //
            // tb_Cameras
            //
            this.tb_Cameras.Location = new System.Drawing.Point(155, 109);
            this.tb_Cameras.Name = "tb_Cameras";
            this.tb_Cameras.Size = new System.Drawing.Size(275, 23);
            this.tb_Cameras.TabIndex = 4;
            this.toolTip1.SetToolTip(this.tb_Cameras, "Comma separated list of Frigate camera names to accept (as configured in Frigate\'s config.yml), blank = all cameras.");
            //
            // label4
            //
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(6, 141);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(123, 15);
            this.label4.TabIndex = 0;
            this.label4.Text = "Labels (blank = all):";
            //
            // tb_Labels
            //
            this.tb_Labels.Location = new System.Drawing.Point(155, 138);
            this.tb_Labels.Name = "tb_Labels";
            this.tb_Labels.Size = new System.Drawing.Size(275, 23);
            this.tb_Labels.TabIndex = 5;
            this.toolTip1.SetToolTip(this.tb_Labels, "Comma separated list of Frigate object labels to accept (e.g. person, car), blank = all labels.");
            //
            // label5
            //
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(6, 170);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(187, 15);
            this.label5.TabIndex = 0;
            this.label5.Text = "Snapshot folder (blank = default):";
            //
            // tb_SnapshotFolder
            //
            this.tb_SnapshotFolder.Location = new System.Drawing.Point(199, 167);
            this.tb_SnapshotFolder.Name = "tb_SnapshotFolder";
            this.tb_SnapshotFolder.Size = new System.Drawing.Size(231, 23);
            this.tb_SnapshotFolder.TabIndex = 6;
            this.toolTip1.SetToolTip(this.tb_SnapshotFolder, "Where downloaded snapshots are saved before being queued. Blank uses %TEMP%\\_AITOOL\\frigate.");
            //
            // label6
            //
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(6, 199);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(132, 15);
            this.label6.TabIndex = 0;
            this.label6.Text = "API Key (optional):";
            //
            // tb_ApiKey
            //
            this.tb_ApiKey.Location = new System.Drawing.Point(155, 196);
            this.tb_ApiKey.Name = "tb_ApiKey";
            this.tb_ApiKey.PasswordChar = '*';
            this.tb_ApiKey.Size = new System.Drawing.Size(275, 23);
            this.tb_ApiKey.TabIndex = 7;
            this.toolTip1.SetToolTip(this.tb_ApiKey, "Only needed if you have put an auth proxy in front of Frigate that requires a Bearer token. Sent as 'Authorization: Bearer <key>'.");
            //
            // Frm_FrigateSettings
            //
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Inherit;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(513, 316);
            this.Controls.Add(this.btTest);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnSave);
            this.Font = new System.Drawing.Font("Segoe UI", 8.25F);
            this.Name = "Frm_FrigateSettings";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Frigate Settings";
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ToolTip toolTip1;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.Button btTest;
        private System.Windows.Forms.GroupBox groupBox1;
        public System.Windows.Forms.CheckBox cb_Enabled;
        private System.Windows.Forms.Label label1;
        public System.Windows.Forms.TextBox tb_Url;
        private System.Windows.Forms.LinkLabel linkLabel_FrigateUrl;
        private System.Windows.Forms.Label label2;
        public System.Windows.Forms.TextBox tb_TopicPrefix;
        private System.Windows.Forms.Label label3;
        public System.Windows.Forms.TextBox tb_Cameras;
        private System.Windows.Forms.Label label4;
        public System.Windows.Forms.TextBox tb_Labels;
        private System.Windows.Forms.Label label5;
        public System.Windows.Forms.TextBox tb_SnapshotFolder;
        private System.Windows.Forms.Label label6;
        public System.Windows.Forms.TextBox tb_ApiKey;
    }
}
