using System;
using System.Windows.Forms;

namespace UNP.Core.Helpers {

    public class ListMessageBox : Form {

        private string[][] options = null;
        private bool multiple = false;

        public string selected = "";
        public string[] selectedMultiple = new string[0];

        private ListBox lstOptions;
        private Button btnOK;
        private Button btnCancel;

        public ListMessageBox(string title, string[][] options, bool multiple) {
            this.options = options;
            this.multiple = multiple;

            this.lstOptions = new ListBox();
            this.btnOK = new Button();
            this.btnCancel = new Button();
            this.SuspendLayout();

            this.lstOptions.FormattingEnabled = true;
            this.lstOptions.SelectionMode = (multiple ? SelectionMode.MultiSimple : SelectionMode.One);
            this.lstOptions.ItemHeight = 16;
            this.lstOptions.Location = new System.Drawing.Point(4, 5);
            this.lstOptions.Name = "lstOptions";
            this.lstOptions.Size = new System.Drawing.Size(287, 212);
            this.lstOptions.TabIndex = 0;
            this.lstOptions.DoubleClick += new System.EventHandler(delegate (object sender, EventArgs e) {
                btnOK.PerformClick();
            });
            for (int i = 0; i < options.Length; i++) {
                this.lstOptions.Items.Add(options[i][0]);
            }


            this.btnOK.Location = new System.Drawing.Point(12, 228);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(101, 26);
            this.btnOK.TabIndex = 1;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(delegate (object sender, EventArgs e) {

                // check if option(s) are selected
                if (lstOptions.SelectedItems.Count == 0) {
                    MessageBox.Show("Select an option from the list to continue...", "Select an option", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    return;
                }
                
                // set the results
                if (multiple) {
                    selectedMultiple = new string[lstOptions.SelectedItems.Count];
                    for (int i = 0; i < lstOptions.SelectedItems.Count; i++) {
                        selectedMultiple[i] = options[lstOptions.SelectedIndices[i]][1];
                        //selectedMultiple[i] = (string)lstOptions.SelectedItems[i];
                    }
                } else {
                    selected = options[lstOptions.SelectedIndices[0]][1];
                    //selected = (string)lstOptions.SelectedItems[0];
                }

                // return ok
                this.DialogResult = DialogResult.OK;
            });


            this.btnCancel.Location = new System.Drawing.Point(179, 228);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(101, 26);
            this.btnCancel.TabIndex = 2;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(delegate (object sender, EventArgs e) {
                this.DialogResult = DialogResult.Cancel;

            });


            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(292, 262);
            this.Controls.Add(this.lstOptions);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.btnCancel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Margin = new System.Windows.Forms.Padding(4);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "DlgList";
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = title;
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(delegate (object sender, FormClosingEventArgs e) {
                if (e.CloseReason == CloseReason.UserClosing) {
                    this.DialogResult = DialogResult.Cancel;
                }
            });

            this.ResumeLayout(false);

        }

        public static string ShowSingle(string title, string[] options) {
            string[][] newOptions = new string[options.Length][];
            for (int i = 0; i < newOptions.Length; i++) {
                newOptions[i] = new string[2];
                newOptions[i][1] = options[i];
                newOptions[i][2] = options[i];
            }

            return ShowSingle(title, newOptions);
        }

        public static string[] ShowMultiple(string title, string[] options) {
            string[][] newOptions = new string[options.Length][];
            for (int i = 0; i < newOptions.Length; i++) {
                newOptions[i] = new string[2];
                newOptions[i][1] = options[i];
                newOptions[i][2] = options[i];
            }

            return ShowMultiple(title, newOptions);
        }

        public static string ShowSingle(string title, string[][] options) {
            using (var form = new ListMessageBox(title, options, false)) {
                if (form.ShowDialog() == DialogResult.OK) return form.selected;
                return "";
            }
        }

        public static string[] ShowMultiple(string title, string[][] options) {
            using (var form = new ListMessageBox(title, options, true)) {
                if (form.ShowDialog() == DialogResult.OK) return form.selectedMultiple;
                return new string[0];
            }
        }

    }

}
