using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using UNP.GUI;

namespace UNP.Core {
    
    public static class MainBoot {

        private static Logger logger = LogManager.GetLogger("MainBoot");

        public static void Run(string[] args, Type applicationType) {
            
            // TODO: check if all dependencies exists
            // TODO: also 32/64 bit (freetype or other dlls)

            // TODO: Add startup arguments (args)
            // - nogui = start without GUI
            // - parameter file =
            // - autosetconfig = 
            // - autostart = 
            // - source (GenerateSignal/KeypressSignal/PlaybackSignal) = 

            //(GenerateSignal/KeypressSignal/PlaybackSignal)
            Type sourceType = Type.GetType("UNP.Sources.GenerateSignal");
            //Type sourceType = Type.GetType("UNP.Sources.KeypressSignal");
            //Type sourceType = Type.GetType("UNP.Sources.NexusSignal");

            // name this thread
            if (Thread.CurrentThread.Name == null)
                Thread.CurrentThread.Name = "Main Thread";

            // create the main (control) object
            MainThread mainThread = new MainThread();

            // create the GUI interface object
            GUIMain gui = new GUIMain(mainThread);
            
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
                logger.Info("GUI (thread) stopped");

            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            
            // wait for the GUI to start or a maximum amount of 5 seconds (5.000 / 50 = 100)
            int waitCounter = 100;
            while (!gui.isLoaded() && waitCounter > 0) {
                Thread.Sleep(50);
                waitCounter--;
            }
            
            // message
            if (Environment.Is64BitProcess)
                logger.Info("Processes are run in a 64 bit environment");
            else
                logger.Info("Processes are run in a 32 bit environment");
            

            // setup and initialize the pipeline
            mainThread.initPipeline(sourceType, applicationType);

            // debug: load 
            mainThread.loadDebugConfig();

            // TODO: load parameter file

            /*
            // TODO:
            // debug - auto configure
            if (mainThread.configureSystem()) {
                // successfully configured

                // initialize
                mainThread.initializeSystem();

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

}
