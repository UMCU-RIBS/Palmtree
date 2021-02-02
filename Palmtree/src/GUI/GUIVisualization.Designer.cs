namespace Palmtree.GUI {

    partial class GUIVisualization {
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
            this.components = new System.ComponentModel.Container();
            this.tmrUpdate = new System.Windows.Forms.Timer(this.components);
            this.SuspendLayout();
            // 
            // tmrUpdate
            // 
            this.tmrUpdate.Enabled = true;
            this.tmrUpdate.Interval = 1000;
            this.tmrUpdate.Tick += new System.EventHandler(this.tmrUpdate_Tick);
            // 
            // GUIVisualization
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.ClientSize = new System.Drawing.Size(616, 547);
            this.Font = new System.Drawing.Font("Microsoft Sans Serif", 14F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel, ((byte)(204)));
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Margin = new System.Windows.Forms.Padding(2);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "GUIVisualization";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Visualization";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.GUIVisualization_FormClosing);
            this.Load += new System.EventHandler(this.GUIVisualization_Load);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Timer tmrUpdate;



    }
}