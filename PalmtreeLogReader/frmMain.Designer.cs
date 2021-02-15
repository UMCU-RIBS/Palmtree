namespace PalmtreeLogReader {
    partial class frmMain {
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
            this.grpInput = new System.Windows.Forms.GroupBox();
            this.btnBrowse = new System.Windows.Forms.Button();
            this.txtInputFile = new System.Windows.Forms.TextBox();
            this.txtOutput = new System.Windows.Forms.TextBox();
            this.btnRead = new System.Windows.Forms.Button();
            this.btnReadBCI2000 = new System.Windows.Forms.Button();
            this.btnConvertBCIToPalmtree = new System.Windows.Forms.Button();
            this.grpInput.SuspendLayout();
            this.SuspendLayout();
            // 
            // grpInput
            // 
            this.grpInput.Controls.Add(this.btnBrowse);
            this.grpInput.Controls.Add(this.txtInputFile);
            this.grpInput.Location = new System.Drawing.Point(12, 12);
            this.grpInput.Name = "grpInput";
            this.grpInput.Size = new System.Drawing.Size(671, 66);
            this.grpInput.TabIndex = 1;
            this.grpInput.TabStop = false;
            this.grpInput.Text = "Input";
            // 
            // btnBrowse
            // 
            this.btnBrowse.Location = new System.Drawing.Point(571, 27);
            this.btnBrowse.Name = "btnBrowse";
            this.btnBrowse.Size = new System.Drawing.Size(90, 23);
            this.btnBrowse.TabIndex = 2;
            this.btnBrowse.Text = "Browse...";
            this.btnBrowse.UseVisualStyleBackColor = true;
            this.btnBrowse.Click += new System.EventHandler(this.btnBrowse_Click);
            // 
            // txtInputFile
            // 
            this.txtInputFile.Location = new System.Drawing.Point(12, 28);
            this.txtInputFile.Name = "txtInputFile";
            this.txtInputFile.Size = new System.Drawing.Size(552, 22);
            this.txtInputFile.TabIndex = 1;
            // 
            // txtOutput
            // 
            this.txtOutput.BackColor = System.Drawing.Color.White;
            this.txtOutput.Location = new System.Drawing.Point(12, 147);
            this.txtOutput.Multiline = true;
            this.txtOutput.Name = "txtOutput";
            this.txtOutput.ReadOnly = true;
            this.txtOutput.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.txtOutput.Size = new System.Drawing.Size(671, 412);
            this.txtOutput.TabIndex = 2;
            this.txtOutput.WordWrap = false;
            // 
            // btnRead
            // 
            this.btnRead.Location = new System.Drawing.Point(24, 95);
            this.btnRead.Name = "btnRead";
            this.btnRead.Size = new System.Drawing.Size(110, 37);
            this.btnRead.TabIndex = 3;
            this.btnRead.Text = "Read Palmtree";
            this.btnRead.UseVisualStyleBackColor = true;
            this.btnRead.Click += new System.EventHandler(this.btnRead_Click);
            // 
            // btnReadBCI2000
            // 
            this.btnReadBCI2000.Location = new System.Drawing.Point(153, 95);
            this.btnReadBCI2000.Name = "btnReadBCI2000";
            this.btnReadBCI2000.Size = new System.Drawing.Size(137, 37);
            this.btnReadBCI2000.TabIndex = 4;
            this.btnReadBCI2000.Text = "Read BCI2000";
            this.btnReadBCI2000.UseVisualStyleBackColor = true;
            this.btnReadBCI2000.Click += new System.EventHandler(this.btnReadBCI2000_Click);
            // 
            // btnConvertBCIToPalmtree
            // 
            this.btnConvertBCIToPalmtree.Location = new System.Drawing.Point(308, 95);
            this.btnConvertBCIToPalmtree.Name = "btnConvertBCIToPalmtree";
            this.btnConvertBCIToPalmtree.Size = new System.Drawing.Size(219, 37);
            this.btnConvertBCIToPalmtree.TabIndex = 5;
            this.btnConvertBCIToPalmtree.Text = "Convert BCI2000 -> Palmtree";
            this.btnConvertBCIToPalmtree.UseVisualStyleBackColor = true;
            this.btnConvertBCIToPalmtree.Click += new System.EventHandler(this.btnConvertBCIToPalmtree_Click);
            // 
            // frmMain
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(695, 571);
            this.Controls.Add(this.btnConvertBCIToPalmtree);
            this.Controls.Add(this.btnReadBCI2000);
            this.Controls.Add(this.btnRead);
            this.Controls.Add(this.txtOutput);
            this.Controls.Add(this.grpInput);
            this.MaximizeBox = false;
            this.Name = "frmMain";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Log Reader";
            this.grpInput.ResumeLayout(false);
            this.grpInput.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.GroupBox grpInput;
        private System.Windows.Forms.Button btnBrowse;
        private System.Windows.Forms.TextBox txtInputFile;
        private System.Windows.Forms.TextBox txtOutput;
        private System.Windows.Forms.Button btnRead;
        private System.Windows.Forms.Button btnReadBCI2000;
        private System.Windows.Forms.Button btnConvertBCIToPalmtree;
    }
}

