namespace UNP.GUI {
    partial class GUIMore {
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
            this.btnPrintParamInfo = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // btnPrintParamInfo
            // 
            this.btnPrintParamInfo.Location = new System.Drawing.Point(27, 22);
            this.btnPrintParamInfo.Name = "btnPrintParamInfo";
            this.btnPrintParamInfo.Size = new System.Drawing.Size(305, 36);
            this.btnPrintParamInfo.TabIndex = 0;
            this.btnPrintParamInfo.Text = "Print parameters info in console";
            this.btnPrintParamInfo.UseVisualStyleBackColor = true;
            this.btnPrintParamInfo.Click += new System.EventHandler(this.btnPrintParamInfo_Click);
            // 
            // GUIMore
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.ClientSize = new System.Drawing.Size(358, 383);
            this.Controls.Add(this.btnPrintParamInfo);
            this.Font = new System.Drawing.Font("Microsoft Sans Serif", 14F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel, ((byte)(204)));
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Margin = new System.Windows.Forms.Padding(2);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "GUIMore";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "More...";
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button btnPrintParamInfo;
    }
}