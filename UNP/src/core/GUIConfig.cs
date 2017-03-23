using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace UNP {

    public partial class GUIConfig : Form {

        private static Dictionary<String, Parameters> paramSets = ParameterManager.getParameterSets();


        public GUIConfig() {
            InitializeComponent();

            // suspend the tabcontrol layout
            tabControl.SuspendLayout();

            // loop through each paramset
            int counter = 0;
            foreach (KeyValuePair<String, Parameters> entry in paramSets) {
                
                // create a new tab for the paramset and suspend the layout
                TabPage newTab = new System.Windows.Forms.TabPage();
                newTab.SuspendLayout();

                // add the tab to the control
                tabControl.Controls.Add(newTab);

                // setup the tab
                newTab.BackColor = System.Drawing.SystemColors.Control;
                newTab.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
                newTab.Name = "tab" + entry.Key;
                newTab.Padding = new System.Windows.Forms.Padding(3);
                newTab.Size = new System.Drawing.Size(884, 640);
                newTab.TabIndex = counter;
                newTab.Text = entry.Key;

                // add a panel to the tab (this will allow scrolling) and suspend the layout
                Panel newPanel = new Panel();
                newPanel.SuspendLayout();
                newPanel.BackColor = System.Drawing.SystemColors.Control;
                newPanel.BorderStyle = System.Windows.Forms.BorderStyle.None;
                newPanel.Location = new Point(0, 0);
                newPanel.Size = new Size(newTab.Width, newTab.Height);
                newPanel.Name = "pnl" + entry.Key;
                newPanel.AutoScroll = true;
                
                newTab.Controls.Add(newPanel); 

                // loop through the parameters in the 

                if (counter < 2) {
                    // temp add test button
                    Button btnCancel2 = new Button();
                    btnCancel2.Location = new System.Drawing.Point(20, 800);
                    //this.btnCancel2.Name = "btnCancel";
                    btnCancel2.Size = new System.Drawing.Size(175, 37);
                    //this.btnCancel2.TabIndex = 1;
                    btnCancel2.Text = "Cancel";
                    btnCancel2.UseVisualStyleBackColor = true;
                    //this.btnCancel2.Click += new System.EventHandler(this.btnCancel_Click);
                    newPanel.Controls.Add(btnCancel2);
                }

                newPanel.Dock = DockStyle.Fill;
                //newPanel.HorizontalScroll.

                // resume the layout of the panel
                newPanel.ResumeLayout(false);

                // resume the layout of the tab
                newTab.ResumeLayout(false);
                
                // next parameter set
                counter++;
            }

            // resume the tabcontrol layout
            tabControl.ResumeLayout(false);

        }

        private void GUIConfig_Load(object sender, EventArgs e) {
            
        }

        private void btnCancel_Click(object sender, EventArgs e) {
            this.Close();
        }

    }

    class NoBorderTabControl : TabControl {
        private const int TCM_ADJUSTRECT = 0x1328;

        protected override void WndProc(ref Message m) {

            //Hide the tab headers at run-time
            if (m.Msg == TCM_ADJUSTRECT) {

                RECT rect = (RECT)(m.GetLParam(typeof(RECT)));
                rect.Left = this.Left - this.Margin.Left;
                rect.Right = this.Right + this.Margin.Right;

                rect.Top = this.Top - this.Margin.Top;
                rect.Bottom = this.Bottom + this.Margin.Bottom;
                Marshal.StructureToPtr(rect, m.LParam, true);

            }
            
            // call the base class implementation
            base.WndProc(ref m);
        }

        private struct RECT {
            public int Left, Top, Right, Bottom;
        }
    }

}
