using NLog;
using NLog.Config;
using NLog.Windows.Forms;
using UNP.views;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Timers;

namespace UNP {
    
    public partial class GUI : Form {

        private static Logger logger;

        private MainThread mainThread = null;           // reference to the main thread, used to pull information from and push commands to
        private IView view = null;                      // reference to the view, used to pull information from and push commands to
        private bool loaded = false;                    // flag to hold whether the form is loaded
        private System.Timers.Timer tmrUpdate = null;   // timer to update the GUI


        public static String getClassName() {
            Type myType = typeof(GUI);
            return myType.Namespace + "." + myType.Name;
        }

        /**
         * GUI constructor
         * 
         * @param experiment	Reference to experiment, is used to pull information from and push commands to
         */
        public GUI(MainThread mainThread) {
            this.mainThread = mainThread;

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

                // stop the update timer
                if (tmrUpdate != null) {
                    tmrUpdate.Enabled = false;
                    tmrUpdate = null;
                }

                // remove references and tell the experiment that the GUI is closed
                if (view != null)   view = null;
                if (mainThread != null) {
                    mainThread.eventGUIClosed();
                    mainThread = null;
                }

            }

        }

        void tmrUpdate_Tick(object sender, ElapsedEventArgs e) {
            
            // retrieve the console information
            updateMainInformation();

        }


        private void updateMainInformation() {

            // check the main thread reference
            if (mainThread != null) {
                //Console.WriteLine(mainThread);

                //logger.Info("mainThread.isSystemConfigured() " + mainThread.isSystemConfigured());
                //logger.Info("mainThread.isSystemInitialized() " + mainThread.isSystemInitialized());
                //logger.Info("mainThread.isStarted() " + mainThread.isStarted());


                // check if the mainthread is configured and initialized
                if (mainThread.isSystemConfigured() && mainThread.isSystemInitialized()) {
                    // configured and initialized

                    // check if the main thread is started
                    if (mainThread.isStarted()) {
                        // started

                        btnEditConfig.Enabled = false;
                        btnSetConfig.Enabled = false;
                        btnStart.Enabled = false;
                        btnStop.Enabled = true;

                    } else {
                        // stopped

                        btnEditConfig.Enabled = true;
                        btnSetConfig.Enabled = true;
                        //logger.Info("true " + btnStart.Enabled);
                        btnStart.Enabled = true;
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

            
        }

        private void GUI_Load(object sender, EventArgs e) {

            
            // Add logger
            logger = LogManager.GetLogger("GUI");
            LoggingConfiguration logConfig = LogManager.Configuration;
            RichTextBoxTarget rtbTarget = new RichTextBoxTarget();
            rtbTarget.FormName = "GUI";
            rtbTarget.ControlName = "txtConsole";
            rtbTarget.Layout = "[${time}] ${logger}: ${message}";
            rtbTarget.UseDefaultRowColoringRules = true;
            rtbTarget.AutoScroll = true;
            logConfig.AddTarget("richTextBox", rtbTarget);
            LoggingRule rule = new LoggingRule("*", LogLevel.Trace, rtbTarget);
            //LoggingRule rule = new LoggingRule("*", LogLevel.Info, rtbTarget);
            logConfig.LoggingRules.Add(rule);
            LogManager.Configuration = logConfig;

            // log message
            logger.Debug("Logger connected to textbox");

            // message
            logger.Info("GUI (thread) started");

            // update the console information
            updateMainInformation();

            // init and start update timer
            tmrUpdate = new System.Timers.Timer(500);
            tmrUpdate.Elapsed += new ElapsedEventHandler(tmrUpdate_Tick);
            tmrUpdate.Enabled = true;

            // set the form loaded flag to try
            loaded = true;

        }

        private void btnSetConfig_Click(object sender, EventArgs e) {

            // check if there is a main thread
            if (mainThread != null) {

                // configure the system
                if (mainThread.configureSystem()) {
                    // configured correctly

                    // initialize the system
                    mainThread.initializeSystem();

                }

                // update the main information
                updateMainInformation();

            }

        }

        private void btnStart_Click(object sender, EventArgs e) {

            // check if there is a main thread
            if (mainThread != null) {

                // start the system
                mainThread.start();

                // update the main information
                updateMainInformation();

            }
        }

        private void btnStop_Click(object sender, EventArgs e) {
            // check if there is a main thread
            if (mainThread != null) {

                // stop the system
                mainThread.stop();

                // update the main information
                updateMainInformation();

            }
        }

        private void btnEditConfig_Click(object sender, EventArgs e) {
            GUIConfig frmConfig = new GUIConfig();
            //mainThread.

            frmConfig.ShowDialog();
        }

    }

}
