using NLog;
using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using UNP.Applications;
using UNP.Core;
using UNP.Core.Helpers;
using UNP.Core.Params;
using System.Windows.Forms;
using System.Runtime.InteropServices;


namespace UNP.Applications {

    public class ExCommunicator : IExternalApplication {

        [DllImport("user32.dll")]                                                   // import dll for setting focus to external application
        static extern bool SetForegroundWindow(IntPtr wndH);                        // function for setting focus

        private const int CLASS_VERSION = 0;
        private const string CLASS_NAME = "Communicator";

        private static Logger logger = LogManager.GetLogger("Communicator");
        private static Parameters parameters = ParameterManager.GetParameters("Communicator", Parameters.ParamSetTypes.Application);

        private uint inputChannels = 0;                                             // amount of input channels (coming in)
        private int mTaskInputChannel = 1;											// input channel

        private String path = null;                                                 // path to executable
        private bool started = false;						                        // flag to hold whether the external application is running
        Process CommunicatorProcess = null;                                         // process to start external application

        public ExCommunicator() {

            // define the parameters
            parameters.addParameter<String>(
                    "ExecutablePath",
                    "Path of Tobii Communicator executable",
                    "", "", @"C:\Windows\System32\notepad.exe");

            parameters.addParameter<int>(
                    "TaskInputChannel",
                    "Channel to use for spelling (1...n)",
                    "1", "", "1");
        }

        public int getClassVersion() {
            return CLASS_VERSION;
        }

        public String getClassName() {
            return CLASS_NAME;
        }

        public Parameters getParameters() {
            return parameters;
        }

        public bool configure(ref SampleFormat input) {

            // store the number of input channels that are coming in
            inputChannels = input.getNumberOfChannels();

            // check if the number of input channels is higher than 0
            if (inputChannels <= 0) {
                logger.Error("Number of input channels cannot be 0");
                return false;
            }

            // retrieve the input channel setting
            mTaskInputChannel = parameters.getValue<int>("TaskInputChannel");

            if (mTaskInputChannel < 1) {
                logger.Error("Invalid input channel, should be higher than 0 (1...n)");
                return false;
            }

            if (mTaskInputChannel > inputChannels) {
                logger.Error("Input should come from channel " + mTaskInputChannel + ", however only " + inputChannels + " channels are coming in");
                return false;
            }

            // retrieve path
            path = parameters.getValue<String>("ExecutablePath");

            // check if Tobii Communicator executable exists
            if (!File.Exists(path)) {
                logger.Error("Communicator executable not found, check path.");
                return false;
            } else {
                logger.Info("Communicator executable located.");
            }

            

            return true;
        }

        public void initialize() {

        }

        public void start() {

            // create new process 
            CommunicatorProcess = new Process();

            // start process
            try {
                CommunicatorProcess.StartInfo.FileName = path;
                CommunicatorProcess.Start();
                started = true;
                logger.Info("Communicator started.");
            } catch (Exception e) {
                logger.Error(CLASS_NAME + " can not be started (" + e.Message + ")");
            }

        }

        public void stop() {

            // Communicator has a button to quit when it receives a F13 keypress
            // TODO: make a parameter of the button to be used for closing so user can define it?
            SendKeys.SendWait("{F13}");
        }

        public bool isStarted() {
            return started;
        }

        public void process(double[] input) {

            // check if input contains a click, if so, send enter
            if (input[mTaskInputChannel-1] == 1) {
                
                // set focus to exernal application
                IntPtr windowHandle = CommunicatorProcess.MainWindowHandle;
                SetForegroundWindow(windowHandle);

                SendKeys.SendWait("{ENTER}"); }
        }

        public void destroy() {

        }

    }
}
