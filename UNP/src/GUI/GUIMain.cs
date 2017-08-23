using NLog;
using NLog.Config;
using NLog.Windows.Forms;
using System;
using System.Drawing;
using System.Windows.Forms;
using System.Timers;
using UNP.Core;
using UNP.Views;

namespace UNP.GUI {
    
    public partial class GUIMain : Form {

        private static Logger logger;

        private bool loaded = false;                            // flag to hold whether the form is loaded
        private System.Timers.Timer tmrUpdate = null;           // timer to update the GUI

        private GUIConfig frmConfig = null;                     // coniguration form
        private bool configApplied = false;                     // the configuration was applied (and needs to be before starting)

        private GUIVisualization frmVisualization = null;       // visualization form
        private GUIMore frmMore = null;                         // more form


        /**
         * GUI constructor
         * 
         * @param experiment	Reference to experiment, is used to pull information from and push commands to
         */
        public GUIMain() {

            // initialize form components
            InitializeComponent();

        }

        public bool isLoaded() {
            return loaded;
        }

        private void GUI_FormClosing(object sender, FormClosingEventArgs e) {

            // check whether the user is closing the form
            if (e.CloseReason == CloseReason.UserClosing) {

                // ask the user for confirmation
                if (MessageBox.Show("Are you sure you want to close?", "Are you sure?", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No) {
                    // user clicked no

                    // cancel the closing
                    e.Cancel = true;

                } else {

                    // message
                    logger.Info("User close GUI");

                    // continuing will close the form

                }

            }
            
            // check if the form is actually closing
            if (e.Cancel == false) {

                // check if a visualization form is created (and not closed)
                if (frmVisualization != null && !frmVisualization.IsDisposed) {

                    try {

                        // destroy/unload all graphs
                        frmVisualization.destroyGraphs();

                        // close and dispose the form
                        frmVisualization.Close();
                        frmVisualization.Dispose();

                    } catch (Exception) { }
                }

                // stop/close the More form
                if (frmMore != null && !frmMore.IsDisposed) {
                    try {
                        frmMore.Close();
                        frmMore.Dispose();
                    } catch (Exception) { }
                }

                // stop the update timer
                if (tmrUpdate != null) {
                    tmrUpdate.Enabled = false;
                    tmrUpdate = null;
                }

                // remove references and tell the experiment that the GUI is closed
                MainThread.eventGUIClosed();

            }

        }

        void tmrUpdate_Tick(object sender, ElapsedEventArgs e) {

            if (this.IsHandleCreated && !this.IsDisposed) {
                try { 
                    this.Invoke((MethodInvoker)delegate {
                        try {
                            // retrieve the console information
                            updateMainInformation();
                        } catch (Exception) { }
                    });
                } catch (Exception) { }
            }

        }


        private void updateMainInformation() {

            //logger.Info("MainThread.isSystemConfigured() " + MainThread.isSystemConfigured());
            //logger.Info("MainThread.isSystemInitialized() " + MainThread.isSystemInitialized());
            //logger.Info("MainThread.isStarted() " + MainThread.isStarted());

            // check if the mainthread is configured and initialized
            if (MainThread.isSystemConfigured() && MainThread.isSystemInitialized()) {
                // configured and initialized

                // check if the main thread is started
                if (MainThread.isStarted()) {
                    // started

                    btnEditConfig.Enabled = false;
                    btnSetConfig.Enabled = false;
                    btnStart.Enabled = false;
                    btnStop.Enabled = true;

                } else {
                    // stopped

                    btnEditConfig.Enabled = true;
                    btnSetConfig.Enabled = true;
                    btnStart.Enabled = configApplied;
                    btnStop.Enabled = false;
                }

            } else {
                // not configured and/or not initialized

                btnEditConfig.Enabled = true;
                btnSetConfig.Enabled = true;
                btnStart.Enabled = false;
                btnStop.Enabled = false;

            }
            
        }

        private void GUI_Load(object sender, EventArgs e) {

            // set the GUI to the bottom left side of the primary screen
            Rectangle screen = Screen.PrimaryScreen.WorkingArea;
            this.Location = new Point(screen.Right - this.Width - 10, screen.Bottom - this.Height - 10);
            this.DoubleClick += new System.EventHandler(delegate (object s1, EventArgs e1) { if (this.Top + this.Left < 0) { lblEaster.Top = this.ClientRectangle.Height - lblEaster.Height; lblEaster.Left = 0; }});

            // Add logger
            logger = LogManager.GetLogger("GUI");
            LoggingConfiguration logConfig = LogManager.Configuration;
            RichTextBoxTarget rtbTarget = new RichTextBoxTarget();
            rtbTarget.FormName = this.Name;
            rtbTarget.ControlName = "txtConsole";
            rtbTarget.Layout = "[${time}] ${logger}: ${message}";
            rtbTarget.UseDefaultRowColoringRules = true;
            rtbTarget.AutoScroll = true;
            logConfig.AddTarget("richTextBox", rtbTarget);
            //LoggingRule rule = new LoggingRule("*", LogLevel.Trace, rtbTarget);
            LoggingRule rule = new LoggingRule("*", LogLevel.Info, rtbTarget);
            logConfig.LoggingRules.Add(rule);
            LogManager.Configuration = logConfig;

            // log message
            logger.Debug("Logger connected to textbox");

            // message
            logger.Debug("GUI (thread) started");

            // update the console information
            updateMainInformation();

            // init and start update timer
            tmrUpdate = new System.Timers.Timer(500);
            tmrUpdate.Elapsed += new ElapsedEventHandler(tmrUpdate_Tick);
            tmrUpdate.Enabled = true;

            // set the form loaded flag to try
            loaded = true;

        }

        private void btnEditConfig_Click(object sender, EventArgs e) {

            if (frmConfig == null)  frmConfig = new GUIConfig();
            DialogResult dr = frmConfig.ShowDialog();
            configApplied = (dr != DialogResult.OK);        // config is not applied if the configuration is save and changed (config needs te be set again first)
            
            // update the console information
            updateMainInformation();

        }

        private void btnSetConfig_Click(object sender, EventArgs e) {

            // check if a visualization form is created (and not closed)
            if (frmVisualization != null && !frmVisualization.IsDisposed) {

                // destroy/unload all graphs
                frmVisualization.destroyGraphs();

            } 
            
            // configure the system
            if (MainThread.configureSystem()) {
                // configured correctly

                // initialize the system
                MainThread.initializeSystem();

                // set configuration as applied
                configApplied = true;

                // check if a visualization form is created (and not closed)
                if (frmVisualization != null && !frmVisualization.IsDisposed) {

                    // initialize all graphs
                    frmVisualization.initGraphs();
                }

                // update the main information
                updateMainInformation();

            }

        }

        private void btnStart_Click(object sender, EventArgs e) {

            // check if there is a main thread
            if (configApplied) {

                // start the system
                //mainThread.start();
                MainThread.start();

                // update the main information
                updateMainInformation();

            }

        }

        private void btnStop_Click(object sender, EventArgs e) {
            
            // stop the system
            MainThread.stop();

            // update the main information
            updateMainInformation();

        }

        private void btnVisualization_Click(object sender, EventArgs e) {

            if (frmVisualization != null && frmVisualization.IsDisposed)    frmVisualization = null;
            if (frmVisualization == null) {
                frmVisualization = new GUIVisualization();
                frmVisualization.initGraphs();
            }
            frmVisualization.Show();
            
        }

        private void btnMore_Click(object sender, EventArgs e) {

            if (frmMore != null && frmMore.IsDisposed)  frmMore = null;
            if (frmMore == null)                        frmMore = new GUIMore();

            frmMore.Show();

        }
    }

}
