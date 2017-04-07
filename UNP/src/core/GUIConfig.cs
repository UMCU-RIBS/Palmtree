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
        private const int labelWidth = 230;
        private const int itemTopPadding = 10;
        private const int itemBottomPadding = 10;

        public GUIConfig() {
            InitializeComponent();

            // suspend the tabcontrol layout
            tabControl.SuspendLayout();

            // loop through each paramset
            int counter = 0;
            foreach (KeyValuePair<String, Parameters> entry in paramSets) {
                
                // create a new tab for the paramset and suspend the layout
                TabPage newTab = new TabPage();
                newTab.SuspendLayout();

                // add the tab to the control
                tabControl.Controls.Add(newTab);

                // setup the tab
                newTab.BackColor = SystemColors.Control;
                newTab.BorderStyle = BorderStyle.None;
                newTab.Name = "tab" + entry.Key;
                newTab.Padding = new Padding(0);
                newTab.Size = new Size(884, 640);
                newTab.TabIndex = counter;
                newTab.Text = entry.Key;

                // add a panel to the tab (this will allow scrolling)
                Panel newPanel = new Panel();
                newPanel.SuspendLayout();
                newPanel.BackColor = SystemColors.Control;
                newPanel.BorderStyle = BorderStyle.None;
                newPanel.Location = new Point(0, 0);
                newPanel.Size = new Size(newTab.Width, newTab.Height);
                newPanel.Name = "pnl" + entry.Key;
                newPanel.AutoScroll = true;
                newTab.Controls.Add(newPanel); 

                // TODO: check grouping etc

                // loop through the parameters in the 
                List<iParam> parameters = entry.Value.getParameters();
                int y = 20;
                for (int i = 0; i < parameters.Count; i++) {
                    addConfigItemToControl(newPanel, parameters[i], ref y);
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

        private void addConfigItemToControl(Control panel, iParam param, ref int y) {

            // create and add a label
            Label newLbl = new Label();
            newLbl.Name = "lbl" + panel.Name + param.Name;
            newLbl.Location = new Point(10, y + itemTopPadding);
            newLbl.Size = new System.Drawing.Size(labelWidth, 20);
            newLbl.Text = param.Name;
            newLbl.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            newLbl.Parent = panel;
            newLbl.TextAlign = ContentAlignment.TopRight;
            panel.Controls.Add(newLbl);

            int itemHeight = 0;
            if (param is ParamBool) {

                // create and add a checkbox
                CheckBox newChk = new CheckBox();
                newChk.Name = "chk" + panel.Name + param.Name;
                newChk.Location = new Point(labelWidth + 20, y + itemTopPadding - 2);
                newChk.Text = "";
                newChk.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
                newChk.Checked = ((ParamBool)param).Value;
                panel.Controls.Add(newChk);
                itemHeight = 20;

            } else if (param is ParamInt || param is ParamDouble) {
                
                // create and add a textbox
                TextBox newTxt = new TextBox();
                newTxt.Name = "txt" + panel.Name + param.Name;
                newTxt.Location = new Point(labelWidth + 20, y + itemTopPadding - 2);
                newTxt.Size = new System.Drawing.Size(200, 20);
                if (param is ParamInt)      newTxt.Text = ((ParamInt)param).Value.ToString();
                if (param is ParamDouble)   newTxt.Text = ((ParamDouble)param).Value.ToString();
                newTxt.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
                panel.Controls.Add(newTxt);
                itemHeight = 20;

            } else if (param is ParamColor) {

            } else if (param is ParamBoolArr) {

            } else if (param is ParamIntArr) {

                // check if it has a limit range of possibilities
                //param.Options

            } else if (param is ParamDoubleArr) {

            } else if (param is ParamBoolMat) {

            } else if (param is ParamIntMat) {

            } else if (param is ParamDoubleMat) {
            
            }


            y = y + itemTopPadding + 20 + itemBottomPadding;
            
        }

        private void GUIConfig_Load(object sender, EventArgs e) {
            
        }

        private void btnCancel_Click(object sender, EventArgs e) {
            this.Close();
        }

    }
    
    class NoBorderTabControl : TabControl {
        private const int TCM_ADJUSTRECT = 0x1328;
        private const int WM_PAINT = 0xF;

        protected override void WndProc(ref Message m) {

            //Hide the tab headers at run-time
            if (m.Msg == TCM_ADJUSTRECT) {

                RECT rect = (RECT)(m.GetLParam(typeof(RECT)));
                rect.Left = this.Left - this.Margin.Left;
                rect.Right = this.Right + this.Margin.Right;
                //rect.Top = this.Top - this.Margin.Top;
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
