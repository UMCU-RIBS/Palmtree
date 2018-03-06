/**
 * The MainBoot class
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
using System.Globalization;
using System.Threading;
using System.Windows.Forms;
using UNP.Core.Helpers;
using UNP.Core.Params;
using UNP.GUI;

namespace UNP.Core {

    /// <summary>
    /// The <c>MainBoot</c> class.
    /// 
    /// ...
    /// </summary>
    public static class MainBoot {

        private const int CLASS_VERSION = 1;

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

            // retrieve the current thread's culture as the initial culture
            CultureInfo culture = (CultureInfo)Thread.CurrentThread.CurrentCulture.Clone();

            // startup argument variables
            bool nogui = false;
            bool startupConfigAndInit = false;
            bool startupStartRun = false;
            string parameterFile = @"";
            string source = "";

            //args = new string[] { "-parameterfile", "UNPMenu_nexus.prm", "-source", "NexusSignal", "-startupConfigAndInit", "-startupStart" };
            //args = new string[] { "-parameterfile", "test_UNPMENU.prm", "-source", "KeypressSignal", "-startupConfigAndInit", "-startupStartRun" };
            //args = new string[] { "-source", "KeypressSignal", "-startupConfigAndInit", "-startupStart" };
            //args = new string[] { "-source", "KeypressSignal"};
            //args = new string[] { "-source", "KeypressSignal"};
            //args = new string[] { "-source", "KeypressSignal", "-language", "en" };
            //args = new string[] { "-language", "en-us" };

            // process startup arguments
            for (int i = 0; i < args.Length; i++) {
                // TODO: process following startup arguments 
                // - autosetconfig = 
                // - autostart = 
                // - source (GenerateSignal/KeypressSignal/PlaybackSignal) = 

                string argument = args[i].ToLower();

                // check if no gui should be shown
                if (argument == "-nogui")                       nogui = true;

                // check if the configuration and initialization should be done automatically at startup
                if (argument == "-startupconfigandinit")        startupConfigAndInit = true;

                // check if the run should be started automatically at startup
                if (argument == "-startupstartrun")             startupStartRun = true;

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

                // check if the language is given
                if (argument == "-language") {

                    // the next element should contain the culture name
                    if (args.Length >= i + 1 && !string.IsNullOrEmpty(args[i + 1])) {

                        try {

                            // overwrite the culture with the given argument, this will cause the respective language 
                            // resource file to be used (unless it does not exists, then it will revert to english)
                            culture = new CultureInfo(args[i + 1]);

                            // message
                            logger.Info("Language/culture set to '" + args[i + 1] + "'");

                        } catch (Exception) {

                            // with an exception the original culture object is ruined
                            // therefore re(clone) the current culture from the thread
                            culture = (CultureInfo)Thread.CurrentThread.CurrentCulture.Clone();

                            // message
                            logger.Error("Language argument given but unknown culture, using the local culture (language)");

                        }

                    }

                }

            }

            // adjust decimal seperator and group seperator
            culture.NumberFormat.NumberDecimalSeparator = ".";
            culture.NumberFormat.NumberGroupSeparator = "";
            culture.NumberFormat.NumberGroupSizes = new int[] { 0 };

            // set the culture for every future thread and the current thread
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;

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

            // check if no (valid) source was given
            if (string.IsNullOrEmpty(source)) {

                // allow the user to choose a source by popup
                source = ListMessageBox.ShowSingle("Source", sources);
                if (string.IsNullOrEmpty(source))  return;

            }

            // GenerateSignal/KeypressSignal/PlaybackSignal
            Type sourceType = Type.GetType(source);

            // name this thread
            if (Thread.CurrentThread.Name == null)
                Thread.CurrentThread.Name = "Main Thread";
            
            // create the main (control) object
            MainThread mainThread = new MainThread(startupConfigAndInit, startupStartRun, nogui);

            // variable for the GUI interface object
            GUIMain gui = null;

            // check if the GUI should be loaded/shown
            if (!nogui) {

                // create the GUI interface object
                gui = new GUIMain();

                // create a GUI (as a separate process)
                // and pass a reference to the experiment for the GUI to pull information from and push commands to the experiment object
                Thread thread = new Thread(() => {

                    // name this thread
                    if (Thread.CurrentThread.Name == null) {
                        Thread.CurrentThread.Name = "GUI Thread";
                    }

                    // setup the GUI
                    Application.EnableVisualStyles();

                    // start and run the GUI
                    //try {
                        Application.Run(gui);
                    //} catch (Exception e) {
                        //logger.Error("Exception in GUI: " + e.Message);
                    //}

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

            // check if a parameter file was given to load at startup
            if (!String.IsNullOrEmpty(parameterFile)) {

                // load parameter file to the applications parametersets
                ParameterManager.loadParameterFile(parameterFile, ParameterManager.getParameterSets());

            }
            
            // start the main thread
            // (do it here, so the UNPThead uses main to run on)
            mainThread.run();

            // check if there is a gui, and close it by delegate
            if (gui != null) {
                gui.closeDelegate();
                gui = null;
            }
            
            // stop all the winforms (Informs all message pumps that they must terminate, and then closes all application windows after the messages have been processed.)
            // TODO: sometimes sticks on this, ever since GUIVisualization was added
            Application.Exit();

            // exit the environment
            Environment.Exit(0);

        }

    }

}
