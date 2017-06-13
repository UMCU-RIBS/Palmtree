using NLog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using UNP.Core.Helpers;
using UNP.Core.Params;

namespace UNP.GUI {

    public partial class GUIConfig : Form {

        public static CultureInfo NumberCulture = CultureInfo.CreateSpecificCulture("en-US");
        private static Logger logger = LogManager.GetLogger("GUIConfig");

        private struct ParamControl {
            public iParam param;
            public Control control;
            public TabPage tab;
            public Control additionalControl1;
            public Control additionalControl2;
            
            public ParamControl(iParam param, Control control, TabPage tab) {
                this.param = param;
                this.control = control;
                this.tab = tab;
                this.additionalControl1 = null;
                this.additionalControl2 = null;
            }
        
            public ParamControl(iParam param, Control control, TabPage tab, Control additionalControl1, Control additionalControl2) {
                this.param = param;
                this.control = control;
                this.tab = tab;
                this.additionalControl1 = additionalControl1;
                this.additionalControl2 = additionalControl2;
            }
        
        }

        private static Dictionary<string, Parameters> paramSets = ParameterManager.getParameterSets();
        private const int labelWidth = 250;
        private const int itemTopPadding = 10;
        private const int itemBottomPadding = 10;

        private List<ParamControl> paramControls = new List<ParamControl>(0);

        public GUIConfig() {

            // initialize components
            InitializeComponent();

            // suspend the tabcontrol layout
            tabControl.SuspendLayout();

            // loop through each paramset
            int counter = 0;
            foreach (KeyValuePair<string, Parameters> entry in paramSets) {
                
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

                // loop through the parameters
                List<iParam> parameters = entry.Value.getParameters();
                int y = 20;
                for (int i = 0; i < parameters.Count; i++) {
                    addConfigItemToControl(newTab, newPanel, parameters[i], ref y);
                }

                // check if the y is higher than the panel height (if there is scrolling; the VerticalScroll.Visible property does not work at this point)
                if (y > newPanel.Height) {

                    // add an empty label at the end to create dummy space
                    Label newLbl = new Label();
                    newLbl.Name = "lbl" + newPanel.Name + "EndDummy";
                    newLbl.Location = new Point(10, y);
                    newLbl.Size = new System.Drawing.Size(labelWidth, 20);
                    newLbl.Text = "";
                    newLbl.Parent = newPanel;
                    newPanel.Controls.Add(newLbl);

                }

                newPanel.Dock = DockStyle.Fill;

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
        
        private void addConfigItemToControl(TabPage tab, Control panel, iParam param, ref int y) {

            // create and add a label
            Label newLbl = new Label();
            newLbl.Name = "lbl" + panel.Name + param.Name;
            newLbl.Location = new Point(10, y + itemTopPadding);
            newLbl.Size = new System.Drawing.Size(labelWidth, 20);
            newLbl.Text = param.Name;
            newLbl.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            newLbl.Parent = panel;
            newLbl.TextAlign = ContentAlignment.TopRight;
            ToolTip tt = new ToolTip();
            tt.AutoPopDelay = 15000;
            tt.InitialDelay = 200;
            tt.SetToolTip(newLbl, param.Desc);
            panel.Controls.Add(newLbl);

            int itemHeight = 0;
            if (param is ParamBool) {

                // create and add a checkbox
                CheckBox newChk = new CheckBox();
                newChk.Name = "chk" + panel.Name + param.Name;
                newChk.Location = new Point(labelWidth + 20, y + itemTopPadding - 2);
                newChk.Text = "";
                newChk.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
                panel.Controls.Add(newChk);
                paramControls.Add(new ParamControl(param, newChk, tab));
                itemHeight = 20;

            } else if (param is ParamInt || param is ParamDouble) {

                // check if there are emulated options
                if (param.Options.Length == 0) {
                    // not emulated options

                    // create and add a textbox
                    TextBox newTxt = new TextBox();
                    newTxt.Name = "txt" + panel.Name + param.Name;
                    newTxt.Location = new Point(labelWidth + 20, y + itemTopPadding - 2);
                    newTxt.Size = new System.Drawing.Size(260, 20);
                    newTxt.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
                    panel.Controls.Add(newTxt);
                    paramControls.Add(new ParamControl(param, newTxt, tab));
                    itemHeight = 20;

                } else {
                    // emulated options

                    // create and add a combobox
                    ComboBox newCmb = new ComboBox();
                    newCmb.Name = "txt" + panel.Name + param.Name;
                    newCmb.Location = new Point(labelWidth + 20, y + itemTopPadding - 3);
                    newCmb.Size = new System.Drawing.Size(320, 20);
                    newCmb.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
                    for (int i = 0; i < param.Options.Length; i++)  newCmb.Items.Add(param.Options[i]);
                    panel.Controls.Add(newCmb);
                    paramControls.Add(new ParamControl(param, newCmb, tab));
                    itemHeight = 22;

                }
                
                
            } else if (param is ParamColor) {

                PictureBox newPic = new PictureBox();
                newPic.BorderStyle = BorderStyle.FixedSingle;
                newPic.Name = "pic" + panel.Name + param.Name;
                newPic.Location = new Point(labelWidth + 20, y + itemTopPadding - 1);
                newPic.Size = new System.Drawing.Size(40, 20);
                newPic.Click += (sender, e) => {
                    ColorDialog clrDialog = new ColorDialog();
                    clrDialog.AllowFullOpen = true;
                    clrDialog.ShowHelp = false;
                    clrDialog.Color = newPic.BackColor;
                    if (clrDialog.ShowDialog() == DialogResult.OK) {
                        newPic.BackColor = clrDialog.Color;
                    }
                };
                panel.Controls.Add(newPic);
                paramControls.Add(new ParamControl(param, newPic, tab));
                itemHeight = 20;

            } else if (param is ParamBoolArr || param is ParamIntArr || param is ParamDoubleArr || param is ParamString) {

                // create and add a textbox
                TextBox newTxt = new TextBox();
                newTxt.Name = "txt" + panel.Name + param.Name;
                newTxt.Location = new Point(labelWidth + 20, y + itemTopPadding - 2);
                newTxt.Size = new System.Drawing.Size(340, 20);
                newTxt.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
                panel.Controls.Add(newTxt);
                paramControls.Add(new ParamControl(param, newTxt, tab));
                itemHeight = 20;

            } else if (param is ParamBoolMat || param is ParamIntMat || param is ParamDoubleMat || param is ParamStringMat) {

                // add the data grid
                DataGridView newGrid = new DataGridView();
                newGrid.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
                newGrid.Name = "grd" + panel.Name + param.Name;
                newGrid.Location = new Point(labelWidth + 20, y + itemTopPadding - 2);
                newGrid.Size = new System.Drawing.Size(420, 144);
                newGrid.AllowUserToAddRows = false;
                newGrid.AllowUserToDeleteRows = false;
                newGrid.AllowUserToResizeRows = false;
                newGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
                newGrid.RowHeadersVisible = false;
                newGrid.DefaultCellStyle.FormatProvider = NumberCulture;
                newGrid.AllowUserToOrderColumns = (param.Options.Length == 0);
                panel.Controls.Add(newGrid);

                // 
                Label newLblRows = new Label();
                Label newLblColumns = new Label();
                NumericUpDown newRows = new System.Windows.Forms.NumericUpDown();
                NumericUpDown newColumns = new System.Windows.Forms.NumericUpDown();

                // rows
                newLblRows.Name = "lbl" + panel.Name + param.Name + "Rows";
                newLblRows.Location = new Point(labelWidth + 20, newGrid.Location.Y + newGrid.Size.Height + 7);
                newLblRows.Size = new System.Drawing.Size(50, 20);
                newLblRows.Text = "Rows:";
                newLblRows.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
                newLblRows.Parent = panel;
                newLblRows.TextAlign = ContentAlignment.TopRight;
                newRows.Name = "num" + panel.Name + param.Name + "Rows";
                newRows.Location = new Point(labelWidth + 75, newGrid.Location.Y + newGrid.Size.Height + 5);
                newRows.Size = new System.Drawing.Size(50, 20);
                newRows.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
                newRows.ReadOnly = true;
                newRows.GotFocus += (sender, e) => { newRows.Enabled = false; newRows.Enabled = true; };
                newRows.MouseWheel += (sender, e) => { ((HandledMouseEventArgs)e).Handled = true; };
                newRows.KeyPress += (sender, e) => { e.Handled = true; };
                newRows.Cursor = Cursors.Arrow;
                newRows.BackColor = Color.White;
                newRows.ValueChanged += (sender, e) => { gridResize(newGrid, (int)newColumns.Value, (int)newRows.Value); };
                panel.Controls.Add(newLblRows);
                panel.Controls.Add(newRows);

                // colums
                newLblColumns.Name = "lbl" + panel.Name + param.Name + "Colums";
                newLblColumns.Location = new Point(labelWidth + 140, newGrid.Location.Y + newGrid.Size.Height + 7);
                newLblColumns.Size = new System.Drawing.Size(80, 20);
                newLblColumns.Text = "Columns:";
                newLblColumns.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
                newLblColumns.Parent = panel;
                newLblColumns.TextAlign = ContentAlignment.TopRight;
                newLblColumns.Visible = (param.Options.Length == 0);
                newColumns.Name = "num" + panel.Name + param.Name + "Columns";
                newColumns.Location = new Point(labelWidth + 225, newGrid.Location.Y + newGrid.Size.Height + 5);
                newColumns.Size = new System.Drawing.Size(50, 20);
                newColumns.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
                newColumns.ReadOnly = true;
                newColumns.GotFocus += (sender, e) => { newColumns.Enabled = false; newColumns.Enabled = true; };
                newColumns.MouseWheel += (sender, e) => { ((HandledMouseEventArgs)e).Handled = true; };
                newColumns.KeyPress += (sender, e) => { e.Handled = true; };
                newColumns.Cursor = Cursors.Arrow;
                newColumns.BackColor = Color.White;
                newColumns.ValueChanged += (sender, e) => { gridResize(newGrid, (int)newColumns.Value, (int)newRows.Value); };
                newColumns.Visible = (param.Options.Length == 0);
                panel.Controls.Add(newLblColumns);
                panel.Controls.Add(newColumns);

                paramControls.Add(new ParamControl(param, newGrid, tab, newRows, newColumns));


                itemHeight = 180;

            }


            y = y + itemTopPadding + itemHeight + itemBottomPadding;
            
        }

        private void gridResize(DataGridView grid, int newColumns, int newRows) {

            while (grid.Columns.Count > newColumns)     grid.Columns.RemoveAt(grid.Columns.Count - 1);
            while (grid.Columns.Count < newColumns)     grid.Columns.Add((grid.Name + "Column" + grid.Columns.Count), (grid.Columns.Count + 1).ToString());

            if (grid.Columns.Count != 0) {
                while (grid.Rows.Count > newRows)           grid.Rows.RemoveAt(grid.Rows.Count - 1);
                while (grid.Rows.Count < newRows)           grid.Rows.Add();
            }

            grid.ClearSelection();
        }

        private void GUIConfig_Load(object sender, EventArgs e) {

            // fill the fields with values
            updateFields();

        }

        private void updateFields() {

            // loop through each paramset
            for (int i = 0; i < paramControls.Count; i++) {
                iParam param = paramControls[i].param;

                if (param is ParamBool) {
                    CheckBox chk = (CheckBox)paramControls[i].control;
                    chk.Checked = ((ParamBool)param).Value;
                    
                } else if ((param is ParamInt && param.Options.Length != 0) || (param is ParamDouble && param.Options.Length != 0)) {                    
                    
                    // int/double emulated options
                    ComboBox cmb = (ComboBox)paramControls[i].control;
                    int intValue = 0;
                    int.TryParse(param.getValue(), NumberStyles.AllowDecimalPoint, Parameters.NumberCulture, out intValue);
                    if (intValue > param.Options.Length)    intValue = 0;
                    cmb.SelectedIndex = intValue;

                } else if ((param is ParamInt && param.Options.Length == 0) || (param is ParamDouble && param.Options.Length == 0) || param is ParamBoolArr || param is ParamIntArr || param is ParamDoubleArr || param is ParamString) {

                    TextBox txt = (TextBox)paramControls[i].control;
                    txt.Text = param.getValue();

                } else if (param is ParamBoolMat || param is ParamIntMat || param is ParamDoubleMat || param is ParamStringMat) {

                    // retrieve references to the control and parameter value(s)
                    DataGridView grd = (DataGridView)paramControls[i].control;
                    NumericUpDown grdRows = (NumericUpDown)paramControls[i].additionalControl1;
                    NumericUpDown grdColumns = (NumericUpDown)paramControls[i].additionalControl2;
                    
                    bool[][]boolValues = null;
                    int[][]intValues = null;
                    double[][]dblValues = null;
                    string[][]strValues = null;
                    Parameters.Units[][] units = null;
                    int columns = 0;
                    int maxRows = 0;
                    if (param is ParamBoolMat) {
                        boolValues = ((ParamBoolMat)param).Value;
                        columns = boolValues.Count();
                        for (int c = 0; c < columns; c++)   if (boolValues[c].Count() > maxRows)    maxRows = boolValues[c].Count();
                    }
                    if (param is ParamIntMat) {
                        intValues = ((ParamIntMat)param).Value;
                        units = ((ParamIntMat)param).Unit;
                        columns = intValues.Count();
                        for (int c = 0; c < columns; c++)   if (intValues[c].Count() > maxRows)    maxRows = intValues[c].Count();
                    }
                    if (param is ParamDoubleMat) {
                        dblValues = ((ParamDoubleMat)param).Value;
                        units = ((ParamDoubleMat)param).Unit;
                        columns = dblValues.Count();
                        for (int c = 0; c < columns; c++)   if (dblValues[c].Count() > maxRows)    maxRows = dblValues[c].Count();
                    }
                    if (param is ParamStringMat) {
                        strValues = ((ParamStringMat)param).Value;
                        columns = strValues.Count();
                        for (int c = 0; c < columns; c++)   if (strValues[c].Count() > maxRows)    maxRows = strValues[c].Count();
                    }

                    // set the columns (the changevalue property of the of the NumericUpDown control will do the actual resizing of the grid)
                    if (param.Options.Length == 0) {
                        grdColumns.Value = columns;
                    } else {
                        grdColumns.Value = param.Options.Length;
                        for (int c = 0; c < param.Options.Length; c++) {
                            grd.Columns[c].HeaderText = param.Options[c];
                        }
                    }


                    // set the rows (the changevalue property of the of the NumericUpDown control will do the actual resizing of the grid)
                    if (maxRows > 0)        grdRows.Value = maxRows;
                    else                    grdRows.Value = 0;

                    // set the values
                    for (int c = 0; c < columns; c++) {

                        if (param is ParamBoolMat) {
                            for (int r = 0; r < boolValues[c].Count(); r++) {
                                grd[c, r].Value = (boolValues[c][r] ? "1" : "0");
                            }
                        }
                        if (param is ParamIntMat) {
                            for (int r = 0; r < intValues[c].Count(); r++) {
                                grd[c, r].Value = (intValues[c][r].ToString(NumberCulture) + (units[c][r] == Parameters.Units.Seconds ? "s" : ""));
                                
                            }
                        }
                        if (param is ParamDoubleMat) {
                            for (int r = 0; r < dblValues[c].Count(); r++) {
                                grd[c, r].Value = (dblValues[c][r].ToString(NumberCulture) + (units[c][r] == Parameters.Units.Seconds ? "s" : ""));
                            }
                        }
                        if (param is ParamStringMat) {
                            for (int r = 0; r < strValues[c].Count(); r++) {
                                grd[c, r].Value = strValues[c][r];
                            }
                        }

                    }

                } else if (param is ParamColor) {

                    PictureBox pic = (PictureBox)paramControls[i].control;
                    RGBColorFloat value = ((ParamColor)param).Value;
                    pic.BackColor = Color.FromArgb(value.getRedAsByte(), value.getGreenAsByte(), value.getBlueAsByte());

                }

            }
            
        }

        private bool saveFields(bool checkInputFirst = true) {
            bool hasError = false;
            TabPage hasErrorFirstTab = null;

            // loop through each paramset
            for (int i = 0; i < paramControls.Count; i++) {
                iParam param = paramControls[i].param;

                if (param is ParamBool) {
                    CheckBox chk = (CheckBox)paramControls[i].control;
                    
                    // testing or saving
                    if (checkInputFirst) {
                        // testing the values before saving

                        // 
                        // will always work regardless of value, so skip this step
                        // 

                    } else {
                        // saving

                        if (!((ParamBool)param).setValue(chk.Checked)) {

                            // flag
                            hasError = true;
                            if (hasErrorFirstTab == null)   hasErrorFirstTab = paramControls[i].tab;

                            // message
                            logger.Error("Error while trying to save the value for parameter " + param.Name + "");
                            
                        }
                    }
                    
                } else if ((param is ParamInt && param.Options.Length != 0) || (param is ParamDouble && param.Options.Length != 0)) {

                    // int/double emulated options
                    ComboBox cmb = (ComboBox)paramControls[i].control;

                    // testing or saving
                    if (checkInputFirst) {
                        // testing

                        // try to parse the text
                        if (!param.tryValue(cmb.SelectedIndex.ToString())) {
                            
                            // flag
                            hasError = true;
                            if (hasErrorFirstTab == null) hasErrorFirstTab = paramControls[i].tab;

                            // mark box as wrong
                            cmb.BackColor = Color.Tomato;

                        } else {

                            // mark box as normal
                            cmb.BackColor = Color.White;

                        }

                    } else {
                        // saving

                        if (!param.setValue(cmb.SelectedIndex.ToString())) {

                            // flag
                            hasError = true;
                            if (hasErrorFirstTab == null)   hasErrorFirstTab = paramControls[i].tab;

                            // message
                            logger.Error("Error while trying to save the value for parameter " + param.Name + "");
                            
                        }

                    }

                } else if ((param is ParamInt && param.Options.Length == 0) || (param is ParamDouble && param.Options.Length == 0) || param is ParamBoolArr || param is ParamIntArr || param is ParamDoubleArr || param is ParamString) {
                    TextBox txt = (TextBox)paramControls[i].control;

                    // testing or saving
                    if (checkInputFirst) {
                        // testing

                        // try to parse the text
                        if (!param.tryValue(txt.Text)) {
                            
                            // flag
                            hasError = true;
                            if (hasErrorFirstTab == null) hasErrorFirstTab = paramControls[i].tab;

                            // mark box as wrong
                            txt.BackColor = Color.Tomato;

                        } else {

                            // mark box as normal
                            txt.BackColor = Color.White;

                        }

                    } else {
                        // saving

                        if (!param.setValue(txt.Text)) {

                            // flag
                            hasError = true;
                            if (hasErrorFirstTab == null)   hasErrorFirstTab = paramControls[i].tab;

                            // message
                            logger.Error("Error while trying to save the value for parameter " + param.Name + "");
                            
                        }

                    }
                
                } else if (param is ParamBoolMat || param is ParamIntMat || param is ParamDoubleMat || param is ParamStringMat) {

                    // retrieve references to the control and parameter value(s)
                    DataGridView grd = (DataGridView)paramControls[i].control;
                    NumericUpDown grdRows = (NumericUpDown)paramControls[i].additionalControl1;
                    NumericUpDown grdColumns = (NumericUpDown)paramControls[i].additionalControl2;
                    int columns = (int)grdColumns.Value;
                    int rows = (int)grdRows.Value;
                    
                    // create input string
                    string matstring = "";
                    if (columns > 0 && rows > 0) {
                        for (int c = 0; c < columns; c++) {
                            for (int r = 0; r < rows; r++) {
                                Object cell = grd[c, r].Value;
                                if (cell==null)     matstring += " ";
                                else                matstring += cell.ToString().Trim().Replace(',','.');
                                if (r != rows - 1) matstring += ","; 
                            }
                            if (c != columns - 1) matstring += ";"; 
                        }
                    }
                    
                    // testing or saving
                    if (checkInputFirst) {
                        // testing

                        // try to parse the value
                        if (!param.tryValue(matstring)) {
                            
                            // flag
                            hasError = true;
                            if (hasErrorFirstTab == null) hasErrorFirstTab = paramControls[i].tab;

                            // mark box as wrong
                            //grd.BackgroundColor = Color.Tomato;
                            for (int c = 0; c < grd.ColumnCount; c++) {
                                for (int r = 0; r < grd.RowCount; r++) {
                                    grd[c, r].Style.BackColor = Color.Tomato;
                                }
                            }


                        } else {

                            // mark box as normal
                            //grd.BackgroundColor = Color.White;
                            for (int c = 0; c < grd.ColumnCount; c++) {
                                for (int r = 0; r < grd.RowCount; r++) {
                                    grd[c, r].Style.BackColor = Color.White;
                                }
                            }

                        }

                    } else {
                        // saving

                        if (!param.setValue(matstring)) {

                            // flag
                            hasError = true;
                            if (hasErrorFirstTab == null)   hasErrorFirstTab = paramControls[i].tab;

                            // message
                            logger.Error("Error while trying to save the value for parameter " + param.Name + "");
                            
                        }

                    }
                

                } else if (param is ParamColor) {
                    PictureBox pic = (PictureBox)paramControls[i].control;

                    // create input string
                    string picString = pic.BackColor.R + ";" + pic.BackColor.G + ";" + pic.BackColor.B;

                    // testing or saving
                    if (checkInputFirst) {
                        // testing

                        // try to parse the string
                        if (!param.tryValue(picString)) {
                            
                            // flag
                            hasError = true;
                            if (hasErrorFirstTab == null) hasErrorFirstTab = paramControls[i].tab;

                            // mark as wrong
                            // TODO?

                        } else {

                            // mark as normal
                            // TODO?

                        }

                    } else {
                        // saving

                        if (!param.setValue(picString)) {

                            // flag
                            hasError = true;
                            if (hasErrorFirstTab == null)   hasErrorFirstTab = paramControls[i].tab;

                            // message
                            logger.Error("Error while trying to save the value for parameter " + param.Name + "");
                            
                        }

                    }


                }

            }

            // if there was an error return false
            if (hasError)  {

                // goto the first tab that encountered an error
                if (hasErrorFirstTab != null)   tabControl.SelectedTab = hasErrorFirstTab;

                // return failure
                return false;

            }

            // check if this was only a check, if so, now continue to actually store the fields
            if (checkInputFirst)    return saveFields(false);

            // return success (if no error and not checking input but actually saving)
            return true;

        }

        private void btnSave_Click(object sender, EventArgs e) {

            // try to save the field
            if (saveFields()) {
                // success

                // message
                logger.Info("Configuration was saved.");

                // flag the configuration as adjusted
                this.DialogResult = DialogResult.OK;

                // close the form
                this.Close();

            } else {
                // failure

                // messagebox
                var result = MessageBox.Show("One or more parameters are invalid, these parameters are indicated in red.", "Error while saving", MessageBoxButtons.OK, MessageBoxIcon.Error);

            }

        }

        private void btnCancel_Click(object sender, EventArgs e) {

            // flag the configuration as adjusted
            this.DialogResult = DialogResult.Cancel;
            
            // close the form
            this.Close();
            
        }

        private void GUIConfig_FormClosing(object sender, FormClosingEventArgs e) {
            /*
            // TODO: check for changes

            // check whether the user is closing the form
            if (e.CloseReason == CloseReason.UserClosing) {

                // ask the user for confirmation
                if (MessageBox.Show("Are you sure you want to close?", "Are you sure?", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No) {
                    // user clicked no

                    // cancel the closing
                    e.Cancel = true;

                } else {

                    // continuing will close the form

                }

            }
            
            // check if the form is actually closing
            if (e.Cancel == false) {

                // flag the configuration as adjusted
                //this.DialogResult = DialogResult.Cancel;

            }
            */
            
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
                rect.Top = this.Top;
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
