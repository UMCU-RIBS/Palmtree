/**
 * The MainThread class
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
//#define DEBUG_KEEP_SAMPLES                                                    // causes the thread not to remove the sample-package after processing causing infinite sample-packages to process, used to test performance of the pipeline (filters + application)
//#define DEBUG_SAMPLES_LOG_PERFORMANCE                                         // log the performance (display the amount of sample-packages processed)
//#define DEBUB_INCOMING_SAMPLES_PER_SECOND                                     // log the incoming samples per second
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Palmtree.Applications;
using Palmtree.Filters;
using Palmtree.Core.Helpers;
using Palmtree.Core.Params;
using Palmtree.Sources;
using Palmtree.Plugins;
using Palmtree.Core.DataIO;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace Palmtree.Core {

    /// <summary>
    /// The <c>MainThread</c> class.
    /// 
    /// ...
    /// </summary>
    public class MainThread {

        private const int CLASS_VERSION                     = 3;

        private static Logger logger                        = LogManager.GetLogger("MainThread");

        private const int threadLoopDelayNoProc             = 200;                  // thread loop delay when not processing (1000ms / 5 run times per second = rest 200ms)
        private const int threadLoopDelayProc               = 100;	                // thread loop delay while processing (1000ms / 5 run times per second = rest 200ms); Sleep will be interrupted when a sample comes in; And no sleep when there are more samples waiting for processing
        private const int sampleBufferSize                  = 10000;                // the size (and maximum samples) of the sample buffer/que 

        private static ManualResetEvent loopManualResetEvent= new ManualResetEvent(false); // Manual reset event to call the WaitOne event on (this allows - in contrast to the sleep wait - to cancel the wait period at any point when a new sample comes in) 

        private static bool running                         = false;                // flag to define if the Palmtree thread is running (setting to false will stop the experiment thread) 
        private static bool process                         = false;                // flag to define if the thread is allowed to process samples
        private static bool stopDataDelegateFlag            = false;                // stop data delegate flag (if true will stop the data class in the main loop)

        private static bool startupConfigAndInit            = false;
        private static bool startupStartRun                 = false;
        private static bool noGUI                           = false;
        private static bool systemConfigured                = false;                // 
        private static bool systemInitialized               = false;                // 
        private static bool started                         = false;                // flag to hold whether the system is in a started or stopped state
        private static Object lockStarted                   = new Object();         // threadsafety lock for starting/stopping the system and processing

        private long nextOutputTime                         = 0;                    // the next timestamp to ouput message in the mainloop (done only once per second because logger holds up the main thread)
        private static ISource source                       = null;                 //
        private static List<IFilter> filters                = new List<IFilter>();  //
        private static IApplication application             = null;                 // reference to the application
        private static List<IPlugin> plugins                = new List<IPlugin>();  //

        private static double[][] samplePackagesBuffer      = new double[sampleBufferSize][]; // the sample-package buffer in which sample-packages are queud
        private static int sampleBufferAddIndex             = 0;                    // the index where in the (ring) sample-package buffer the next sample-package will be added
        private static int sampleBufferReadIndex            = 0;                    // the index where in the (ring) sample-package buffer the next sample-package that should be read is
        private static int numSamplePackagesInBuffer        = 0;                    // the number of added but unread samples-package in the (ring) sample-packages buffer
        private static int numSamplePackagesDiscarded       = 0;                    // count the number of discarded sample-packages

        #if DEBUG_SAMPLES_LOG_PERFORMANCE || DEBUB_INCOMING_SAMPLES_PER_SECOND
            private static long samplePackagesProcessed         = 0;                // the number of sample-packages that were processed since the last reset
            private static long samplePackagesIn                = 0;                // the number of sample-packages that came in since the last reset
            private static long samplePackagesInNextOutputTime  = 0;                // the next timestamp at which to display the number of sample-packages that came in
        #endif


        /**
         * MainThread constructor
         * 
         */
        public MainThread(bool startupConfigAndInit, bool startupStartRun, bool noGUI) {
            
            // transfer the startup flags to local variables
            MainThread.startupConfigAndInit = startupConfigAndInit;
            MainThread.startupStartRun = startupStartRun;
            MainThread.noGUI = noGUI;

        }

        public static int getClassVersion() {
            return CLASS_VERSION;
        }
        
        public bool initPipeline(string sourceType, List<string[]> filtersTypes, Type applicationType) {
            
            // Add an event handler to allow for "interface/header" assemblies. 
            AppDomain currentDomain = AppDomain.CurrentDomain;
            currentDomain.AssemblyResolve += new ResolveEventHandler(resolveEventHandler);    
            
            // constuct the Data static class (this makes sure that the Data parameterset is created for configuration)
            Data.construct();

            // retrieve the source type and create and instance
            try {
                source = (ISource)Activator.CreateInstance(Type.GetType(sourceType));
            } catch (Exception) {
                logger.Error("Unable to create a source instance of '" + sourceType + "'");
                return false;
            }

            // create filters
            if (filtersTypes != null || filtersTypes.Count > 0) {
                for (int i = 0; i < filtersTypes.Count; i++) {
                    try {
                        IFilter filter = (IFilter)Activator.CreateInstance(Type.GetType(filtersTypes[i][0]), filtersTypes[i][1]);
                        filters.Add(filter);
                    } catch (Exception) {
                        logger.Error("Unable to create a filter instance of '" + filtersTypes[i][0] + "'");
                        return false;
                    }
                }
            }

            // create the application
            try {
                application = (IApplication)Activator.CreateInstance(applicationType);
            } catch (Exception e) {
                logger.Error("Unable to create an application instance of '" + applicationType.Name + "' (" + e.Message + ")");
            }

            // create/add plugins
            //plugins.Add(new WindowsSensorsPlugin("WindowsSensorsPlugin", "wsp"));

            // return success
            return true;

        }

        /**
         * Configures the system (the source, pipeline filters and application)
         **/
        public static bool configureSystem() {
            
            
            // configure the data object
            if (!Data.configure()) {

                // mesage
                logger.Error("An error occured while configuring the data class, stopped");

                // return failure and go no further
                return false;

            }

            // configure the plugins
            for (int i = 0; i < plugins.Count; i++) {

                // configure the filter
                if (!plugins[i].configure()) {

                    // message
                    logger.Error("An error occured while configuring plugin '" + plugins[i].getName() + "', stopped");

                    // flag as not configured
                    systemConfigured = false;

                    // return failure and go no further
                    return false;

                }
                
            }

            // configure source (this will also give the output format information)
            SamplePackageFormat packageFormat = null;
            if (source != null) {

                // configure the source
                if (!source.configure(out packageFormat)) {

                    // message and return failure
                    logger.Error("An error occured while configuring source, stopped");
                    return false;

                }

            }

            // register the pipeline input based on the output format of the source
            Data.registerPipelineInput(packageFormat);
            
            // configure the filters
            for (int i = 0; i < filters.Count; i++) {

                // create a local variable to temporarily store the output format of the filter in
                // (will be given in the configure step)
                SamplePackageFormat outputFormat = null;

                // configure the filter
                if (!filters[i].configure(ref packageFormat, out outputFormat)) {
                    
                    // message
                    logger.Error("An error occured while configuring filter '" + filters[i].GetType().Name + "', stopped");


                    // flag as not configured
                    systemConfigured = false;

                    // return failure and go no further
                    return false;

                }

                // store the output filter as the input filter for the next loop (filter)
                packageFormat = outputFormat;

            }

            // configure the application
            if (application != null) {
                
                // configure the application
                if (!application.configure(ref packageFormat)) {

                    // message
                    logger.Error("An error occured while configuring application, stopped");

                    // return failure and go no further
                    return false;

                }

            }

            // flag as configured
            systemConfigured = true;

            // return success
            return true;

        }

        /**
         * Returns whether the system was configured
         * 
         * @return Whether the system was configured
         */
        public static bool isSystemConfigured() {
            return systemConfigured;
	    }

        /**
         * 
         * 
         * 
         **/
        public static void initializeSystem() {

            // check if the system was configured and initialized
            if (!systemConfigured) {

                // message
                logger.Error("Could not initialize the system, first make sure it is configured correctly");

                return;

            }

            // initialize the plugins
            for (int i = 0; i < plugins.Count; i++)     plugins[i].initialize();

            // initialize source, filter and view
            if (source != null)                         source.initialize();
            for (int i = 0; i < filters.Count; i++)     filters[i].initialize();
            if (application != null)                    application.initialize();

            // flag as initialized
            systemInitialized = true;

        }

	    /**
	     * Returns whether the system was initialized
	     * 
	     * @return Whether the system was initialized
	     */
	    public static bool isSystemInitialized() {
            return systemInitialized;
	    }


	    /**
	     * Start the system (source, filters and application)
	     */
        public static void start() {

            // if the system is stopping, then do not try to start anything anymore
            // (not locked for performance/deadlock reasons, as it concerns the main loop)
            if (!running)   return;

            // check if the system was configured and initialized
            if (!systemConfigured || !systemInitialized) {

                // message
                logger.Error("Could not start system, first configure and initialize");

                return;

            }
            
            // lock for thread safety
            lock(lockStarted) {

                // check if the system is not started (return if already started)
                if (started) {

                    // message
                    logger.Error("Could not start system, system is already started");

                    return;

                }
                
                // start the data
                Data.start();
                
                // start the plugins, the application module and the filter modules
                for (int i = 0; i < plugins.Count; i++)     plugins[i].start();
                if (application != null)                    application.start();
                for (int i = 0; i < filters.Count; i++)     filters[i].start();

                // reset the sample-packages buffer counters
                lock (samplePackagesBuffer.SyncRoot) {
                    sampleBufferAddIndex = 0;
                    sampleBufferReadIndex = 0;
                    numSamplePackagesInBuffer = 0;
                    numSamplePackagesDiscarded = 0;
                }

                // allow the main loop to process sample-packages
                process = true;

                // interrupt the 'noproc' waitloop , allowing the loop to continue (in case it was waiting the sample interval)
                // causing it to fall into the 'proc' loop
                loopManualResetEvent.Set();
                
                // start the source
                // (start last so everything set to receive and process samples)
                if (source != null)     source.start();
                
                // flag the system as started
                started = true;

            }

        }

        /**
         * Stop the system (source, filters and application)
         */
        public static void stop(bool stopDataImmediate) {
            
            // lock for thread safety
            lock(lockStarted) {

                // check if the system is started
                if (started) {

                    // stop the source
                    // (stop first so no new samples are coming in)
                    if (source != null)                         source.stop();

                    // stop the processing of samples in the main loop
                    process = false;

                    // stop the filter modules, the application module and the plugins
                    for (int i = 0; i < filters.Count; i++)     filters[i].stop();
                    if (application != null)                    application.stop();
                    for (int i = 0; i < plugins.Count; i++)     plugins[i].stop();

                    // check if the data class should be stopped immediately or later (by the loop)
                    if (stopDataImmediate) {

                        // stop the data immediately
                        Data.stop();

                    } else {

                        // set a flag to stop the data class later
                        stopDataDelegateFlag = true;

                    }

                    // flag the system as stopped
                    started = false;

                }

            }

	    }
        

        /**
         * Returns whether the system is started
         * 
         * @return Whether the system is started
         */
        public static bool isStarted() {
            return started;
	    }

        /**
         * Returns whether the system is runing
         * 
         * @return Whether the system is running
         */
        public static bool isRunning() {
            return running;
	    }



        public void run() {

            // debug messages
            #if (DEBUG_KEEP_SAMPLES)
                logger.Error("DEBUG_KEEP_SAMPLES is enabled");
            #endif

            // check if there is no application instance
            if (application == null)    logger.Error("No application instance could be created");

            // log message
            logger.Debug("Thread started");

            // local variables
            double[] samplePackage = null;

            #if (DEBUG_SAMPLES_LOG_PERFORMANCE)
                samplePackagesProcessed = 0;
            #endif

            // flag as running
            running = true;

            // check if the sytem should be configured and initialized at startup
            if (startupConfigAndInit) {

                if (MainThread.configureSystem()) {
                    // successfully configured

                    // initialize
                    MainThread.initializeSystem();

                } else {

                    // check if there is no gui
                    if (noGUI) {

                        // message and do not start the process
                        MessageBox.Show("Error during configuration, check log file", "Error during configuration", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        running = false;

                    }

                }

            } else {

                // check if there is no gui
                if (noGUI) {

                    // message and do not start the process
                    MessageBox.Show("Error during startup, without a GUI and automatic startup arguments there is no way to run the program, check startup arguments", "Error during startup", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    running = false;

                }

            }

            // check if the system should be started at startup (and is ready to start)
            if (startupStartRun && systemConfigured && systemInitialized) {
                
                // start the run
                MainThread.start();

            }

            // set an initial output time
            nextOutputTime = Stopwatch.GetTimestamp() + Stopwatch.Frequency;

            // loop while running
            while (running) {
                
                // lock for thread safety
                // (very much needed, else it will make calls to other modules and indirectly the data module, which already might have stopped at that point)
                lock(lockStarted) {

                    // check if we are processing samples
                    if (process) {
                        // processing
                        
                        // see if there samples in the queue, pick the sample for processing
                        samplePackage = null;
                        lock(samplePackagesBuffer.SyncRoot) {
                            
                            // performance watch
                            if (Stopwatch.GetTimestamp() > nextOutputTime) {

                                // check if there were sample discarded the last second
                                if (numSamplePackagesDiscarded > 0) {

                                    // message missed
                                    logger.Error("Missed " + numSamplePackagesDiscarded + " samples, because the sample buffer was full, the roundtrip of a sample through the filter and application takes longer than the sample frequency of the source");

                                    // reset counter
                                    numSamplePackagesDiscarded = 0;

                                }

                                // 
                                #if (DEBUG_SAMPLES_LOG_PERFORMANCE)
                                    logger.Info("----------");
                                    logger.Info("samples processed: " + samplePackagesProcessed);
                                    logger.Info("number of samples left in buffer: " + numSamplePackagesInBuffer);
                                    samplePackagesProcessed = 0;
                                #endif

                                // set the next time to output messages
                                nextOutputTime = Stopwatch.GetTimestamp() + Stopwatch.Frequency;

                            }


                            // check if there are samples in the buffer to process
                            if (numSamplePackagesInBuffer > 0) {

                                // retrieve the sample to process (pointer to from array)
                                samplePackage = samplePackagesBuffer[sampleBufferReadIndex];

                                #if (!DEBUG_KEEP_SAMPLES)

                                    // set the read index to the next item
                                    sampleBufferReadIndex++;
                                    if (sampleBufferReadIndex == sampleBufferSize) sampleBufferReadIndex = 0;

                                    // decrease the itemcounter as it will be processed (to make space for another item in the samples buffer)
                                    numSamplePackagesInBuffer--;

                                #endif
                                
                            }

                        }

                        // check if there is a sample to process
                        if (samplePackage != null) {
                            double[] output = null;

                            // Announce the sample at the beginning of the pipeline
                            Data.pipelineProcessingStart();

                            // keep?
                            for (int i = 0; i < plugins.Count; i++) {
                                plugins[i].preFiltersProcess();
                            }

                            // log data (the 'LogPipelineInputStreamValue' function will not log the samples if pipeline input logging is disabled)
                            Data.logPipelineInputStreamValues(samplePackage);

                            // TODO:
                            // log for visualization (the 'LogVisualizationStreamValue' function will discard the sample if visualization is disabled)
                            //Data.logVisualizationStreamValue(sample);

                            // process the sample (filters)
                            for (int i = 0; i < filters.Count; i++) {
                                filters[i].process(samplePackage, out output);
                                samplePackage = output;
                                output = null;
                            }

                            // keep? 
                            for (int i = 0; i < plugins.Count; i++) {
                                plugins[i].postFiltersProcess();
                            }
                            
                            // process the sample (application)
                            if (application != null)    application.process(samplePackage);
                            
                            // Announce the sample at the end of the pipeline
                            Data.pipelineProcessingEnd();

                            // flush all plugin buffers to file
                            Data.writePluginData(-1);
                            
                            #if (DEBUG_SAMPLES_LOG_PERFORMANCE)
                                samplePackagesProcessed++;
                            #endif

                        }

                        // sleep (when there are no samples) to allow for other processes
                        if (threadLoopDelayProc != -1 && numSamplePackagesInBuffer == 0) {

                            // reset the manual reset event, so it is sure to block on the next call to WaitOne
                            // 
                            // Note: not using AutoResetEvent because it could happen that .Set is called while not in WaitOne yet, when
                            // using AutoResetEvent this will cause it to skip the next WaitOne call
                            loopManualResetEvent.Reset();

                            // Sleep wait
                            loopManualResetEvent.WaitOne(threadLoopDelayProc);      // using WaitOne because this wait is interruptable (in contrast to sleep)


                        }

                    } else {
                        // not processing

                        // reset the manual reset event, so it is sure to block on the next call to WaitOne
                        // 
                        // Note: not using AutoResetEvent because it could happen that .Set is called while not in WaitOne yet, when
                        // using AutoResetEvent this will cause it to skip the next WaitOne call
                        loopManualResetEvent.Reset();

                        // Sleep wait
                        loopManualResetEvent.WaitOne(threadLoopDelayNoProc);      // using WaitOne because this wait is interruptable (in contrast to sleep)

                    }
                    
                }   // end lock

                // check if a stop delegate wants the processing to stop
                if (stopDataDelegateFlag) {

                    // stop the data
                    Data.stop();

                    // reset flag
                    stopDataDelegateFlag = false;

                }

            }   // end loop (running)

            // stop the source, filter, view and data (if these were running)
            stop(true);

            // destroy the source
            if (source != null) {
                source.destroy();
                source = null;
            }

            // destroy the filters
            for (int i = 0; i < filters.Count; i++)     filters[i].destroy();

            // destroy the view
            if (application != null)                    application.destroy();

            // destroy the plugins
            for (int i = 0; i < plugins.Count; i++)     plugins[i].destroy();

            // destroy the data class
            Data.destroy();

            // log message
            logger.Debug("Thread stopped");

        }

        // 
        // Called when a new sample comes from the source, this method is called directly from the source object.
        // 
        // Not using delegate (or interface) events since there would be only one receiver, being this function called.
        // A direct method call will suffice and is probably even faster.
        public static void eventNewSample(double[] sample) {
            
            #if DEBUB_INCOMING_SAMPLES_PER_SECOND
                samplePackagesIn++;
                if (Stopwatch.GetTimestamp() > samplePackagesInNextOutputTime) {

                    logger.Info("----------");
                    logger.Info("new sample-packages in (last 1s): " + samplePackagesIn);

                    samplePackagesIn = 0;
                    samplePackagesInNextOutputTime = Stopwatch.GetTimestamp() + Stopwatch.Frequency;

                }

            #endif


            lock(samplePackagesBuffer.SyncRoot) {
                
                // check if the buffer is full
                if (numSamplePackagesInBuffer == sampleBufferSize) {

                    // count the number of discarded sample-packages
                    // (warning, do not message here about discarded samples, this will take to much time and this function should return as quickly as possible)
                    numSamplePackagesDiscarded++;
                    
                    // immediately return, discard sample
                    return;

                }
                
                // add the sample-package at the pointer location, and increase the pointer (or loop the pointer around)
                samplePackagesBuffer[sampleBufferAddIndex] = sample;
                sampleBufferAddIndex++;
                if (sampleBufferAddIndex == sampleBufferSize) sampleBufferAddIndex = 0;

                // increase the counter that tracks the number of sample-packages in the array
                numSamplePackagesInBuffer++;

                // interrupt the loop wait. The loop will reset the wait lock (so it will wait again upon the next WaitOne call)
                // this will make sure the newly set sample rate interval is applied in the loop
                loopManualResetEvent.Set();

            }

        }


        /**
         * Called when the GUI is dispatched and closed
         * 
         * Not using delegate (or interface) events since there would be only one receiver, being this function called.
         */
        public static void eventGUIClosed() {

            // stop the program from running
            running = false;

        }

        /**
         * Called when the View is dispatched and closed
         * 
         * Not using delegate (or interface) events since there would be only one receiver, being this function called.
         */
        public static void eventViewClosed() {
            logger.Error("eventViewClosed");
            // stop the program from running
            running = false;

        }

        /**
         * Static function to return the number of samples per second according the source
         * Used by Parameters to convert seconds to samples
         **/
        public static double getPipelineSamplesPerSecond() {
            
            // check 
            if (source != null) {

                // retrieve the number of samples per second
                return source.getOutputSamplesPerSecond();

            } else {
                
                // message
                logger.Error("Trying to retrieve the samples per second before a source was set, returning 0");

                // return 0
                return 0;

            }
            
        }

        public static ISource getSource() {
            return source;
        }

        public static List<IFilter> getFilters() {
            return filters;
        }

        public static IApplication getApplication() {
            return application;
        }

        /**
         * Static function to return 
         **/
        private static Parameters getFilterParameters(string filterName) {

            // find the filter, and return a reference to its parameters
            for (int i = 0; i < filters.Count; i++) {
                if (filters[i].getName().Equals(filterName)) {
                    return filters[i].getParameters();
                }
            }

            // message
            logger.Error("Filter '" + filterName + "' could not be found, returning null");

            // return failure
            return null;

        }

        /**
         * Static function to return 
         **/
        public static Parameters getFilterParametersClone(string filterName) {

            // find the filter, and return a clone of its parameters
            for (int i = 0; i < filters.Count; i++) {
                if (filters[i].getName().Equals(filterName)) {
                    return filters[i].getParameters().clone();
                }
            }

            // message
            logger.Error("Filter '" + filterName + "' could not be found, returning null");

            // return failure
            return null;

        }

        /// <summary>Re-configure and/or reset the configration parameters of the filter (defined in the newParameters argument) on-the-fly.</summary>
        /// <param name="newParameters">Parameter object that defines the configuration parameters to be set. Also used to indicate which filter to re-configure and/or reset.</param>
        /// <param name="resetOption">Filter reset options. 0 will reset the minimum; 1 will perform a complete reset of filter information. > 1 for custom resets.</param>
        /// <returns>A boolean, either true for a succesfull re-configuration, or false upon failure</returns>
        public static void configureRunningFilter(Parameters newParameters, int resetOption = 0) {
            if (newParameters == null)  return;
            configureRunningFilter(newParameters.ParamSetName, newParameters, resetOption);    // use the name of the parameter set as the filterName (should be equal)
        }

        /// <summary>Re-configure and/or reset the configration parameters of the indicated filter (by name) on-the-fly.</summary>
        /// <param name="filterName">The name of the filter to re-configure and/or reset.</param>
        /// <param name="newParameters">Parameter object that defines the configuration parameters to be set. Set to NULL to leave the configuration parameters untouched.</param>
        /// <param name="resetOption">Filter reset options. 0 will reset the minimum; 1 will perform a complete reset of filter information. > 1 for custom resets.</param>
        /// <returns>A boolean, either true for a succesfull re-configuration, or false upon failure</returns>
        public static bool configureRunningFilter(string filterName, Parameters newParameters, int resetOption = 0) {

            // find the filter, and return a reference to its paremeters
            IFilter filter = null;
            for (int i = 0; i < filters.Count; i++) {
                if (filters[i].getName().Equals(filterName)) {
                    filter = filters[i];
                    break;
                }
            }
            
            // check if the filter could not be found
            if (filter == null) {

                // message
                logger.Error("Filter '" + filterName + "' could not be found, no filter parameters were adjusted");

                // return without action
                return false;

            }
            
            // apply the new parameters to the running filter
            return filter.configureRunningFilter(newParameters, resetOption);

        }

        /// <summary>Re-configure and/or reset the configration parameters of a array of filters (defined in the newParameters argument) on-the-fly.</summary>
        /// <param name="newParameters">Parameter objects that define the configuration parameters to be set.</param>
        /// <param name="resetOption">Filter reset options. 0 will reset the minimum; 1 will perform a complete reset of filter information. > 1 for custom resets.</param>
        /// <returns>A boolean, either true for a succesfull re-configuration, or false upon failure</returns>
        public static void configureRunningFilter(Parameters[] newParameters, int resetOption = 0) {
            for (int i = 0; i < newParameters.Length; i++)      
                configureRunningFilter(newParameters[i], resetOption);
        }

        public static void configureRunningFilter(Parameters[] newParameters, int[] resetOption) {
            for (int i = 0; i < newParameters.Length; i++)      
                configureRunningFilter(newParameters[i], (i < resetOption.Length ? resetOption[i] : 0));
        }

        // This handler is called only when the common language runtime tries to bind to the assembly and fails.
        // Using this handler we can intercept the - expected - unsuccesfull runtime binding of Palmtree to the
        // DLLs of the "interface/header" assemblies (ISummitAPI, ...) and substitute them with loading the
        // actual 3th party DLLs, so binding them purely by <assemblyName>.dll; Effectively having the assembly
        // projects function as C header files would for libraries. In order for this to work, the DLLs from the
        // "interface/header" assemblies should never be included in the Palmtree outputput folder.
        // 
        // Note: The sole reason for using stubs/interfaces is because we cannot include some 3th party DLLS with
        //       the project, as a result the open-source project would not compile without them. Palmtree supports
        //       multiple devices, therefore "interface/header" assemblies are used.
        private static Assembly resolveEventHandler(object sender, ResolveEventArgs args) {
            AssemblyName assemblyName = new AssemblyName(args.Name);
            
            // if a resources assembly is loaded, return null to let the default handler resolve it
            // Note: Since .NET Framework 4, the ResolveEventHandler event is raised for all assemblies, including resource assemblies
            if (assemblyName.Name.EndsWith(".resources")) {
                return null;
            }

            // define the assemblies that are resolved by name (case-insensitive)
            // optionally provide the public-key for additional checks
            string[][] rebindAssemblies = new string[][] {
                                                        new string[] { "Medtronic.SummitAPI",           ""},
                                                        new string[] { "Medtronic.TelemetryM",          ""},
                                                        new string[] { "Medtronic.NeuroStim.Olympus",   ""}
                                                    };

            // check if the assembly is setup to be resolved
            int[] bindIdx = ArrayHelper.jaggedArrayCompare(assemblyName.Name, rebindAssemblies, null, new int[]{0}, true);
            if (bindIdx == null) {
                logger.Error("Assembly '" + assemblyName.Name + "' is not setup to be rebound to a DLL. Assembly resolve failed.");
                return null;
            }

            // check if a dll file with the assembly name exists in the current assembly's path
            string folderPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string assemblyPath = Path.Combine(folderPath, assemblyName.Name + ".dll");
            if (!File.Exists(assemblyPath)) {
                logger.Error("Could not find assembly DLL: '" + assemblyPath + ". Resolve failed.");
                return null;
            }

            // try to load the assembly
            Assembly assembly = null;
            try {
                assembly = Assembly.LoadFrom(assemblyPath);

                // TODO: optional extra checks to verify 
                // add check to publickey, to ensure they are indeed the genuine 3th Party DLLs
            
                // success
                return assembly;

           } catch (FileLoadException e) {
                logger.Error("Error while loading asssembly '" + assemblyName.Name + "': " + e.Message);
            } catch (FileNotFoundException e) {
                logger.Error("Error while loading asssembly '" + assemblyName.Name + "': " + e.Message);
            } catch (BadImageFormatException e) {
                logger.Error("Error while loading asssembly '" + assemblyName.Name + "': " + e.Message);
            };
            
            return null;
        }
        
    }

}
