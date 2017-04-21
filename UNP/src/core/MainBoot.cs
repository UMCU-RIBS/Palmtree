using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace UNP.Core {
    
    public static class MainBoot {

        private static Logger logger = LogManager.GetLogger("MainBoot");

        public static void Run(Type applicationType) {

            // name this thread
            if (Thread.CurrentThread.Name == null)
                Thread.CurrentThread.Name = "Main Thread";

            // create the main (control) object
            MainThread mainThread = new MainThread();

            // create the GUI interface object
            GUI gui = new GUI(mainThread);
            
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
            mainThread.initPipeline(applicationType);

            // debug: load 
            mainThread.loadDebugConfig();

            /*
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
            Application.Exit();

            // exit the environment
            Environment.Exit(0);

        }

    }

}
