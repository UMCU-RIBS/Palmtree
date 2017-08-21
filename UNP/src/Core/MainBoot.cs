using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using UNP.Core.Params;
using UNP.GUI;

namespace UNP.Core {

    public static class MainBoot {

        private const int CLASS_VERSION = 0;

        private static Logger logger = LogManager.GetLogger("MainBoot");

        // the available sources
        private static string[][] sources = new string[][] {
                                                new string[] { "GenerateSignal",    "UNP.Sources.GenerateSignal"},
                                                new string[] { "KeypressSignal",    "UNP.Sources.KeypressSignal"},
                                                new string[] { "NexusSignal",       "UNP.Sources.NexusSignal" },
                                                new string[] { "PlaybackSignal",    "UNP.Sources.PlaybackSignal" }
                                            };

        public static int getClassVersion() {
            return CLASS_VERSION;
        }

        public static void Run(string[] args, Type applicationType) {

            // TODO: check if all dependencies exists
            // TODO: also 32/64 bit (freetype or other dlls)

            // startup argument variables
            bool nogui = false;
            bool startupConfigAndInit = false;
            bool startupStart = false;
            string parameterFile = "";
            string source = "";

            //args = new string[] { "-parameterfile", "test_UNPMENU.prm", "-source", "KeypressSignal", "-startupConfigAndInit", "-startupStart" };

            // process startup arguments
            for (int i = 0; i < args.Length; i++) {
                // TODO: process following startup arguments 
                // - autosetconfig = 
                // - autostart = 
                // - source (GenerateSignal/KeypressSignal/PlaybackSignal) = 

                string argument = args[i].ToLower();

                // check if no gui should be shown
                if (argument == "-nogui")                   nogui = true;

                // check if no gui should be shown
                if (argument == "-startupconfigandinit")    startupConfigAndInit = true;
                
                // check if no gui should be shown
                if (argument == "-startupstart")            startupStart = true;

                // check if the source is given
                if (argument == "-source") {

                    // the next element should be the source, try to retrieve
                    if (args.Length >= i + 1 && !string.IsNullOrEmpty(args[i + 1])) {
                        source = args[i + 1];
                    }

                }

                // check if the parameterfile is given
                if (argument == "-parameterfile") {

                    // the next element should be the parameter file, try to retrieve
                    if (args.Length >= i + 1 && !string.IsNullOrEmpty(args[i + 1])) {
                        parameterFile = args[i + 1];
                    }

                }

            }

            // check if a source is given as argument
            if (!string.IsNullOrEmpty(source)) {

                // loop throug the available source to check the given source can be found
                bool sourceFound = false;
                for (int i = 0; i < sources.Length; i++) {
                    if (string.Compare(source, sources[i][0], true) == 0) {

                        // flag as found
                        sourceFound = true;

                        // update the source to the type name
                        source = sources[i][1];

                        // stop looping
                        break;

                    }
                }

                // check if the source was not found
                if (!sourceFound) {

                    // build a list of valid sources to give as startup source argument
                    string sourceList = "";
                    for (int i = 0; i < sources.Length; i++) {
                        if (i != 0) sourceList += ", ";
                        sourceList += sources[i][0];
                    }

                    // message
                    logger.Error("Could not find the source that was given as startup source argument ('" + source + "'), use one of the following: " + sourceList);

                    // empty the source
                    source = "";

                }
            }

            // debug
            //source = "UNP.Sources.GenerateSignal";
            //source = "UNP.Sources.KeypressSignal";
            //source = "UNP.Sources.NexusSignal";
            //source = "UNP.Sources.PlaybackSignal";

            // check if no (valid) source was given
            if (string.IsNullOrEmpty(source)) {

                // allow the user to choose a source by popup
                source = SourceMessageBox.Show(sources);
                if (string.IsNullOrEmpty(source))  return;

            }

            //(GenerateSignal/KeypressSignal/PlaybackSignal)
            Type sourceType = Type.GetType(source);

            // name this thread
            if (Thread.CurrentThread.Name == null)
                Thread.CurrentThread.Name = "Main Thread";
            
            // create the main (control) object
            MainThread mainThread = new MainThread();

            // check if the GUI should be loaded/shown
            if (!nogui) {

                // create the GUI interface object
                GUIMain gui = new GUIMain();

                // create a GUI (as a separate process)
                // and pass a reference to the experiment for the GUI to pull information from and push commands to the experiment object
                Thread thread = new Thread(() => {

                    // name this thread
                    if (Thread.CurrentThread.Name == null) {
                        Thread.CurrentThread.Name = "GUI Thread";
                    }

                    // setup the GUI
                    Application.EnableVisualStyles();

                    // start the GUI
                    Application.Run(gui);

                    // message
                    logger.Info("GUI (thread) started");

                });
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();

                // wait for the GUI to start or a maximum amount of 5 seconds (5.000 / 50 = 100)
                int waitCounter = 100;
                while (!gui.isLoaded() && waitCounter > 0) {
                    Thread.Sleep(50);
                    waitCounter--;
                }

            }

            // message versions
            logger.Info("MainBoot version " + MainBoot.getClassVersion());
            logger.Info("MainThread version " + MainThread.getClassVersion());


            // message
            if (Environment.Is64BitProcess)
                logger.Debug("Processes are run in a 64 bit environment");
            else
                logger.Debug("Processes are run in a 32 bit environment");


            // setup and initialize the pipeline
            mainThread.initPipeline(sourceType, applicationType);

            // debug: load 
            mainThread.loadDebugConfig();

            // check if a parameter file was given to load at startup
            if (!String.IsNullOrEmpty(parameterFile)) {

                // load parameter file to the applications parametersets
                ParameterManager.loadParameterFile(parameterFile, ParameterManager.getParameterSets());

            }
            /*
            // check if the sytem should be configured and initialized at statup
            if (startupConfigAndInit) {

                // TODO: max, quick and dirty
                // start a thread that fires after
                Thread thread = new Thread(() => {

                    Thread.Sleep(1000);

                    if (MainThread.configureSystem()) {
                        // successfully configured

                        // initialize
                        MainThread.initializeSystem();

                        if (startupStart) MainThread.start();

                }
                });
                thread.Start();
                
            }
            */
            // start the main thread
            // (do it here, so the UNPThead uses main to run on)
            mainThread.run();

            // stop all the winforms (Informs all message pumps that they must terminate, and then closes all application windows after the messages have been processed.)
            // TODO: sometimes sticks on this, ever since GUIVisualization was added
            Application.Exit();

            // exit the environment
            Environment.Exit(0);

        }

    }

    public class SourceMessageBox : Form {

        private string[][] sources = null;
        public string source = "";

        private System.Windows.Forms.ListBox lstSources;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Button btnExit;

        public SourceMessageBox(string[][] sources) {
            this.sources = sources;

            this.lstSources = new System.Windows.Forms.ListBox();
            this.btnOK = new System.Windows.Forms.Button();
            this.btnExit = new System.Windows.Forms.Button();
            this.SuspendLayout();

            this.lstSources.FormattingEnabled = true;
            this.lstSources.ItemHeight = 16;
            this.lstSources.Location = new System.Drawing.Point(4, 5);
            this.lstSources.Name = "listBox1";
            this.lstSources.Size = new System.Drawing.Size(287, 212);
            this.lstSources.TabIndex = 0;
            this.lstSources.DoubleClick += new System.EventHandler(delegate (object sender, EventArgs e) {
                btnOK.PerformClick();
            });
            for (int i = 0; i < sources.Length; i++) {
                this.lstSources.Items.Add(sources[i][0]);
            }

            

            this.btnOK.Location = new System.Drawing.Point(12, 228);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(101, 26);
            this.btnOK.TabIndex = 1;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(delegate (object sender, EventArgs e) {
                if (lstSources.SelectedIndex == -1) {
                    MessageBox.Show("Select a source from the list to continue...", "Select a source", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    return;
                }
                source = sources[lstSources.SelectedIndex][1];
                this.DialogResult = DialogResult.OK;
            });


            this.btnExit.Location = new System.Drawing.Point(179, 228);
            this.btnExit.Name = "btnExit";
            this.btnExit.Size = new System.Drawing.Size(101, 26);
            this.btnExit.TabIndex = 2;
            this.btnExit.Text = "Exit";
            this.btnExit.UseVisualStyleBackColor = true;
            this.btnExit.Click += new System.EventHandler(delegate (object sender, EventArgs e) {
                this.DialogResult = DialogResult.Cancel;

            });


            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(292, 262);
            this.Controls.Add(this.lstSources);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.btnExit);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Margin = new System.Windows.Forms.Padding(4);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "DlgSource";
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Source";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(delegate (object sender, FormClosingEventArgs e) {
                if (e.CloseReason == CloseReason.UserClosing) {
                    this.DialogResult = DialogResult.Cancel;
                }
            });
            
            this.ResumeLayout(false);

        }

        public static string Show(string[][] sources) {
            using (var form = new SourceMessageBox(sources)) {
                if (form.ShowDialog() == DialogResult.OK)   return form.source;
                return "";
            }
        }

    }

}
