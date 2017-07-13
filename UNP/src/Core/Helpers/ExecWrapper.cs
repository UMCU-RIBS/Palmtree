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

    public class ExecWrapper {

		private static Logger logger = LogManager.GetLogger("ExecWrapper");

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]                                                   // import dll for setting focus to external application
        private static extern bool SetForegroundWindow(IntPtr wndH);                // function for setting focus
		
        private string filepath = null;                                             // path to executable
        private bool started = false;						                        // flag to hold whether the external application is running
        Process process = null;                                         			// process to start external application

        public ExecWrapper(string filepath) {
			
			// check if executable exists
            if (!File.Exists(filepath)) {
                logger.Warn("Given filepath '" + filepath + "' ");
			}
			
			// transfer the filepath
			this.filepath = filepath;
			
        }


        public void start() {
			
            // check if executable exists
            if (!File.Exists(filepath)) {

                // message
                logger.Error("Executable '" + filepath + "' not found, check path");

                // return directly
                return;

			}
            
            try {

                // create new process 
                process = new Process();

                // start process
                process.StartInfo.FileName = filepath;
                process.Start();

                // flag as started
                started = true;
                
                // message
				logger.Info("Executable '" + filepath + "' started.");
				
            } catch (Exception e) {

                // message error
                logger.Error("Executable '" + filepath + "' could not be started (" + e.Message + ")");

            }

        }

        public void close() {
			
			
			
            // Communicator has a button to quit when it receives a F13 keypress
            // TODO: make a parameter of the button to be used for closing so user can define it?
            SendKeys.SendWait("{F13}");
			// Dit naar UNP menu
			
			
        }

        public bool isStarted() {
            return started;
        }

		public void sendKeyDown() {

            // send key
            SendKeys.SendWait("{ENTER}");

		}
		
        public bool hasFocus() {

            // get the handle of the window currently on the foreground
            IntPtr foregroundHandle = GetForegroundWindow();

            // get the handle of the process
            IntPtr processHandle = process.MainWindowHandle;

            // return whether the process handle is the foreground handle
            return (foregroundHandle == processHandle);

        }

		public void setFocus() {

            // TODO: check if started

            // get the handle of the process
            IntPtr windowHandle = process.MainWindowHandle;

            // set focus to exernal application
            SetForegroundWindow(windowHandle);

		}
		
    }

}
