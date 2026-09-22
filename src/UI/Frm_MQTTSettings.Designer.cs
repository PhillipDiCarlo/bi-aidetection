namespace AITool
{
    partial class Frm_MQTTSettings
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
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.label6 = new System.Windows.Forms.Label();
            this.tb_Payload = new System.Windows.Forms.TextBox();
            this.tb_Topic = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.tb_LWTPayload = new System.Windows.Forms.TextBox();
            this.tb_Password = new System.Windows.Forms.TextBox();
            this.tb_ClientID = new System.Windows.Forms.TextBox();
            this.tb_LWTTopic = new System.Windows.Forms.TextBox();
            this.tb_Username = new System.Windows.Forms.TextBox();
            this.tb_ServerPort = new System.Windows.Forms.TextBox();
            this.cb_Retain = new System.Windows.Forms.CheckBox();
            this.cb_UseTLS = new System.Windows.Forms.CheckBox();
            this.label8 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.label9 = new System.Windows.Forms.Label();
            this.label7 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.btTest = new System.Windows.Forms.Button();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.cb_HomeAssistantDiscovery = new System.Windows.Forms.CheckBox();
            this.cb_HomeAssistantPublishImage = new System.Windows.Forms.CheckBox();
            this.label10 = new System.Windows.Forms.Label();
            this.tb_HomeAssistantDiscoveryPrefix = new System.Windows.Forms.TextBox();
            this.label11 = new System.Windows.Forms.Label();
            this.tb_HomeAssistantDeviceName = new System.Windows.Forms.TextBox();
            this.label12 = new System.Windows.Forms.Label();
            this.tb_HomeAssistantOffDelaySeconds = new System.Windows.Forms.TextBox();
            this.groupBox1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.groupBox3.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(431, 440);
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
            this.btnSave.Location = new System.Drawing.Point(353, 440);
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
            this.groupBox1.Controls.Add(this.groupBox2);
            this.groupBox1.Controls.Add(this.tb_LWTPayload);
            this.groupBox1.Controls.Add(this.tb_Password);
            this.groupBox1.Controls.Add(this.tb_ClientID);
            this.groupBox1.Controls.Add(this.tb_LWTTopic);
            this.groupBox1.Controls.Add(this.tb_Username);
            this.groupBox1.Controls.Add(this.tb_ServerPort);
            this.groupBox1.Controls.Add(this.cb_Retain);
            this.groupBox1.Controls.Add(this.cb_UseTLS);
            this.groupBox1.Controls.Add(this.label8);
            this.groupBox1.Controls.Add(this.label3);
            this.groupBox1.Controls.Add(this.label9);
            this.groupBox1.Controls.Add(this.label7);
            this.groupBox1.Controls.Add(this.label2);
            this.groupBox1.Controls.Add(this.label1);
            this.groupBox1.Location = new System.Drawing.Point(8, 12);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(493, 302);
            this.groupBox1.TabIndex = 6;
            this.groupBox1.TabStop = false;
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.label6);
            this.groupBox2.Controls.Add(this.tb_Payload);
            this.groupBox2.Controls.Add(this.tb_Topic);
            this.groupBox2.Controls.Add(this.label4);
            this.groupBox2.Controls.Add(this.label5);
            this.groupBox2.Location = new System.Drawing.Point(10, 168);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(471, 128);
            this.groupBox2.TabIndex = 5;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Testing";
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.ForeColor = System.Drawing.Color.DodgerBlue;
            this.label6.Location = new System.Drawing.Point(9, 19);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(424, 15);
            this.label6.TabIndex = 6;
            this.label6.Text = "Specify more than one topic/payload by using the PIPE | symbol between each.";
            // 
            // tb_Payload
            // 
            this.tb_Payload.Font = new System.Drawing.Font("Consolas", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.tb_Payload.Location = new System.Drawing.Point(69, 69);
            this.tb_Payload.Multiline = true;
            this.tb_Payload.Name = "tb_Payload";
            this.tb_Payload.Size = new System.Drawing.Size(395, 51);
            this.tb_Payload.TabIndex = 5;
            // 
            // tb_Topic
            // 
            this.tb_Topic.Location = new System.Drawing.Point(70, 40);
            this.tb_Topic.Name = "tb_Topic";
            this.tb_Topic.Size = new System.Drawing.Size(395, 23);
            this.tb_Topic.TabIndex = 4;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(23, 43);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(38, 15);
            this.label4.TabIndex = 0;
            this.label4.Text = "Topic:";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(9, 69);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(52, 15);
            this.label5.TabIndex = 0;
            this.label5.Text = "Payload:";
            // 
            // tb_LWTPayload
            // 
            this.tb_LWTPayload.Location = new System.Drawing.Point(311, 78);
            this.tb_LWTPayload.Name = "tb_LWTPayload";
            this.tb_LWTPayload.Size = new System.Drawing.Size(169, 23);
            this.tb_LWTPayload.TabIndex = 2;
            // 
            // tb_Password
            // 
            this.tb_Password.Location = new System.Drawing.Point(312, 50);
            this.tb_Password.Name = "tb_Password";
            this.tb_Password.Size = new System.Drawing.Size(169, 23);
            this.tb_Password.TabIndex = 2;
            // 
            // tb_ClientID
            // 
            this.tb_ClientID.Location = new System.Drawing.Point(78, 107);
            this.tb_ClientID.Name = "tb_ClientID";
            this.tb_ClientID.Size = new System.Drawing.Size(161, 23);
            this.tb_ClientID.TabIndex = 1;
            // 
            // tb_LWTTopic
            // 
            this.tb_LWTTopic.Location = new System.Drawing.Point(78, 79);
            this.tb_LWTTopic.Name = "tb_LWTTopic";
            this.tb_LWTTopic.Size = new System.Drawing.Size(161, 23);
            this.tb_LWTTopic.TabIndex = 1;
            // 
            // tb_Username
            // 
            this.tb_Username.Location = new System.Drawing.Point(79, 51);
            this.tb_Username.Name = "tb_Username";
            this.tb_Username.Size = new System.Drawing.Size(161, 23);
            this.tb_Username.TabIndex = 1;
            // 
            // tb_ServerPort
            // 
            this.tb_ServerPort.Location = new System.Drawing.Point(79, 22);
            this.tb_ServerPort.Name = "tb_ServerPort";
            this.tb_ServerPort.Size = new System.Drawing.Size(402, 23);
            this.tb_ServerPort.TabIndex = 0;
            // 
            // cb_Retain
            // 
            this.cb_Retain.AutoSize = true;
            this.cb_Retain.Location = new System.Drawing.Point(77, 143);
            this.cb_Retain.Name = "cb_Retain";
            this.cb_Retain.Size = new System.Drawing.Size(59, 19);
            this.cb_Retain.TabIndex = 3;
            this.cb_Retain.Text = "Retain";
            this.cb_Retain.UseVisualStyleBackColor = true;
            this.cb_Retain.CheckedChanged += new System.EventHandler(this.cb_UseTLS_CheckedChanged);
            // 
            // cb_UseTLS
            // 
            this.cb_UseTLS.AutoSize = true;
            this.cb_UseTLS.Location = new System.Drawing.Point(9, 143);
            this.cb_UseTLS.Name = "cb_UseTLS";
            this.cb_UseTLS.Size = new System.Drawing.Size(66, 19);
            this.cb_UseTLS.TabIndex = 3;
            this.cb_UseTLS.Text = "Use TLS";
            this.cb_UseTLS.UseVisualStyleBackColor = true;
            this.cb_UseTLS.CheckedChanged += new System.EventHandler(this.cb_UseTLS_CheckedChanged);
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(253, 82);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(52, 15);
            this.label8.TabIndex = 0;
            this.label8.Text = "Payload:";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(246, 54);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(60, 15);
            this.label3.TabIndex = 0;
            this.label3.Text = "Password:";
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(18, 110);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(55, 15);
            this.label9.TabIndex = 0;
            this.label9.Text = "Client ID:";
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(10, 82);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(63, 15);
            this.label7.TabIndex = 0;
            this.label7.Text = "LWT Topic:";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(10, 54);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(63, 15);
            this.label2.TabIndex = 0;
            this.label2.Text = "Username:";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(6, 25);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(67, 15);
            this.label1.TabIndex = 0;
            this.label1.Text = "Server:Port:";
            // 
            // btTest
            // 
            this.btTest.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btTest.Location = new System.Drawing.Point(276, 440);
            this.btTest.Name = "btTest";
            this.btTest.Size = new System.Drawing.Size(70, 30);
            this.btTest.TabIndex = 6;
            this.btTest.Text = "Test";
            this.btTest.UseVisualStyleBackColor = true;
            this.btTest.Click += new System.EventHandler(this.btTest_ClickAsync);
            // 
            // groupBox3
            // 
            this.groupBox3.Controls.Add(this.cb_HomeAssistantDiscovery);
            this.groupBox3.Controls.Add(this.cb_HomeAssistantPublishImage);
            this.groupBox3.Controls.Add(this.label10);
            this.groupBox3.Controls.Add(this.tb_HomeAssistantDiscoveryPrefix);
            this.groupBox3.Controls.Add(this.label11);
            this.groupBox3.Controls.Add(this.tb_HomeAssistantDeviceName);
            this.groupBox3.Controls.Add(this.label12);
            this.groupBox3.Controls.Add(this.tb_HomeAssistantOffDelaySeconds);
            this.groupBox3.Location = new System.Drawing.Point(8, 320);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Size = new System.Drawing.Size(493, 110);
            this.groupBox3.TabIndex = 9;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "Home Assistant";
            // 
            // cb_HomeAssistantDiscovery
            // 
            this.cb_HomeAssistantDiscovery.AutoSize = true;
            this.cb_HomeAssistantDiscovery.Location = new System.Drawing.Point(9, 22);
            this.cb_HomeAssistantDiscovery.Name = "cb_HomeAssistantDiscovery";
            this.cb_HomeAssistantDiscovery.Size = new System.Drawing.Size(180, 19);
            this.cb_HomeAssistantDiscovery.TabIndex = 0;
            this.cb_HomeAssistantDiscovery.Text = "Enable Home Assistant Discovery";
            this.cb_HomeAssistantDiscovery.UseVisualStyleBackColor = true;
            // 
            // cb_HomeAssistantPublishImage
            // 
            this.cb_HomeAssistantPublishImage.AutoSize = true;
            this.cb_HomeAssistantPublishImage.Location = new System.Drawing.Point(280, 22);
            this.cb_HomeAssistantPublishImage.Name = "cb_HomeAssistantPublishImage";
            this.cb_HomeAssistantPublishImage.Size = new System.Drawing.Size(97, 19);
            this.cb_HomeAssistantPublishImage.TabIndex = 1;
            this.cb_HomeAssistantPublishImage.Text = "Publish Image";
            this.cb_HomeAssistantPublishImage.UseVisualStyleBackColor = true;
            // 
            // label10
            // 
            this.label10.AutoSize = true;
            this.label10.Location = new System.Drawing.Point(9, 51);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(93, 15);
            this.label10.TabIndex = 0;
            this.label10.Text = "Discovery Prefix:";
            // 
            // tb_HomeAssistantDiscoveryPrefix
            // 
            this.tb_HomeAssistantDiscoveryPrefix.Location = new System.Drawing.Point(108, 48);
            this.tb_HomeAssistantDiscoveryPrefix.Name = "tb_HomeAssistantDiscoveryPrefix";
            this.tb_HomeAssistantDiscoveryPrefix.Size = new System.Drawing.Size(120, 23);
            this.tb_HomeAssistantDiscoveryPrefix.TabIndex = 2;
            // 
            // label11
            // 
            this.label11.AutoSize = true;
            this.label11.Location = new System.Drawing.Point(246, 51);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(84, 15);
            this.label11.TabIndex = 0;
            this.label11.Text = "Device Name:";
            // 
            // tb_HomeAssistantDeviceName
            // 
            this.tb_HomeAssistantDeviceName.Location = new System.Drawing.Point(336, 48);
            this.tb_HomeAssistantDeviceName.Name = "tb_HomeAssistantDeviceName";
            this.tb_HomeAssistantDeviceName.Size = new System.Drawing.Size(145, 23);
            this.tb_HomeAssistantDeviceName.TabIndex = 3;
            // 
            // label12
            // 
            this.label12.AutoSize = true;
            this.label12.Location = new System.Drawing.Point(9, 82);
            this.label12.Name = "label12";
            this.label12.Size = new System.Drawing.Size(120, 15);
            this.label12.TabIndex = 0;
            this.label12.Text = "Off Delay (seconds):";
            // 
            // tb_HomeAssistantOffDelaySeconds
            // 
            this.tb_HomeAssistantOffDelaySeconds.Location = new System.Drawing.Point(135, 79);
            this.tb_HomeAssistantOffDelaySeconds.Name = "tb_HomeAssistantOffDelaySeconds";
            this.tb_HomeAssistantOffDelaySeconds.Size = new System.Drawing.Size(60, 23);
            this.tb_HomeAssistantOffDelaySeconds.TabIndex = 4;
            // 
            // Frm_MQTTSettings
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Inherit;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(513, 484);
            this.Controls.Add(this.groupBox3);
            this.Controls.Add(this.btTest);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnSave);
            this.Font = new System.Drawing.Font("Segoe UI", 8.25F);
            this.Name = "Frm_MQTTSettings";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "MQTT Settings";
            this.Load += new System.EventHandler(this.Frm_MQTTSettings_Load);
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.groupBox3.ResumeLayout(false);
            this.groupBox3.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button btTest;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label5;
        public System.Windows.Forms.TextBox tb_Payload;
        public System.Windows.Forms.TextBox tb_Topic;
        public System.Windows.Forms.TextBox tb_Password;
        public System.Windows.Forms.TextBox tb_Username;
        public System.Windows.Forms.TextBox tb_ServerPort;
        public System.Windows.Forms.CheckBox cb_UseTLS;
        private System.Windows.Forms.Label label6;
        public System.Windows.Forms.TextBox tb_LWTPayload;
        public System.Windows.Forms.TextBox tb_LWTTopic;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.Label label7;
        public System.Windows.Forms.CheckBox cb_Retain;
        public System.Windows.Forms.TextBox tb_ClientID;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.GroupBox groupBox3;
        public System.Windows.Forms.CheckBox cb_HomeAssistantDiscovery;
        public System.Windows.Forms.CheckBox cb_HomeAssistantPublishImage;
        private System.Windows.Forms.Label label10;
        public System.Windows.Forms.TextBox tb_HomeAssistantDiscoveryPrefix;
        private System.Windows.Forms.Label label11;
        public System.Windows.Forms.TextBox tb_HomeAssistantDeviceName;
        private System.Windows.Forms.Label label12;
        public System.Windows.Forms.TextBox tb_HomeAssistantOffDelaySeconds;
    }
}