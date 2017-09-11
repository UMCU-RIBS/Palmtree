//#define DEBUG_SAMPLES                   // causes the thread not to remove the sample after processing causing infinite samples to process, used to test performance of the pipeline (filters + application)
//#define DEBUG_SAMPLES_LOG_PERFORMANCE   // log the performance (display the amount of samples processed)

using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using UNP.Applications;
using UNP.Filters;
using UNP.Core.Helpers;
using UNP.Core.Params;
using UNP.Sources;
using UNP.Plugins;
using UNP.Core.DataIO;

namespace UNP.Core {
    
    public class MainThread {

        private const int CLASS_VERSION = 0;

        private static Logger logger = LogManager.GetLogger("MainThread");

        private const int threadLoopDelayNoProc = 200;                              // thread loop delay when not processing (1000ms / 5 run times per second = rest 200ms)
        private const int threadLoopDelayProc = 100;	                            // thread loop delay while processing (1000ms / 5 run times per second = rest 200ms); Sleep will be interrupted when a sample comes in; And no sleep when there are more samples waiting for processing
        private const int sampleBufferSize = 10000;                                 // the size (and maximum samples) of the sample buffer/que 

        private static ManualResetEvent loopManualResetEvent = new ManualResetEvent(false);    // Manual reset event to call the WaitOne event on (this allows - in contrast to the sleep wait - to cancel the wait period at any point when a new sample comes in) 

        private static bool running = false;				                        // flag to define if the UNP thread is running (setting to false will stop the experiment thread) 
        private static bool process = false;                                        // flag to define if the thread is allowed to process samples

        private static bool startupConfigAndInit = false;
        private static bool startupStartRun = false;
        private static bool systemConfigured = false;                               // 
        private static bool systemInitialized = false;                              // 
        private static bool started = false;                                        // flag to hold whether the system is in a started or stopped state
        private static Object lockStarted = new Object();                           // threadsafety lock for starting/stopping the system and processing

        private long nextOutputTime = Stopwatch.GetTimestamp() + Stopwatch.Frequency;       // the next timestamp to ouput message in the mainloop (done only once per second because logger holds up the main thread)

        private static ISource source = null;                                       //
        private static List<IFilter> filters = new List<IFilter>();                 //
        private static IApplication application = null;                             // reference to the application
        private static List<IPlugin> plugins = new List<IPlugin>();                 //

        private static double[][] sampleBuffer = new double[sampleBufferSize][];    // the sample buffer in which samples are queud
        private static int sampleBufferAddIndex = 0;                                // the index where in the (ring) sample buffer the next sample will be added
        private static int sampleBufferReadIndex = 0;                               // the index where in the (ring) sample buffer the next sample that should be read is
        private static int numSamplesInBuffer = 0;                                  // the number of added but unread samples in the (ring) sample buffer
        private static int numSamplesDiscarded = 0;                                 // count the number of discarded samples


        /**
         * UNPThread constructor
         * 
         */
        public MainThread(bool startupConfigAndInit, bool startupStartRun) {

            // transfer the startup flags to local variables
            MainThread.startupConfigAndInit = startupConfigAndInit;
            MainThread.startupStartRun = startupStartRun;

        }

        public static int getClassVersion() {
            return CLASS_VERSION;
        }

        public void initPipeline(Type sourceType, Type applicationType) {
            
            // constuct the Data static class (this makes sure that the Data parameterset is created for configuration)
            Data.construct();
            
            // create a source
            try {
                source = (ISource)Activator.CreateInstance(sourceType);
                //Console.WriteLine("Created source instance of " + sourceType.Name);
            } catch (Exception) {
                logger.Error("Unable to create a source instance of '" + sourceType.Name + "'");
            }

            // create filters
            filters.Add(new RedistributionFilter("FeatureSelector"));
            filters.Add(new TimeSmoothingFilter("TimeSmoothing"));
            filters.Add(new AdaptationFilter("Adaptation"));
            filters.Add(new RedistributionFilter("LinearClassifier"));
            filters.Add(new KeySequenceFilter("KeySequence"));
            filters.Add(new ThresholdClassifierFilter("ThresholdClassifier"));
            filters.Add(new ClickTranslatorFilter("ClickTranslator"));
            filters.Add(new NormalizerFilter("Normalizer"));

            // create the application
            try {
                application = (IApplication)Activator.CreateInstance(applicationType);
                //Console.WriteLine("Created application instance of " + applicationType.Name);
            } catch (Exception e) {
                logger.Error("Unable to create an application instance of '" + applicationType.Name + "' (" + e.Message + ")");
            }

            // create/add plugins
            //plugins.Add(new WindowsSensorsPlugin("WindowsSensorsPlugin", "wsp"));

        }

        /**
         * Configures the system (the source, pipeline filters and application)
         **/
        public static bool configureSystem() {

            // configure the data object
            if (!Data.configure()) {

                // message
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
            SampleFormat tempFormat = null;
            if (source != null) {

                // configure the source
                if (!source.configure(out tempFormat)) {

                    // message
                    logger.Error("An error occured while configuring source, stopped");

                    // return failure and go no further
                    return false;

                }

            }

            // register the pipeline input based on the output format of the source
            Data.registerPipelineInput(tempFormat);
            
            // configure the filters
            for (int i = 0; i < filters.Count; i++) {

                // create a local variable to temporarily store the output format of the filter in
                // (will be given in the configure step)
                SampleFormat outputFormat = null;

                // configure the filter
                if (!filters[i].configure(ref tempFormat, out outputFormat)) {
                    
                    // message
                    logger.Error("An error occured while configuring filter '" + filters[i].GetType().Name + "', stopped");


                    // flag as not configured
                    systemConfigured = false;

                    // return failure and go no further
                    return false;

                }

                // store the output filter as the input filter for the next loop (filter)
                tempFormat = outputFormat;

            }

            // configure the application
            if (application != null) {
                
                // configure the application
                if (!application.configure(ref tempFormat)) {

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

                // start the plugins
                for (int i = 0; i < plugins.Count; i++)   plugins[i].start();

                // start the application
                if (application != null)    application.start();

                // start the filters
                for (int i = 0; i < filters.Count; i++)   filters[i].start();
                
                // allow the main loop to process samples
                process = true;

                // clear the samplesbuffer counter
                lock (sampleBuffer.SyncRoot) {
                    sampleBufferAddIndex = 0;
                    sampleBufferReadIndex = 0;
                    numSamplesInBuffer = 0;
                    numSamplesDiscarded = 0;
                }

                // interrupt the 'noproc' waitloop , allowing the loop to continue (in case it was waiting the sample interval)
                // casing it to fall into the 'proc' loop
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
        public static void stop() {

            // lock for thread safety
            lock(lockStarted) {

                // check if the system is started
                if (started) {

                    // stop the source
                    // (stop first so no new samples are coming in)
                    if (source != null)                         source.stop();

                    // stop the processing of samples in the main loop
                    process = false;

                    // stop the filters
                    for (int i = 0; i < filters.Count; i++)     filters[i].stop();

                    // stop the application
                    if (application != null)                    application.stop();

                    // stop the plugins
                    for (int i = 0; i < plugins.Count; i++)     plugins[i].stop();

                    // stop the data
                    Data.stop();

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
            #if (DEBUG_SAMPLES)
                logger.Error("DEBUG_SAMPLES is enabled");
            #endif

            // check if there is no application instance
            if (application == null)    logger.Error("No application instance could be created");

            // log message
            logger.Debug("Thread started");

            // local variables
            double[] sample = null;

            #if (DEBUG_SAMPLES_LOG_PERFORMANCE)

                long samplesProcessed = 0;

            #endif

            // check if the sytem should be configured and initialized at startup
            if (startupConfigAndInit) {

                if (MainThread.configureSystem()) {
                    // successfully configured

                    // initialize
                    MainThread.initializeSystem();
                    
                }

            }

            // flag as running
            running = true;

            // check if the system should be started at startup (and is ready to start)
            if (startupStartRun && systemConfigured && systemInitialized) {
                
                // start the run
                MainThread.start();

            }

            // loop while running
            while (running) {
                
                // lock for thread safety
                // (very much needed, else it will make calls to other modules and indirectly the data module, which already might have stopped at that point)
                lock(lockStarted) {

                    // check if we are processing samples
                    if (process) {
                        // processing

                        // see if there samples in the queue, pick the sample for processing
                        sample = null;
                        lock(sampleBuffer.SyncRoot) {

                            // performance watch
                            if (Stopwatch.GetTimestamp() > nextOutputTime) {

                                // check if there were sample discarded the last second
                                if (numSamplesDiscarded > 0) {

                                    // message missed
                                    logger.Error("Missed " + numSamplesDiscarded + " samples, because the sample buffer was full, the roundtrip of a sample through the filter and application takes longer than the sample frequency of the source");

                                    // reset counter
                                    numSamplesDiscarded = 0;

                                }


                                #if (DEBUG_SAMPLES_LOG_PERFORMANCE)

                                    // print
                                    logger.Info("----------");
                                    logger.Info("samplesProcessed: " + samplesProcessed);
                                    logger.Info("numberOfSamples: " + numSamplesInBuffer);

                                    // reset counter
                                    samplesProcessed = 0;

                                #endif

                                // set the next time to output messages
                                nextOutputTime = Stopwatch.GetTimestamp() + Stopwatch.Frequency;

                            }


                            // check if there are samples in the buffer to process
                            if (numSamplesInBuffer > 0) {

                                // retrieve the sample to process (pointer to from array)
                                sample = sampleBuffer[sampleBufferReadIndex];

                                #if (!DEBUG_SAMPLES)

                                    // set the read index to the next item
                                    sampleBufferReadIndex++;
                                    if (sampleBufferReadIndex == sampleBufferSize) sampleBufferReadIndex = 0;

                                    // decrease the itemcounter as it will be processed (to make space for another item in the samples buffer)
                                    numSamplesInBuffer--;

                                #endif
                                
                            }

                        }

                        // check if there is a sample to process
                        if (sample != null) {

                            double[] output = null;

                            // Announce the sample at the beginning of the pipeline
                            Data.sampleProcessingStart();

                            // debug 
                            for (int i = 0; i < plugins.Count; i++) {
                                plugins[i].preFiltersProcess();
                            }
                            
                            // loop through the pipeline input samples
                            for (int i = 0; i < sample.Length; i++) {

                                // log data (the 'LogPipelineInputStreamValue' function not log the sample if pipeline input logging is disabled)
                                Data.logPipelineInputStreamValue(sample[i]);

                                // log for visualization (the 'LogVisualizationStreamValue' function will discard the sample if visualization is disabled)
                                Data.logVisualizationStreamValue(sample[i]);

                            }

                            // process the sample (filters)
                            for (int i = 0; i < filters.Count; i++) {
                                filters[i].process(sample, out output);
                                sample = output;
                                output = null;
                            }

                            // debug 
                            for (int i = 0; i < plugins.Count; i++) {
                                plugins[i].postFiltersProcess();
                            }

                            // process the sample (application)
                            if (application != null)    application.process(sample);

                            // Announce the sample at the end of the pipeline
                            Data.sampleProcessingEnd();
                            
                            #if (DEBUG_SAMPLES_LOG_PERFORMANCE)
                                samplesProcessed++;
                            #endif

                        }

                        // sleep (when there are no samples) to allow for other processes
                        if (threadLoopDelayProc != -1 && numSamplesInBuffer == 0) {

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
                    
                }

            }

            // stop the source, filter, view and data (if these were running)
            stop();

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
            
            lock(sampleBuffer.SyncRoot) {
                
                // check if the buffer is full
                if (numSamplesInBuffer == sampleBufferSize) {

                    // count the number of discarded samples
                    // (warning, do not message here about discarded samples, this will take to much time and this function should return as quickly as possible)
                    numSamplesDiscarded++;
                    
                    // immediately return, discard sample
                    return;

                }
                
                // add the sample at the pointer location, and increase the pointer (or loop the pointer around)
                sampleBuffer[sampleBufferAddIndex] = sample;
                sampleBufferAddIndex++;
                if (sampleBufferAddIndex == sampleBufferSize) sampleBufferAddIndex = 0;

                // increase the counter that tracks the number of samples in the array
                numSamplesInBuffer++;

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
         * Static function to return the number of samples per second according the source
         * Used by Parameters to convert seconds to samples
         **/
        public static double SamplesPerSecond() {
            
            // check 
            if (source != null) {

                // retrieve the number of samples per second
                return source.getSamplesPerSecond();

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

        /**
         * Static function to adjust a filter's settings on the fly (during runtime)
         * 
         * 
         **/
        public static void configureRunningFilter(Parameters newParameters, bool resetFilter = false) {
            configureRunningFilter(newParameters.ParamSetName, newParameters, resetFilter);    // use the name of the parameter set as the filterName (should be equal)
        }
        public static void configureRunningFilter(string filterName, Parameters newParameters, bool resetFilter = false) {

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
                return;

            }
            
            // apply the new parameters to the running filter
            filter.configureRunningFilter(newParameters, resetFilter);

        }
        public static void configureRunningFilter(Parameters[] newParameters, bool resetFilter = false) {
            for (int i = 0; i < newParameters.Length; i++)      configureRunningFilter(newParameters[i], resetFilter);
        }
        public static void configureRunningFilter(Parameters[] newParameters, bool[] resetFilter) {
            for (int i = 0; i < newParameters.Length; i++)      configureRunningFilter(newParameters[i], (i < resetFilter.Length ? resetFilter[i] : false));
        }

    }

}
