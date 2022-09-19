/**
 * The GUIConfig class
 * 
 * ...
 * 
 * 
 * Copyright (C) 2017:  RIBS group (Nick Ramsey Lab), University Medical Center Utrecht (The Netherlands) & external contributors
 * Author(s):           Max van den Boom            (info@maxvandenboom.nl)
 * 
 * This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software
 * Foundation, either version 3 of the License, or (at your option) any later version. This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for
 * more details. You should have received a copy of the GNU General Public License along with this program. If not, see <https://www.gnu.org/licenses/>.
 */
using NLog;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Palmtree.Core.Helpers;
using Palmtree.Core.Params;

namespace Palmtree.GUI {

    /// <summary>
    /// The <c>GUIConfig</c> class.
    /// 
    /// ...
    /// </summary>
    public partial class GUIConfig : Form {

        private const int labelWidth = 240;
        private const int itemTopPadding = 10;
        private const int itemBottomPadding = 10;

        private static Logger logger = LogManager.GetLogger("GUIConfig");

        private struct ParamControl {
            public iParam globalParam;          // reference to global copy of the parameter (used to save the parameter for runtime usage)
            public iParam localParam;           // reference to local copy of the parameter (used to load/save the parameter set from/as file)

            public Control control;
            public TabPage tab;
            public Control additionalControl1;
            public Control additionalControl2;

            public ParamControl(ref iParam globalParam, ref iParam localParam, TabPage tab) {
                this.globalParam = globalParam;
                this.localParam = localParam;
                this.tab = tab;
                this.control = null;
                this.additionalControl1 = null;
                this.additionalControl2 = null;
            }

            public ParamControl(ref iParam globalParam, ref iParam localParam, TabPage tab, Control control) {
                this.globalParam = globalParam;
                this.localParam = localParam;
                this.tab = tab;
                this.control = control;
                this.additionalControl1 = null;
                this.additionalControl2 = null;
            }
        
            public ParamControl(iParam globalParam, iParam localParam, TabPage tab, Control control, Control additionalControl1, Control additionalControl2) {
                this.globalParam = globalParam;
                this.localParam = localParam;
                this.tab = tab;
                this.control = control;
                this.additionalControl1 = additionalControl1;
                this.additionalControl2 = additionalControl2;
            }
        
        }

        private static Dictionary<string, Parameters> localParamSets = null;         // local copy of the parameter sets (used to load/save the parameter set from/as file)
        private List<ParamControl> paramControls = new List<ParamControl>(0);

        public GUIConfig() {

            // initialize components
            InitializeComponent();

            // suspend the tabcontrol layout
            tabControl.SuspendLayout();

            // retrieve the parameter set to build the controls on
            Dictionary<string, Parameters> paramSets = ParameterManager.getParameterSets();

            // create a local copy of the dataset
            localParamSets = ParameterManager.getParameterSetsClone();
            
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
                newPanel.MouseClick += new MouseEventHandler(delegate(object sender, MouseEventArgs e) {
                    clearFocusToPanel(newPanel);
                });
                newTab.Controls.Add(newPanel);

                // TODO: check grouping etc


                // loop through the parameters
                List<iParam> globalParameters = entry.Value.getParameters();
                List<iParam> localParameters = localParamSets[entry.Key].getParameters();
                int y = 20;
                for (int i = 0; i < globalParameters.Count; i++) {

                    // retrieve references to the global and local parameter
                    iParam globalParam = globalParameters[i];
                    iParam localParam = localParameters[i];

                    // create ParamControl object
                    ParamControl paramControl = new ParamControl(ref globalParam, ref localParam, newTab);

                    // create the control (and attach to ParamControl object)
                    addConfigItemToControl(newTab, newPanel, ref paramControl, ref y);

                    // add paramControl to collection
                    paramControls.Add(paramControl);

                }

                // check if the y is higher than the panel height (if there is scrolling; the VerticalScroll.Visible property does not work at this point)
                if (y > newPanel.Height) {

                    // add an empty label at the end to create dummy space
                    Label newLbl = new Label();
                    newLbl.Name = newPanel.Name + "_lblEndDummy";
                    newLbl.Location = new Point(10, y);
                    newLbl.Size = new System.Drawing.Size(labelWidth, 20);
                    newLbl.Text = "";
                    newLbl.Parent = newPanel;
                    newLbl.MouseClick += new MouseEventHandler(delegate(object sender, MouseEventArgs e) {
                        clearFocusToPanel(newPanel);
                    });
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

            // upon changing the tab, clear the selection for datagrid controls and set the focus to the panel
            tabControl.SelectedIndexChanged += new EventHandler(delegate(object sender, EventArgs e) {
                clearFocusToPanel((Panel)tabControl.SelectedTab.Controls[0]);
            });

        }
        
        private void addConfigItemToControl(TabPage tab, Panel panel, ref ParamControl paramControl, ref int y) {

            // retrieve reference to the global parameter
            iParam param = paramControl.globalParam;
            
            int itemHeight = 0;
            if (param is ParamSeperator) {
                
                // create and add a label
                SeperatorLabelControl newSep = new SeperatorLabelControl();
                newSep.Name = panel.Name + "_sep" + param.Name + "_y" + y;
                newSep.Location = new Point(50, y + itemTopPadding);
                newSep.Size = new System.Drawing.Size(panel.Width, 20);
                newSep.Text = param.Name + "  ";
                newSep.ForeColor = Color.FromArgb(30, 30 ,30);
                newSep.Font = new System.Drawing.Font("Microsoft Sans Serif", 14F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel, ((byte)(204)));
                newSep.Parent = panel;
                newSep.TextAlign = ContentAlignment.TopRight;
                newSep.MouseClick += new MouseEventHandler(delegate(object sender, MouseEventArgs e) {
                    clearFocusToPanel(panel);
                });
                panel.Controls.Add(newSep);
                itemHeight = 20;

            } else {

                // create and add a label
                Label newLbl = new Label();
                newLbl.Name = panel.Name + "_lbl" + param.Name;
                newLbl.Location = new Point(10, y + itemTopPadding);
                newLbl.Size = new System.Drawing.Size(labelWidth, 20);
                newLbl.Text = param.Name;
                newLbl.Font = new System.Drawing.Font("Microsoft Sans Serif", 14F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel, ((byte)(204)));
                newLbl.Parent = panel;
                newLbl.TextAlign = ContentAlignment.TopRight;
                newLbl.MouseClick += new MouseEventHandler(delegate(object sender, MouseEventArgs e) {
                    clearFocusToPanel(panel);
                });
                ToolTip tt = new ToolTip();
                tt.AutoPopDelay = 15000;
                tt.InitialDelay = 200;
                tt.SetToolTip(newLbl, param.Desc);
                panel.Controls.Add(newLbl);

                
                if (param is ParamBool) {

                    // create and add a checkbox
                    CheckBox newChk = new CheckBox();
                    newChk.Name = panel.Name + "_chk" + param.Name;
                    newChk.Location = new Point(labelWidth + 20, y + itemTopPadding - 2);
                    newChk.Text = "";
                    newChk.Font = new System.Drawing.Font("Microsoft Sans Serif", 14F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel, ((byte)(204)));
                    panel.Controls.Add(newChk);
                    paramControl.control = newChk;
                    itemHeight = 20;

                } else if (param is ParamInt || param is ParamDouble) {

                    // check if there are emulated options
                    if (param.Options.Length == 0) {
                        // not emulated options

                        // create and add a textbox
                        TextBox newTxt = new TextBox();
                        newTxt.Name = panel.Name + "_txt" + param.Name;
                        newTxt.Location = new Point(labelWidth + 20, y + itemTopPadding - 2);
                        newTxt.Size = new System.Drawing.Size(200, 20);
                        newTxt.Font = new System.Drawing.Font("Microsoft Sans Serif", 14F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel, ((byte)(204)));
                        panel.Controls.Add(newTxt);
                        paramControl.control = newTxt;
                        itemHeight = 20;

                    } else {
                        // emulated options

                        // create and add a combobox
                        ComboBox newCmb = new ComboBox();
                        newCmb.DropDownStyle = ComboBoxStyle.DropDownList;
                        newCmb.Name = panel.Name + "_cmb" + param.Name;
                        newCmb.SelectedValueChanged += new EventHandler(delegate(object sender, EventArgs e) {
                            // after changing make sure the combobox loses focus, this prevents scrolling
                            // or keypresses from accidentally changing the selection
                            panel.Focus();
                        });
                        newCmb.Location = new Point(labelWidth + 20, y + itemTopPadding - 3);
                        newCmb.Size = new System.Drawing.Size(320, 20);
                        newCmb.Font = new System.Drawing.Font("Microsoft Sans Serif", 14F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel, ((byte)(204)));
                        for (int i = 0; i < param.Options.Length; i++)  newCmb.Items.Add(param.Options[i]);
                        panel.Controls.Add(newCmb);
                        paramControl.control = newCmb;
                        itemHeight = 22;

                    }
                
                
                } else if (param is ParamColor) {

                    PictureBox newPic = new PictureBox();
                    newPic.BorderStyle = BorderStyle.FixedSingle;
                    newPic.Name = panel.Name + "_pic" + param.Name;
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
                    paramControl.control = newPic;
                    itemHeight = 20;

                } else if (param is ParamBoolArr || param is ParamIntArr || param is ParamDoubleArr || param is ParamString) {


                    int elementWidth = 0;

                    if (param is ParamString && param.Options.Length != 0) {
                        // string with emulated options

                        elementWidth = 320;

                        // create and add a combobox
                        ComboBox newCmb = new ComboBox();
                        newCmb.Name = panel.Name + "_cmb" + param.Name;
                        newCmb.Location = new Point(labelWidth + 20, y + itemTopPadding - 3);
                        newCmb.Size = new System.Drawing.Size(elementWidth, 20);
                        newCmb.Font = new System.Drawing.Font("Microsoft Sans Serif", 14F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel, ((byte)(204)));
                        for (int i = 0; i < param.Options.Length; i++)  newCmb.Items.Add(param.Options[i]);
                        panel.Controls.Add(newCmb);
                        paramControl.control = newCmb;
                        itemHeight = 22;

                    } else {
                        // bool-, int- or double-array or string without emulated options

                        elementWidth = (param is ParamFileString ? 480 : 340);

                        // create and add a textbox
                        TextBox newTxt = new TextBox();
                        newTxt.Name = panel.Name + "_txt" + param.Name;
                        newTxt.Location = new Point(labelWidth + 20, y + itemTopPadding - 2);
                        newTxt.Size = new System.Drawing.Size(elementWidth, 20);
                        newTxt.Font = new System.Drawing.Font("Microsoft Sans Serif", 14F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel, ((byte)(204)));
                        panel.Controls.Add(newTxt);
                        paramControl.control = newTxt;
                        itemHeight = 20;

                        // if FileString parameter, add a browse option
                        if (param is ParamFileString) {
                    
                            // create and add a button
                            Button newBtn = new Button();
                            newBtn.Name = panel.Name + "_btn" + param.Name;
                            newBtn.Location = new Point(labelWidth + elementWidth + 20, y + itemTopPadding - 2);
                            newBtn.Size = new System.Drawing.Size(40, 23);
                            newBtn.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel, ((byte)(204)));
                            newBtn.Text = "...";
                            newBtn.Click += (sender, e) => {

                                // open file dialog to open dat file
                                OpenFileDialog dlgLoadDatFile = new OpenFileDialog();

                                // set initial directory (or the closest we can get)
                                string folder = newTxt.Text;
                                bool tryFolder = true;
                                while (tryFolder) {
                                    try {
                                        FileAttributes attr = File.GetAttributes(folder);
                                        if ((attr & FileAttributes.Directory) == FileAttributes.Directory)
                                            tryFolder = false;
                                        else {
                                            int lastIndex = folder.LastIndexOf('\\');
                                            if (lastIndex == -1) {
                                                tryFolder = false;
                                                folder = "";
                                            } else
                                                folder = folder.Substring(0, lastIndex);
                                        }
                                    } catch (Exception) {
                                        if (folder.Length > 0) folder = folder.Substring(0, folder.Length - 1);
                                        int lastIndex = folder.LastIndexOf('\\');
                                        if (lastIndex == -1) {
                                            tryFolder = false;
                                            folder = "";
                                        } else
                                            folder = folder.Substring(0, lastIndex);
                                    }

                                }
                                if (string.IsNullOrEmpty(folder)) dlgLoadDatFile.InitialDirectory = Directory.GetCurrentDirectory();
                                else dlgLoadDatFile.InitialDirectory = folder;

                                // 
                                dlgLoadDatFile.Filter = "All files (*.*)|*.*";
                                dlgLoadDatFile.RestoreDirectory = true;            // restores current directory to the previously selected directory, potentially beneficial if other code relies on the currently set directory

                                // check if ok has been clicked on the dialog
                                if (dlgLoadDatFile.ShowDialog() == DialogResult.OK) {

                                    newTxt.Text = dlgLoadDatFile.FileName;

                                }

                            };
                            panel.Controls.Add(newBtn);
                            paramControl.additionalControl1 = newBtn;

                        }
                
                    }

                    // 
                    if (param is ParamString && !(param is ParamFileString)) {
                        Param.ParamSideButton[] sideButtons = ((ParamString)param).Buttons;
                        if (sideButtons != null) {
                            int buttonLeft = labelWidth + elementWidth + 25;

                            // create and add buttons
                            for (int iButton = 0; iButton < sideButtons.Length; iButton++) {
                                Button newBtn = new Button();
                                newBtn.Name = panel.Name + "_btn" + param.Name + "Side" + iButton;
                                newBtn.Location = new Point(buttonLeft, y + itemTopPadding - 2);
                                newBtn.Size = new System.Drawing.Size(sideButtons[iButton].width, 23);
                                newBtn.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel, ((byte)(204)));
                                newBtn.Text = sideButtons[iButton].name;
                                if (sideButtons[iButton].clickEvent != null)
                                    newBtn.Click += sideButtons[iButton].clickEvent;
                                panel.Controls.Add(newBtn);
                                paramControl.additionalControl1 = newBtn;

                                buttonLeft += sideButtons[iButton].width + 5;
                            }

                        }
                    }


                } else if (param is ParamBoolMat || param is ParamIntMat || param is ParamDoubleMat || param is ParamStringMat) {

                    // add the data grid
                    DataGridView newGrid = new DataGridView();
                    newGrid.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
                    newGrid.Name = panel.Name + "_grd" + param.Name;
                    newGrid.Location = new Point(labelWidth + 20, y + itemTopPadding - 2);
                    newGrid.Size = new System.Drawing.Size(650, 144);
                    newGrid.AllowUserToAddRows = false;
                    newGrid.AllowUserToDeleteRows = false;
                    newGrid.AllowUserToResizeRows = false;
                    newGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
                    newGrid.RowHeadersVisible = false;
                    newGrid.DefaultCellStyle.FormatProvider = Parameters.NumberCulture;
                    newGrid.AllowUserToOrderColumns = (param.Options.Length == 0);
                    panel.Controls.Add(newGrid);

                    // 
                    Label newLblRows = new Label();
                    Label newLblColumns = new Label();
                    NumericUpDown newRows = new System.Windows.Forms.NumericUpDown();
                    NumericUpDown newColumns = new System.Windows.Forms.NumericUpDown();

                    // rows
                    newLblRows.Name = panel.Name + "_lbl" + param.Name + "Rows";
                    newLblRows.Location = new Point(labelWidth + 20, newGrid.Location.Y + newGrid.Size.Height + 7);
                    newLblRows.Size = new System.Drawing.Size(50, 20);
                    newLblRows.Text = "Rows:";
                    newLblRows.Font = new System.Drawing.Font("Microsoft Sans Serif", 14F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel, ((byte)(204)));
                    newLblRows.Parent = panel;
                    newLblRows.TextAlign = ContentAlignment.TopRight;
                    newLblRows.MouseClick += new MouseEventHandler(delegate(object sender, MouseEventArgs e) {
                        clearFocusToPanel(panel);
                    });
                    newRows.Name = panel.Name + "_num" + param.Name + "Rows";
                    newRows.Location = new Point(labelWidth + 75, newGrid.Location.Y + newGrid.Size.Height + 5);
                    newRows.Size = new System.Drawing.Size(50, 20);
                    newRows.Font = new System.Drawing.Font("Microsoft Sans Serif", 14F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel, ((byte)(204)));
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
                    newLblColumns.Name = panel.Name + "_lbl" + param.Name + "Colums";
                    newLblColumns.Location = new Point(labelWidth + 140, newGrid.Location.Y + newGrid.Size.Height + 7);
                    newLblColumns.Size = new System.Drawing.Size(80, 20);
                    newLblColumns.Text = "Columns:";
                    newLblColumns.Font = new System.Drawing.Font("Microsoft Sans Serif", 14F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel, ((byte)(204)));
                    newLblColumns.Parent = panel;
                    newLblColumns.TextAlign = ContentAlignment.TopRight;
                    newLblColumns.Visible = (param.Options.Length == 0);
                    newLblColumns.MouseClick += new MouseEventHandler(delegate(object sender, MouseEventArgs e) {
                        clearFocusToPanel(panel);
                    });
                    newColumns.Name = panel.Name + "_num" + param.Name + "Columns";
                    newColumns.Location = new Point(labelWidth + 225, newGrid.Location.Y + newGrid.Size.Height + 5);
                    newColumns.Size = new System.Drawing.Size(50, 20);
                    newColumns.Font = new System.Drawing.Font("Microsoft Sans Serif", 14F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel, ((byte)(204)));
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


                    // create and add a save button
                    Button newBtnSave = new Button();
                    newBtnSave.Name = panel.Name + "_btn" + param.Name + "Save";
                    newBtnSave.Size = new System.Drawing.Size(40, 23);
                    newBtnSave.Location = new Point(newGrid.Location.X + newGrid.Size.Width - newBtnSave.Size.Width, newGrid.Location.Y + newGrid.Size.Height + 7);
                    newBtnSave.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel, ((byte)(204)));
                    newBtnSave.Text = "Save";
                    newBtnSave.Click += (sender, e) => {

                        // retrieve the values as string
                        string strMat = gridToString(newGrid);

                        // open file dialog to save file
                        SaveFileDialog dlgSaveDatFile = new SaveFileDialog();
                        dlgSaveDatFile.Filter = "Matrix files (*.mat)|*.mat|All files (*.*)|*.*";
                        dlgSaveDatFile.RestoreDirectory = true;            // restores current directory to the previously selected directory, potentially beneficial if other code relies on the currently set directory

                        // check if ok has been clicked on the dialog
                        if (dlgSaveDatFile.ShowDialog() == DialogResult.OK) {
                        
                            // write the values as text to a file
                            try { 
                                File.WriteAllText(dlgSaveDatFile.FileName, strMat);
                            } catch (Exception) {
                                logger.Error("Could not write matrix values to file '" + dlgSaveDatFile.FileName +  "'");
                                return;
                            }

                        }

                    };
                    panel.Controls.Add(newBtnSave);

                    // create and add a load button
                    Button newBtnLoad = new Button();
                    newBtnLoad.Name = panel.Name + "_btn" + param.Name + "Load";
                    newBtnLoad.Size = new System.Drawing.Size(40, 23);
                    newBtnLoad.Location = new Point(newBtnSave.Location.X - newBtnLoad.Size.Width - 4, newGrid.Location.Y + newGrid.Size.Height + 7);
                    newBtnLoad.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel, ((byte)(204)));
                    newBtnLoad.Text = "Load";
                    newBtnLoad.Click += (sender, e) => {

                        // open file dialog to open file
                        OpenFileDialog dlgOpenDatFile = new OpenFileDialog();
                        dlgOpenDatFile.Filter = "Matrix files (*.mat)|*.mat|All files (*.*)|*.*";
                        dlgOpenDatFile.RestoreDirectory = true;            // restores current directory to the previously selected directory, potentially beneficial if other code relies on the currently set directory

                        // check if ok has been clicked on the dialog
                        if (dlgOpenDatFile.ShowDialog() == DialogResult.OK) {

                            string strMat = "";

                            // read the values from the file
                            try {
                                strMat = File.ReadAllText(dlgOpenDatFile.FileName);
                            } catch (Exception) {
                                logger.Error("Could not read matrix values from file '" + dlgOpenDatFile.FileName + "'");
                                return;
                            }
                        
                            // try to interpret the value
                            iParam interpret = null;
                            if (param is ParamBoolMat)      interpret = new ParamBoolMat("", "", null, "", "", param.Options);
                            if (param is ParamIntMat)       interpret = new ParamIntMat("", "", null, "", "", param.Options);
                            if (param is ParamDoubleMat)    interpret = new ParamDoubleMat("", "", null, "", "", param.Options);
                            if (param is ParamStringMat)    interpret = new ParamStringMat("", "", null, "", "", param.Options);
                            if (!interpret.tryValue(strMat)) {
                                logger.Error("Could not interpret matrix values from file '" + dlgOpenDatFile.FileName + "'");
                            }
                            interpret.setValue(strMat);

                            // apply the values to the grid
                            paramValuesToGrid(newGrid, newColumns, newRows, interpret);

                        }
                    };
                    panel.Controls.Add(newBtnLoad);

                    paramControl.control = newGrid;
                    paramControl.additionalControl1 = newRows;
                    paramControl.additionalControl2 = newColumns;

                    itemHeight = 180;
                    
                    newGrid.LostFocus += new EventHandler(delegate(object sender, EventArgs e) {
                        clearFocusToPanel(panel);
                    });

                }

            }

            // 
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
            grid.CurrentCell = null;

        }

        private void GUIConfig_Load(object sender, EventArgs e) {

            // fill the fields with values
            updateFields(false);

        }

        // local determine is the fields should be loaded the local copy or global (runtime) parameter-sets
        private void updateFields(bool local) {

            // loop through each paramset
            for (int i = 0; i < paramControls.Count; i++) {

                // get the parameter reference
                iParam param = null;
                if (local)      param = paramControls[i].localParam;
                else            param = paramControls[i].globalParam;

                // skip seperator parameters, these are only for esthetics
                if (param.GetType() == typeof(ParamSeperator))
                    continue;

                // determine type of parameter and update accordingly
                if (param is ParamBool) {

                    CheckBox chk = (CheckBox)paramControls[i].control;
                    chk.Checked = ((ParamBool)param).Value;
                    
                } else if ((param is ParamInt && param.Options.Length != 0) || (param is ParamDouble && param.Options.Length != 0) || (param is ParamString && param.Options.Length != 0)) {                    
                    // int/double/string emulated options

                    ComboBox cmb = (ComboBox)paramControls[i].control;
                    if (param is ParamString) {
                        cmb.Text = param.getValue();
                    } else {
                        int intValue = 0;
                        int.TryParse(param.getValue(), NumberStyles.AllowDecimalPoint, Parameters.NumberCulture, out intValue);
                        if (intValue > param.Options.Length)    intValue = 0;
                        cmb.SelectedIndex = intValue;
                    }

                } else if ((param is ParamInt && param.Options.Length == 0) || (param is ParamDouble && param.Options.Length == 0) || (param is ParamString && param.Options.Length == 0) || param is ParamBoolArr || param is ParamIntArr || param is ParamDoubleArr) {

                    TextBox txt = (TextBox)paramControls[i].control;
                    txt.Text = param.getValue();

                } else if (param is ParamBoolMat || param is ParamIntMat || param is ParamDoubleMat || param is ParamStringMat) {

                    // retrieve references to the control and parameter value(s)
                    DataGridView grd = (DataGridView)paramControls[i].control;
                    NumericUpDown grdRows = (NumericUpDown)paramControls[i].additionalControl1;
                    NumericUpDown grdColumns = (NumericUpDown)paramControls[i].additionalControl2;

                    // apply the values to the grid
                    paramValuesToGrid(grd, grdColumns, grdRows, param);

                } else if (param is ParamColor) {

                    PictureBox pic = (PictureBox)paramControls[i].control;
                    RGBColorFloat value = ((ParamColor)param).Value;
                    pic.BackColor = Color.FromArgb(value.getRedAsByte(), value.getGreenAsByte(), value.getBlueAsByte());

                }

            }
            
        }

        // local determine is the fields should be loaded the local copy or global (runtime) parameter-sets
        private bool processFields(bool saveFields, bool local) {
            bool hasError = false;
            TabPage hasErrorFirstTab = null;

            // loop through each paramset
            for (int i = 0; i < paramControls.Count; i++) {

                // get the parameter reference
                iParam param = null;
                if (local)      param = paramControls[i].localParam;
                else            param = paramControls[i].globalParam;

                // skip seperator parameters, these are only for esthetics
                if (param.GetType() == typeof(ParamSeperator))
                    continue;

                // 
                if (param is ParamBool) {
                    CheckBox chk = (CheckBox)paramControls[i].control;
                    
                    // testing or saving
                    if (!saveFields) {
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
                    
                } else if ((param is ParamInt && param.Options.Length != 0) || (param is ParamDouble && param.Options.Length != 0) || (param is ParamString && param.Options.Length != 0)) {

                    // int/double emulated options
                    ComboBox cmb = (ComboBox)paramControls[i].control;

                    // retrieve the value to test or save
                    string cmbValue = "";
                    if (param is ParamString)
                        cmbValue = cmb.Text;
                    else
                        cmbValue = cmb.SelectedIndex.ToString();

                    // testing or saving
                    if (!saveFields) {
                        // testing

                        // try to parse the text
                        if (!param.tryValue(cmbValue)) {
                            
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

                        if (!param.setValue(cmbValue)) {

                            // flag
                            hasError = true;
                            if (hasErrorFirstTab == null)   hasErrorFirstTab = paramControls[i].tab;

                            // message
                            logger.Error("Error while trying to save the value for parameter " + param.Name + "");
                            
                        }

                    }

                } else if ((param is ParamInt && param.Options.Length == 0) || (param is ParamDouble && param.Options.Length == 0) || (param is ParamString && param.Options.Length == 0) || param is ParamBoolArr || param is ParamIntArr || param is ParamDoubleArr) {
                    TextBox txt = (TextBox)paramControls[i].control;

                    // testing or saving
                    if (!saveFields) {
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

                    // get the grid values as string
                    string strMat = gridToString(grd);
                    
                    // testing or saving
                    if (!saveFields) {
                        // testing

                        // try to parse the value
                        if (!param.tryValue(strMat)) {
                            
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

                        if (!param.setValue(strMat)) {

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
                    if (!saveFields) {
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

            // return success (if no error and not checking input but actually saving)
            return true;

        }

        private string gridToString(DataGridView grd) {

            int columns = grd.ColumnCount;
            int rows = grd.RowCount;

            // create input string
            string matstring = "";
            if (columns > 0 && rows > 0) {
                for (int c = 0; c < columns; c++) {
                    for (int r = 0; r < rows; r++) {
                        Object cell = grd[c, r].Value;
                        if (cell == null) matstring += "";
                        else matstring += cell.ToString().Trim().Replace(',', '.');
                        if (r != rows - 1) matstring += Parameters.MatRowDelimiters[0];
                    }
                    if (c != columns - 1) matstring += Parameters.MatColumnDelimiters[0];
                }
            }

            // return the string
            return matstring;

        }

        private void paramValuesToGrid(DataGridView grd, NumericUpDown grdColumns, NumericUpDown grdRows, iParam param) {

            bool[][] boolValues = null;
            int[][] intValues = null;
            double[][] dblValues = null;
            string[][] strValues = null;
            Parameters.Units[][] units = null;
            int columns = 0;
            int maxRows = 0;
            if (param is ParamBoolMat) {
                boolValues = ((ParamBoolMat)param).Value;
                columns = boolValues.Length;
                for (int c = 0; c < columns; c++) if (boolValues[c].Length > maxRows) maxRows = boolValues[c].Length;
            }
            if (param is ParamIntMat) {
                intValues = ((ParamIntMat)param).Value;
                units = ((ParamIntMat)param).Unit;
                columns = intValues.Length;
                for (int c = 0; c < columns; c++) if (intValues[c].Length > maxRows) maxRows = intValues[c].Length;
            }
            if (param is ParamDoubleMat) {
                dblValues = ((ParamDoubleMat)param).Value;
                units = ((ParamDoubleMat)param).Unit;
                columns = dblValues.Length;
                for (int c = 0; c < columns; c++) if (dblValues[c].Length > maxRows) maxRows = dblValues[c].Length;
            }
            if (param is ParamStringMat) {
                strValues = ((ParamStringMat)param).Value;
                columns = strValues.Length;
                for (int c = 0; c < columns; c++) if (strValues[c].Length > maxRows) maxRows = strValues[c].Length;
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
            if (maxRows > 0) grdRows.Value = maxRows;
            else grdRows.Value = 0;

            // loop over the columns
            for (int c = 0; c < columns; c++) {

                // ensure that each column is not sortable
                grd.Columns[c].SortMode = DataGridViewColumnSortMode.NotSortable;

                // set the values
                if (param is ParamBoolMat) {
                    for (int r = 0; r < boolValues[c].Length; r++) {
                        grd[c, r].Value = (boolValues[c][r] ? "1" : "0");
                    }
                }
                if (param is ParamIntMat) {
                    for (int r = 0; r < intValues[c].Length; r++) {
                        grd[c, r].Value = (intValues[c][r].ToString(Parameters.NumberCulture) + (units[c][r] == Parameters.Units.Seconds ? "s" : ""));

                    }
                }
                if (param is ParamDoubleMat) {
                    for (int r = 0; r < dblValues[c].Length; r++) {
                        grd[c, r].Value = (dblValues[c][r].ToString(Parameters.NumberCulture) + (units[c][r] == Parameters.Units.Seconds ? "s" : ""));
                    }
                }
                if (param is ParamStringMat) {
                    for (int r = 0; r < strValues[c].Length; r++) {
                        grd[c, r].Value = strValues[c][r];
                    }
                }

            }

        }

        private void btnSave_Click(object sender, EventArgs e) {

            // check the fields
            if (processFields(false, false)) {

                // try to save the fields
                if (processFields(true, false)) {
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

        private void btnLoadPrmFile_Click(object sender, EventArgs e) {
			
			// open file dialog to open parameter file, starting in current directory, with filter for .prm files
			OpenFileDialog dlgLoadPrmFile = new OpenFileDialog();
            dlgLoadPrmFile.InitialDirectory = Directory.GetCurrentDirectory();
            dlgLoadPrmFile.Filter = "Parameter files (*.prm)|*.prm|All files (*.*)|*.*";
            dlgLoadPrmFile.RestoreDirectory = true;            // restores current directory to the previously selected directory, potentially beneficial if other code relies on the currently set directory

			// if dialog is succesfully shown
			if (dlgLoadPrmFile.ShowDialog() == DialogResult.OK) {
                
                // try to load the parameter file
                ParameterManager.loadParameterFile(dlgLoadPrmFile.FileName, localParamSets);

                // update the fields in the form with the local settings
                updateFields(true);

			}
            
        }

        private void btnSavePrmFile_Click(object sender, EventArgs e) {

            // check the fields
            if (processFields(false, true)) {

                // try to save the fields
                if (processFields(true, true)) {
                    // success

                    // create save file dialog box for user with standard filename set to current time
                    SaveFileDialog dlgSavePrmFile = new SaveFileDialog();
                    dlgSavePrmFile.Filter = "Parameter files (*.prm)|*.prm|All files (*.*)|*.*";
                    dlgSavePrmFile.RestoreDirectory = true;                                         // restores current directory to the previously selected directory, potentially beneficial if other code relies on the currently set directory
                    dlgSavePrmFile.FileName = DateTime.Now.ToString("yyyyMMdd_HHmm") + ".prm";

                    // if user sucessfully selected location, save location and store file
                    if (dlgSavePrmFile.ShowDialog() == DialogResult.OK) {

                        // store the parameters based on the local copy of the parameter set
                        ParameterManager.saveParameterFile(dlgSavePrmFile.FileName, localParamSets);

                    }

                }
            }

        }

        private void clearFocusToPanel(Panel panel) {
            
            // for datagrids clear the selection
            for (int i = 0; i < panel.Controls.Count; i++) {
                if (panel.Controls[i] is DataGridView)
                    ((DataGridView)panel.Controls[i]).CurrentCell = null;
            }
                    
            // set focus to panel
            panel.Focus();
        
        }

        private void GUIConfig_KeyDown(object sender, KeyEventArgs e) {
            if (e.KeyCode == Keys.Down || e.KeyCode == Keys.Up) {
                Panel activePanel = (Panel)tabControl.SelectedTab.Controls[0];
                if (activePanel.Focused) {

                    int scrollChange = 50;
                    if (e.KeyCode == Keys.Down)
                        activePanel.AutoScrollPosition = new Point(0, -activePanel.AutoScrollPosition.Y + scrollChange);
                    else
                        activePanel.AutoScrollPosition = new Point(0, -activePanel.AutoScrollPosition.Y - scrollChange);
                    
                }
            }
        }

        private void GUIConfig_Shown(object sender, EventArgs e) {
            clearFocusToPanel((Panel)tabControl.SelectedTab.Controls[0]);
        }
    }

    class SeperatorLabelControl : Label {

        protected override void OnPaint(PaintEventArgs e) {
            base.OnPaint(e);
            e.Graphics.DrawLine(new Pen(Color.FromArgb(50, 50, 50)), 
                                0, this.Height - 1, this.Width, this.Height - 1);
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
