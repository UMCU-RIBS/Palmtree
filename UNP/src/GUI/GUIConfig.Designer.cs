namespace UNP.GUI {

    partial class GUIConfig {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing) {
            if (disposing && (components != null)) {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent() {
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnSave = new System.Windows.Forms.Button();
            this.btnLoadPrmFile = new System.Windows.Forms.Button();
            this.btnSavePrmFile = new System.Windows.Forms.Button();
            this.tabControl = new UNP.GUI.NoBorderTabControl();
            this.SuspendLayout();
            // 
            // btnCancel
            // 
            this.btnCancel.Font = new System.Drawing.Font("Microsoft Sans Serif", 14F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel, ((byte)(0)));
            this.btnCancel.Location = new System.Drawing.Point(775, 599);
            this.btnCancel.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(175, 37);
            this.btnCancel.TabIndex = 1;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // btnSave
            // 
            this.btnSave.Font = new System.Drawing.Font("Microsoft Sans Serif", 14F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel, ((byte)(0)));
            this.btnSave.Location = new System.Drawing.Point(593, 599);
            this.btnSave.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(175, 37);
            this.btnSave.TabIndex = 2;
            this.btnSave.Text = "Save";
            this.btnSave.UseVisualStyleBackColor = true;
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);
            // 
            // btnLoadPrmFile
            // 
            this.btnLoadPrmFile.Font = new System.Drawing.Font("Microsoft Sans Serif", 14F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel, ((byte)(0)));
            this.btnLoadPrmFile.Location = new System.Drawing.Point(15, 599);
            this.btnLoadPrmFile.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.btnLoadPrmFile.Name = "btnLoadPrmFile";
            this.btnLoadPrmFile.Size = new System.Drawing.Size(175, 37);
            this.btnLoadPrmFile.TabIndex = 3;
            this.btnLoadPrmFile.Text = "Load .prm file";
            this.btnLoadPrmFile.UseVisualStyleBackColor = true;
            this.btnLoadPrmFile.Click += new System.EventHandler(this.btnLoadPrmFile_Click);
            // 
            // btnSavePrmFile
            // 
            this.btnSavePrmFile.Font = new System.Drawing.Font("Microsoft Sans Serif", 14F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel, ((byte)(0)));
            this.btnSavePrmFile.Location = new System.Drawing.Point(195, 599);
            this.btnSavePrmFile.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.btnSavePrmFile.Name = "btnSavePrmFile";
            this.btnSavePrmFile.Size = new System.Drawing.Size(175, 37);
            this.btnSavePrmFile.TabIndex = 4;
            this.btnSavePrmFile.Text = "Save .prm file";
            this.btnSavePrmFile.UseVisualStyleBackColor = true;
            this.btnSavePrmFile.Click += new System.EventHandler(this.btnSavePrmFile_Click);
            // 
            // tabControl
            // 
            this.tabControl.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel, ((byte)(204)));
            this.tabControl.Location = new System.Drawing.Point(-3, 5);
            this.tabControl.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.tabControl.Name = "tabControl";
            this.tabControl.SelectedIndex = 0;
            this.tabControl.Size = new System.Drawing.Size(965, 587);
            this.tabControl.TabIndex = 0;
            this.tabControl.TabStop = false;
            // 
            // GUIConfig
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.ClientSize = new System.Drawing.Size(960, 645);
            this.Controls.Add(this.btnSavePrmFile);
            this.Controls.Add(this.btnLoadPrmFile);
            this.Controls.Add(this.btnSave);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.tabControl);
            this.Font = new System.Drawing.Font("Microsoft Sans Serif", 14F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel, ((byte)(204)));
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "GUIConfig";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Edit configuration";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.GUIConfig_FormClosing);
            this.Load += new System.EventHandler(this.GUIConfig_Load);
            this.ResumeLayout(false);

        }

        #endregion

        private NoBorderTabControl tabControl;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.Button btnLoadPrmFile;
        private System.Windows.Forms.Button btnSavePrmFile;

    }
}