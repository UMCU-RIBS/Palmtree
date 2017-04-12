//#define DEBUG_SAMPLES                   // causes the thread not to remote the sample after processing causing infinite samples to process, used to test performance
//#define DEBUG_SAMPLES_LOG_PERFORMANCE   // log the performance

using NLog;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using UNP.applications;
using UNP.filters;
using UNP.helpers;
using UNP.sources;
using UNP.views;


namespace UNP {

    public class MainThread {

        private static Logger logger = LogManager.GetLogger("MainThread");

        public const int threadLoopDelayNoProc = 30;                        // thread loop delay when not processing (1000ms / 5 run times per second = rest 200ms)
        public const int threadLoopDelayProc = -1;		                    // thread loop delay while processing (1000ms / 5 run times per second = rest 200ms)
        private bool running = true;				                        // flag to define if the UNP thread is still running (setting to false will stop the experiment thread) 
        private bool process = false;                                       // flag to define if the thread is allowed to process samples

        private bool systemConfigured = false;                              // 
        private bool systemInitialized = false;                             // 
        private bool started = false;                                       // flag to hold whether the system is in a started or stopped state
        private Object lockStarted = new Object();                          // threadsafety lock for starting/stopping the system and processing


        private static ISource source = null;                               //
        private List<IFilter> filters = new List<IFilter>();                //
        private IApplication application = null;                            // reference to the view, used to pull information from and push commands to

        const int sampleBufferSize = 10000;                                 // the size (and maximum samples) of the sample buffer/que 
        private double[][] sampleBuffer = new double[sampleBufferSize][];   // the sample buffer in which samples are queud
        private int sampleBufferAddIndex = 0;                               // the index where in the (ring) sample buffer the next sample will be added
        private int sampleBufferReadIndex = 0;                              // the index where in the (ring) sample buffer of the next sample that should be read
        private int numberOfSamples = 0;                                    // the number of added but unread samples in the (ring) sample buffer

        /**
         * UNPThread constructor
         * 
         * Creates the source, pipeline filters and application
         * 
         */
        public MainThread() {

            // initialially set as not configured
            systemConfigured = false;
            systemInitialized = false;

	    }

        public void initPipeline(Type applicationType) {

            // create a source
            source = new GenerateSignal(this);

            // create filters
            filters.Add(new TimeSmoothingFilter());
            filters.Add(new AdaptationFilter());
            filters.Add(new ThresholdClassifierFilter());
            filters.Add(new ClickTranslatorFilter());

            // create the application
            Console.WriteLine("applicationType " + applicationType.Name);
            /*
            if (!String.IsNullOrEmpty(applicationClass)) {
                try {
                    application = (IApplication)Activator.CreateInstance(Type.GetType(applicationClass));
                    logger.Info("Created an application instance of the class '" + applicationClass + "'");
                } catch (Exception e) {
                    logger.Error("Unable to create an application instance of the class '" + applicationClass + "'");
                    Console.WriteLine("---");
                    Console.WriteLine(e.Message);
                    Console.WriteLine("---");
                }
            }
            */
            application = (IApplication)Activator.CreateInstance(applicationType);

        }

        public void loadDebugConfig() {





            // (optional/debug) set/load the parameters
            // (the parameter list has already been filled by the constructors of the source, filters and views)
            Parameters sourceParameters = source.getParameters();
            sourceParameters.setValue("SourceChannels", 2);



            Parameters timeSmoothingParameters = filters[0].getParameters();
            timeSmoothingParameters.setValue("EnableFilter", true);

            Parameters adaptationParameters = filters[1].getParameters();
            adaptationParameters.setValue("EnableFilter", true);
            adaptationParameters.setValue("WriteIntermediateFile", false);
            adaptationParameters.setValue("Adaptation", "1 1");
            adaptationParameters.setValue("InitialChannelMeans", "497.46 362.58");
            adaptationParameters.setValue("InitialChannelStds", "77.93 4.6");
            adaptationParameters.setValue("BufferLength", "9s");
            adaptationParameters.setValue("BufferDiscardFirst", "1s");
            adaptationParameters.setValue("AdaptationMinimalLength", "5s");
            adaptationParameters.setValue("ExcludeStdThreshold", "0.85 2.7");


            /*
            Parameters.setParameterValue("SourceChannels", "2");
            
            Parameters.setParameterValue("SF_WriteIntermediateFile", "0");
            
            Parameters.setParameterValue("AF_WriteIntermediateFile", "0");
            */

        }

        /**
         * Configures the system (the source, pipeline filters and application)
         **/
        public bool configureSystem() {

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
            
            // configure the filters
            for (int i = 0; i < filters.Count(); i++) {

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
            if (application != null)    application.configure(ref tempFormat);


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
        public bool isSystemConfigured() {
            return systemConfigured;
	    }

        /**
         * 
         * 
         * 
         **/
        public void initializeSystem() {

            // check if the system was configured and initialized
            if (!systemConfigured) {

                // message
                logger.Error("Could not initialize the system, first make sure it is configured correctly");

                return;

            }

            // initialize source, filter and view
            if (source != null)                         source.initialize();
            for (int i = 0; i < filters.Count(); i++)   filters[i].initialize();
            if (application != null)                    application.initialize();

            // flag as initialized
            systemInitialized = true;

        }

	    /**
	     * Returns whether the system was initialized
	     * 
	     * @return Whether the system was initialized
	     */
	    public bool isSystemInitialized() {
            return systemInitialized;
	    }


	    /**
	     * Start the system (source, filters and application)
	     */
        public void start() {

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

                // start the application
                if (application != null)    application.start();


                // start the filters
                for (int i = 0; i < filters.Count(); i++) filters[i].start();

                // allow the main loop to process samples
                process = true;

                // clear the samplesbuffer counter
                lock(sampleBuffer.SyncRoot) {
                    sampleBufferAddIndex = 0;
                    sampleBufferReadIndex = 0;
                    numberOfSamples = 0;
                }
                
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
        public void stop() {

            // lock for thread safety
            lock(lockStarted) {

                // check if the system is started
                if (started) {

                    // stop the source
                    // (stop first so no new samples are coming in)
                    if (source != null)     source.stop();

                    // stop the processing of samples in the main loop
                    process = false;

                    // stop the filters
                    for (int i = 0; i < filters.Count(); i++) filters[i].stop();

                    // stop the application
                    if (application != null)    application.stop();

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
        public bool isStarted() {
            return started;
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
            long time = Stopwatch.GetTimestamp() + Stopwatch.Frequency;
            long counter = 0;
            double[] sample = null;
            long samplesProcessed = 0;

            // loop while running
            while (running) {

                // performance watch
                if (Stopwatch.GetTimestamp() > time) {

                    #if(DEBUG_SAMPLES_LOG_PERFORMANCE)

                        logger.Info("----------");
                        logger.Info("samplesProcessed: " + samplesProcessed);
                        logger.Info("numberOfSamples: " + numberOfSamples);
                        logger.Info("tick counter: " + counter);
                        //logger.Info("sample[0]: " + sample[0]);

                    #endif

                    counter = 0;
                    time = Stopwatch.GetTimestamp() + Stopwatch.Frequency;
                    samplesProcessed = 0;

                }
                counter++;

                // lock for thread safety
                lock(lockStarted) {

                    // check if the system should process samples
			        if (process) {
                        
                        // see if there samples in the que, pick the sample for processing
                        sample = null;
                        lock(sampleBuffer.SyncRoot) {

                            if (numberOfSamples > 0) {

                                // retrieve the sample to process (pointer to from array)
                                sample = sampleBuffer[sampleBufferReadIndex];

                                #if (!DEBUG_SAMPLES)

                                    // set the read index to the next item
                                    sampleBufferReadIndex++;
                                    if (sampleBufferReadIndex == sampleBufferSize) sampleBufferReadIndex = 0;

                                    // decrease the itemcounter as it will be processed (to make space for another item in the samples buffer)
                                    numberOfSamples--;

                                #endif

                            }

                        }

                        // check if there is a sample to process
                        if (sample != null) {
                            
                            double[] output = null;

                            // process the sample (filters)
                            for (int i = 0; i < filters.Count(); i++) {
                                filters[i].process(sample, out output);
                                sample = output;
                                output = null;
                            }

                            // process the sample (application)
                            if (application != null)    application.process(sample);

                            // add one to the number of samples 
                            samplesProcessed++;

                        }

                    }

                    // check if the thread is still running
                    if (running) {

                        // check if we are processing samples
                        if (process) {
                            // processing

			                // sleep (when there are no samples) to allow for other processes
			                if (threadLoopDelayProc != -1 && numberOfSamples == 0) {
                                Thread.Sleep(threadLoopDelayProc);
			                }


                        } else {
                            // not processing

                            // sleep to allow for other processes
                            Thread.Sleep(threadLoopDelayNoProc);

                        }

                    }

                }

            }


            // stop and destroy the source
            if (source != null) {
                source.destroy();
                source = null;
            }

            // stop and destoy the filters
            for (int i = 0; i < filters.Count(); i++) filters[i].destroy();

            // stop and destroy the view
            if (application != null)    application.destroy();

            // log message
            logger.Debug("Thread stopped");

        }


        public void eventNewSample(double[] sample) {
            
            lock(sampleBuffer.SyncRoot) {

                // check if the buffer is full
                if (numberOfSamples == sampleBufferSize) {

                    // immediately return, discard sample
                    return;

                }

                // add the sample at the pointer location, and increase the pointer (or loop the pointer around)
                sampleBuffer[sampleBufferAddIndex] = sample;
                sampleBufferAddIndex++;
                if (sampleBufferAddIndex == sampleBufferSize) sampleBufferAddIndex = 0;

                // increase the counter that holds the number of elements of the array
                numberOfSamples++;

            }

        }


        /**
         * event called when the GUI is dispatched and closed
         */
        public void eventGUIClosed() {

            // stop the program from running
            running = false;

        }

        /**
         * Static function to return the number of samples per second according the source
         * Used by Parameters to convert seconds to samples
         **/
        public static int SamplesPerSecond() {
            
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

    }

}
