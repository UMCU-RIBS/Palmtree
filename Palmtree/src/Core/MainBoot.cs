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
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Threading;
using System.Windows.Forms;
using Palmtree.Core.Helpers;
using Palmtree.Core.Params;
using Palmtree.GUI;
using Palmtree.Core.Helpers.AppConfig;

namespace Palmtree.Core {

    /// <summary>
    /// The <c>MainBoot</c> class.
    /// 
    /// ...
    /// </summary>
    public static class MainBoot {

        private const int CLASS_VERSION = 2;

        private static Logger logger = LogManager.GetLogger("MainBoot");

        // the available sources
        private static string[][] availableSources = new string[][] {
                                                        new string[] { "GenerateSignal",            "Palmtree.Sources.GenerateSignal"},
                                                        new string[] { "KeypressSignal",            "Palmtree.Sources.KeypressSignal"},
                                                        new string[] { "NexusSignal",               "Palmtree.Sources.NexusSignal" },
                                                        new string[] { "PlaybackSignal",            "Palmtree.Sources.PlaybackSignal" }
                                                    };

        // the available filters
        private static string[][] availableFilters = new string[][] {
                                                        new string[] { "AdaptationFilter",          "Palmtree.Filters.AdaptationFilter" },                                    
                                                        new string[] { "ClickTranslatorFilter",     "Palmtree.Filters.ClickTranslatorFilter" },
                                                        new string[] { "KeySequenceFilter",         "Palmtree.Filters.KeySequenceFilter" },
                                                        new string[] { "NormalizerFilter",          "Palmtree.Filters.NormalizerFilter" },
                                                        new string[] { "RedistributionFilter",      "Palmtree.Filters.RedistributionFilter"},
                                                        new string[] { "ThresholdClassifierFilter", "Palmtree.Filters.ThresholdClassifierFilter" },
                                                        new string[] { "TimeSmoothingFilter",       "Palmtree.Filters.TimeSmoothingFilter"},
                                                        new string[] { "FlexKeySequenceFilter",     "Palmtree.Filters.FlexKeySequenceFilter" }
                                                    };

        public static int getClassVersion() {
            return CLASS_VERSION;
        }

        public static void Run(string[] args, Type applicationType) {

            // check the endianness
            if (!BitConverter.IsLittleEndian) {
                logger.Error("This software assumes a little-endianness OS, your OS uses a big-endian system. Exiting");
                return;
            }

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
            List<string[]> filters = new List<string[]>();

            // See if we are providing a graphical user interface (GUI)
            // 
            // Note: done seperately because it will detemine whether error messages are 
            //       also show as message dialogs, instead of only written to the log file
            for (int i = 0; i < args.Length; i++) {
                string argument = args[i].ToLower();
                if (argument == "-nogui")                       nogui = true;
            }

            // process all startup arguments
            for (int i = 0; i < args.Length; i++) {
                string argument = args[i].ToLower();
                
                // check if the configuration and initialization should be done automatically at startup
                if (argument == "-startupconfigandinit")        startupConfigAndInit = true;

                // check if the run should be started automatically at startup
                if (argument == "-startupstartrun")             startupStartRun = true;

                // check if the source is given
                if (argument == "-source") {

                    // the next element should be the source, try to retrieve
                    if (args.Length >= i + 1 && !string.IsNullOrEmpty(args[i + 1])) {

                        // check if valid source
                        int[] sourceIdx = ArrayHelper.jaggedArrayCompare(args[i + 1], availableSources, null, new int[]{0}, true);
                        if (sourceIdx == null) {

                            string errMessage = "Could not find the source module '" + args[i + 1] + "' requested as startup argument), choose one of the following: " + ArrayHelper.jaggedArrayJoin(", ", availableSources, 0);
                            logger.Error(errMessage);
                            if (!nogui)
                                MessageBox.Show(errMessage, "Requested source module not found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            
                            return;

                        } else {
                            
                            // store the source-type as a string
                            source = availableSources[sourceIdx[0]][1];

                        }
                        
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
            
            // try to read the pipeline modules (source and filters) from the <AppName>.Config file
            try {
                
                // if no valid source was parsed from a command-line argument, then try to retrieve a valid source from the <appname>.config file
                KeyValueConfigurationElement appConfigSource = PipelineConfigurationSection.Pipeline.Source;
                if (string.IsNullOrEmpty(source) && !string.IsNullOrEmpty(appConfigSource.Value)) {
                    int[] sourceIdx = ArrayHelper.jaggedArrayCompare(appConfigSource.Value, availableSources, null, new int[]{0}, true);
                    if (sourceIdx != null) {
    
                        // store the source-type as a string
                        source = availableSources[sourceIdx[0]][1];
                        
                    } else {

                        string errMessage = "Could not find the source module '" + appConfigSource.Value + "' requested in the <appname>.config file, choose one of the following: " + ArrayHelper.jaggedArrayJoin(", ", availableSources, 0);
                        logger.Error(errMessage);
                        if (!nogui)
                                MessageBox.Show(errMessage, "Requested source module not found", MessageBoxButtons.OK, MessageBoxIcon.Error);

                        return;

                    }
                }

                // if no valid filters were parsed from a command-line argument, then try to retrieve valid filters from the <appname>.config file
                List<FilterConfigurationElement> appConfigFilters = PipelineConfigurationSection.Pipeline.Filters.All;
                if (filters.Count == 0 && appConfigFilters.Count > 0) {
                    for (int iFilter = 0; iFilter < appConfigFilters.Count; iFilter++) {
                        string filterName = appConfigFilters[iFilter].Name;
                        string filterType = appConfigFilters[iFilter].Type;
                                   
                        // check if valid filter
                        int[] filterIdx = ArrayHelper.jaggedArrayCompare(filterType, availableFilters, null, new int[]{0}, true);
                        if (filterIdx == null) {

                            string errMessage = "A filter-module of the type '" + filterType + "' was requested in the <appname>.config file, but could not be found. The following filter-types are available: " + ArrayHelper.jaggedArrayJoin(", ", availableFilters, 0);
                            logger.Error(errMessage);
                            if (!nogui)
                                MessageBox.Show(errMessage, "Requested filter module not found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;

                        } else {
                            
                            // add the filter as a string array with two values (filter-type and filter-name)
                            filters.Add(new string[] {availableFilters[filterIdx[0]][1], filterName});
                        }

                    }
                }

            } catch (Exception e) {
                string errMessage = "Error while reading the Pipeline config section in the <appname>.config file: " + e.Message;
                logger.Error(errMessage);
                if (!nogui)
                    MessageBox.Show(errMessage, "Error in the <appname>.config file", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            // if no source passed using command-line or <appname>.config file then allow the user to choose a source by popup
            if (string.IsNullOrEmpty(source)) {
                source = ListMessageBox.ShowSingle("Source", availableSources);
                if (string.IsNullOrEmpty(source))  return;
            }


            //
            // Setup and initialize Palmtree
            // 

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
            if (mainThread.initPipeline(source, filters, applicationType)) {

                // check if a parameter file was given to load at startup
                if (!String.IsNullOrEmpty(parameterFile)) {

                    // load parameter file to the applications parametersets
                    ParameterManager.loadParameterFile(parameterFile, ParameterManager.getParameterSets());

                }
            
                // continue to run as the main thread
                mainThread.run();

            }

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
