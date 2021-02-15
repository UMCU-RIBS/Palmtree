using System;
using System.Windows.Forms;

namespace Palmtree.GUI {

    partial class GUIMain {

        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing) {
            try {
                if (disposing && (components != null)) {
                    components.Dispose();
                }
                base.Dispose(disposing);
            } catch (Exception) {   }
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.btnStop = new System.Windows.Forms.Button();
            this.btnStart = new System.Windows.Forms.Button();
            this.btnSetConfig = new System.Windows.Forms.Button();
            this.grpConsole = new System.Windows.Forms.GroupBox();
            this.txtConsole = new System.Windows.Forms.RichTextBox();
            this.btnEditConfig = new System.Windows.Forms.Button();
            this.btnVisualization = new System.Windows.Forms.Button();
            this.btnMore = new System.Windows.Forms.Button();
            this.lblEaster = new System.Windows.Forms.Label();
            this.grpConsole.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnStop
            // 
            this.btnStop.Enabled = false;
            this.btnStop.Font = new System.Drawing.Font("Microsoft Sans Serif", 14F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel, ((byte)(204)));
            this.btnStop.Location = new System.Drawing.Point(492, 18);
            this.btnStop.Margin = new System.Windows.Forms.Padding(4);
            this.btnStop.Name = "btnStop";
            this.btnStop.Size = new System.Drawing.Size(120, 44);
            this.btnStop.TabIndex = 17;
            this.btnStop.Text = "Stop";
            this.btnStop.UseVisualStyleBackColor = true;
            this.btnStop.Click += new System.EventHandler(this.btnStop_Click);
            // 
            // btnStart
            // 
            this.btnStart.Enabled = false;
            this.btnStart.Font = new System.Drawing.Font("Microsoft Sans Serif", 14F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel, ((byte)(204)));
            this.btnStart.Location = new System.Drawing.Point(373, 18);
            this.btnStart.Margin = new System.Windows.Forms.Padding(4);
            this.btnStart.Name = "btnStart";
            this.btnStart.Size = new System.Drawing.Size(120, 44);
            this.btnStart.TabIndex = 16;
            this.btnStart.Text = "Start";
            this.btnStart.UseVisualStyleBackColor = true;
            this.btnStart.Click += new System.EventHandler(this.btnStart_Click);
            // 
            // btnSetConfig
            // 
            this.btnSetConfig.Enabled = false;
            this.btnSetConfig.Font = new System.Drawing.Font("Microsoft Sans Serif", 14F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel, ((byte)(204)));
            this.btnSetConfig.Location = new System.Drawing.Point(157, 18);
            this.btnSetConfig.Margin = new System.Windows.Forms.Padding(4);
            this.btnSetConfig.Name = "btnSetConfig";
            this.btnSetConfig.Size = new System.Drawing.Size(181, 44);
            this.btnSetConfig.TabIndex = 15;
            this.btnSetConfig.Text = "Set Config and Init";
            this.btnSetConfig.UseVisualStyleBackColor = true;
            this.btnSetConfig.Click += new System.EventHandler(this.btnSetConfig_Click);
            // 
            // grpConsole
            // 
            this.grpConsole.Controls.Add(this.txtConsole);
            this.grpConsole.Font = new System.Drawing.Font("Microsoft Sans Serif", 14F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel, ((byte)(204)));
            this.grpConsole.Location = new System.Drawing.Point(16, 79);
            this.grpConsole.Margin = new System.Windows.Forms.Padding(4);
            this.grpConsole.Name = "grpConsole";
            this.grpConsole.Padding = new System.Windows.Forms.Padding(4);
            this.grpConsole.Size = new System.Drawing.Size(853, 495);
            this.grpConsole.TabIndex = 14;
            this.grpConsole.TabStop = false;
            this.grpConsole.Text = "Output";
            // 
            // txtConsole
            // 
            this.txtConsole.BackColor = System.Drawing.Color.White;
            this.txtConsole.Font = new System.Drawing.Font("Microsoft Sans Serif", 13F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel, ((byte)(204)));
            this.txtConsole.Location = new System.Drawing.Point(17, 32);
            this.txtConsole.Margin = new System.Windows.Forms.Padding(4);
            this.txtConsole.Name = "txtConsole";
            this.txtConsole.ReadOnly = true;
            this.txtConsole.Size = new System.Drawing.Size(817, 447);
            this.txtConsole.TabIndex = 0;
            this.txtConsole.Text = "";
            this.txtConsole.WordWrap = false;
            // 
            // btnEditConfig
            // 
            this.btnEditConfig.Enabled = false;
            this.btnEditConfig.Font = new System.Drawing.Font("Microsoft Sans Serif", 14F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel, ((byte)(204)));
            this.btnEditConfig.Location = new System.Drawing.Point(16, 18);
            this.btnEditConfig.Margin = new System.Windows.Forms.Padding(4);
            this.btnEditConfig.Name = "btnEditConfig";
            this.btnEditConfig.Size = new System.Drawing.Size(142, 44);
            this.btnEditConfig.TabIndex = 18;
            this.btnEditConfig.Text = "Edit Config";
            this.btnEditConfig.UseVisualStyleBackColor = true;
            this.btnEditConfig.Click += new System.EventHandler(this.btnEditConfig_Click);
            // 
            // btnVisualization
            // 
            this.btnVisualization.Enabled = false;
            this.btnVisualization.Font = new System.Drawing.Font("Microsoft Sans Serif", 14F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel, ((byte)(204)));
            this.btnVisualization.Location = new System.Drawing.Point(642, 18);
            this.btnVisualization.Margin = new System.Windows.Forms.Padding(4);
            this.btnVisualization.Name = "btnVisualization";
            this.btnVisualization.Size = new System.Drawing.Size(115, 44);
            this.btnVisualization.TabIndex = 19;
            this.btnVisualization.Text = "Visualization";
            this.btnVisualization.UseVisualStyleBackColor = true;
            this.btnVisualization.Visible = false;
            this.btnVisualization.Click += new System.EventHandler(this.btnVisualization_Click);
            // 
            // btnMore
            // 
            this.btnMore.Enabled = false;
            this.btnMore.Font = new System.Drawing.Font("Microsoft Sans Serif", 14F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel, ((byte)(204)));
            this.btnMore.Location = new System.Drawing.Point(756, 18);
            this.btnMore.Margin = new System.Windows.Forms.Padding(4);
            this.btnMore.Name = "btnMore";
            this.btnMore.Size = new System.Drawing.Size(113, 44);
            this.btnMore.TabIndex = 20;
            this.btnMore.Text = "More...";
            this.btnMore.UseVisualStyleBackColor = true;
            this.btnMore.Click += new System.EventHandler(this.btnMore_Click);
            // 
            // lblEaster
            // 
            this.lblEaster.AutoSize = true;
            this.lblEaster.Font = new System.Drawing.Font("Microsoft Sans Serif", 7F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel, ((byte)(204)));
            this.lblEaster.Location = new System.Drawing.Point(-100, -100);
            this.lblEaster.Name = "lblEaster";
            this.lblEaster.Size = new System.Drawing.Size(222, 9);
            this.lblEaster.TabIndex = 21;
            this.lblEaster.Text = "In the beginning were the words, and the words made the world...";
            // 
            // GUIMain
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.ClientSize = new System.Drawing.Size(888, 588);
            this.Controls.Add(this.lblEaster);
            this.Controls.Add(this.btnMore);
            this.Controls.Add(this.btnSetConfig);
            this.Controls.Add(this.btnVisualization);
            this.Controls.Add(this.btnEditConfig);
            this.Controls.Add(this.btnStop);
            this.Controls.Add(this.btnStart);
            this.Controls.Add(this.grpConsole);
            this.Font = new System.Drawing.Font("Microsoft Sans Serif", 14F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel, ((byte)(204)));
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Margin = new System.Windows.Forms.Padding(4);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "GUIMain";
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Palmtree";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.GUI_FormClosing);
            this.Load += new System.EventHandler(this.GUI_Load);
            this.grpConsole.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private Button btnStop;
        private Button btnStart;
        private Button btnSetConfig;
        private GroupBox grpConsole;
        private RichTextBox txtConsole;
        private Button btnEditConfig;
        private Button btnVisualization;
        private Button btnMore;
        private Label lblEaster;
    }
}