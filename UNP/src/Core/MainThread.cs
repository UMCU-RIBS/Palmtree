//#define DEBUG_SAMPLES                   // causes the thread not to remove the sample after processing causing infinite samples to process, used to test performance
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
using UNP.Applications;
using UNP.Filters;
using UNP.Core.Helpers;
using UNP.Sources;
using UNP.Views;
using UNP.Core.Params;
using System.Windows.Forms;
using System.Xml;
using System.IO;

namespace UNP.Core {

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
        private static List<IFilter> filters = new List<IFilter>();         //
        private static IApplication application = null;                            // reference to the view, used to pull information from and push commands to

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

        public void initPipeline(Type sourceType, Type applicationType) {

            // constuct the Data static class (this makes sure that the Data parameterset is created for configuration)
            Data.Construct();

            // create a source
            try {
                source = (ISource)Activator.CreateInstance(sourceType, this);
                Console.WriteLine("Created source instance of " + sourceType.Name);
            } catch (Exception) {
                logger.Error("Unable to create a source instance of '" + sourceType.Name + "'");
            }

            // create filters
            filters.Add(new TimeSmoothingFilter("TimeSmoothing"));
            filters.Add(new AdaptationFilter("Adaptation"));
            filters.Add(new KeySequenceFilter("KeySequence"));
            filters.Add(new ThresholdClassifierFilter("ThresholdClassifier"));
            filters.Add(new ClickTranslatorFilter("ClickTranslator"));
            filters.Add(new NormalizerFilter("Normalizer"));

            // create the application
            try {
                application = (IApplication)Activator.CreateInstance(applicationType);
                Console.WriteLine("Created application instance of " + applicationType.Name);
            } catch (Exception) {
                logger.Error("Unable to create an application instance of '" + applicationType.Name + "'");
            }

        }

        public void loadDebugConfig() {


            // (optional/debug) set/load the parameters
            
            
            Parameters sourceParameters = source.getParameters();
            sourceParameters.setValue("Channels", 2);
            //sourceParameters.setValue("SampleRate", 5.0);
            //sourceParameters.setValue("Keys", "F,G;1,2;1,1;-1,-1");
            
            Parameters timeSmoothingParameters = getFilterParameters("TimeSmoothing");
            timeSmoothingParameters.setValue("EnableFilter", true);
            timeSmoothingParameters.setValue("LogDataStreams", false);
            double[][] bufferWeights = new double[2][];     // first dimensions is the colums, second dimension is the rows
            for (int i = 0; i < bufferWeights.Length; i++)  bufferWeights[i] = new double[] { 0.7, 0.5, 0.2, 0.2, 0 };
            timeSmoothingParameters.setValue("BufferWeights", bufferWeights);

            Parameters adaptationParameters = getFilterParameters("Adaptation");
            adaptationParameters.setValue("EnableFilter", true);
            adaptationParameters.setValue("LogDataStreams", true);
            adaptationParameters.setValue("Adaptation", "1 1");
            adaptationParameters.setValue("InitialChannelMeans", "497.46 362.58");
            adaptationParameters.setValue("InitialChannelStds", "77.93 4.6");
            adaptationParameters.setValue("BufferLength", "9s");
            adaptationParameters.setValue("BufferDiscardFirst", "1s");
            adaptationParameters.setValue("AdaptationMinimalLength", "5s");
            adaptationParameters.setValue("ExcludeStdThreshold", "0.85 2.7");

            Parameters keysequenceParameters = getFilterParameters("KeySequence");
            keysequenceParameters.setValue("EnableFilter", true);
            keysequenceParameters.setValue("LogDataStreams", false);
            keysequenceParameters.setValue("Threshold", 0.5);
            keysequenceParameters.setValue("Proportion", 0.7);
            bool[] sequence = new bool[] { true, true, true, true };
            keysequenceParameters.setValue("Sequence", sequence);
            
            Parameters thresholdParameters = getFilterParameters("ThresholdClassifier");
            thresholdParameters.setValue("EnableFilter", true);
            thresholdParameters.setValue("LogDataStreams", false);
            double[][] thresholds = new double[4][];        // first dimensions is the colums, second dimension is the rows
            thresholds[0] = new double[] { 1 };
            thresholds[1] = new double[] { 1 };
            thresholds[2] = new double[] { 0.45 };
            thresholds[3] = new double[] { 1 };
            thresholdParameters.setValue("Thresholds", thresholds);

            Parameters clickParameters = getFilterParameters("ClickTranslator");
            clickParameters.setValue("EnableFilter", true);
            clickParameters.setValue("LogDataStreams", false);
            clickParameters.setValue("ActivePeriod", "1s");
            clickParameters.setValue("ActiveRateClickThreshold", ".5");
            clickParameters.setValue("RefractoryPeriod", "3.6s");

            Parameters normalizerParameters = getFilterParameters("Normalizer");
            normalizerParameters.setValue("EnableFilter", true);
            normalizerParameters.setValue("LogDataStreams", false);
            normalizerParameters.setValue("NormalizerOffsets", "0 0");
            normalizerParameters.setValue("NormalizerGains", "1 1");

            /*
            Parameters unpmenuParameters = application.getParameters();
            unpmenuParameters.setValue("WindowLeft", 0);
            unpmenuParameters.setValue("WindowTop", 0);
            unpmenuParameters.setValue("WindowWidth", 800);
            unpmenuParameters.setValue("WindowHeight", 600);
            unpmenuParameters.setValue("WindowRedrawFreqMax", 60);
            unpmenuParameters.setValue("WindowBackgroundColor", "0;255;0");
            */
            /*
            Parameters followParameters = application.getParameters();
            followParameters.setValue("WindowLeft", 0);
            followParameters.setValue("WindowTop", 0);
            followParameters.setValue("WindowWidth", 800);
            followParameters.setValue("WindowHeight", 600);
            followParameters.setValue("WindowRedrawFreqMax", 60);
            followParameters.setValue("WindowBackgroundColor", "0");
            followParameters.setValue("TaskFirstRunStartDelay", "5s");
            followParameters.setValue("TaskStartDelay", "10s");
            followParameters.setValue("TaskInputChannel", 1);
            followParameters.setValue("TaskInputSignalType", 0);
            followParameters.setValue("TaskShowScore", true);
            followParameters.setValue("CursorSize", 4.0);
            followParameters.setValue("CursorColorRule", 0);
            followParameters.setValue("CursorColorMiss", "204;0;0");
            followParameters.setValue("CursorColorHit", "204;204;0");
            followParameters.setValue("CursorColorHitTime", "2s");
            followParameters.setValue("CursorColorHit", "170;0;170");
            followParameters.setValue("CursorColorEscapeTime", "2s");
            followParameters.setValue("Targets", "25,25,25,75,75,75;50,50,50,50,50,50;2,2,2,3,5,7");
            followParameters.setValue("TargetTextures", "images\\sky.bmp,images\\sky.bmp,images\\sky.bmp,images\\grass.bmp,images\\grass.bmp,images\\grass.bmp");
            followParameters.setValue("TargetYMode", 3);
            followParameters.setValue("TargetWidthMode", 1);
            followParameters.setValue("TargetHeightMode", 1);
            followParameters.setValue("TargetSpeed", 120);
            followParameters.setValue("NumberTargets", 70);
            */




        }

        /**
         * Configures the system (the source, pipeline filters and application)
         **/
        public bool configureSystem() {

            // configure the data object
            Data.Configure();

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
                Data.Start();
                
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

                    // stop the data
                    Data.Stop();

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
            double[] sample = null;

            #if(DEBUG_SAMPLES_LOG_PERFORMANCE)

                long time = Stopwatch.GetTimestamp() + Stopwatch.Frequency;
                long counter = 0;
                long samplesProcessed = 0;
            
            #endif
            
            // loop while running
            while (running) {
                
                #if(DEBUG_SAMPLES_LOG_PERFORMANCE)

                    // performance watch
                    if (Stopwatch.GetTimestamp() > time) {

                            logger.Info("----------");
                            logger.Info("samplesProcessed: " + samplesProcessed);
                            logger.Info("numberOfSamples: " + numberOfSamples);
                            logger.Info("tick counter: " + counter);
                            //logger.Info("sample[0]: " + sample[0]);

                        counter = 0;
                        time = Stopwatch.GetTimestamp() + Stopwatch.Frequency;
                        samplesProcessed = 0;

                    }
                    counter++;

                #endif

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

                            // Announce the sample at the beginning of the pipeline
                            Data.SampleProcessingStart();

                            // process the sample (filters)
                            for (int i = 0; i < filters.Count(); i++) {
                                filters[i].process(sample, out output);
                                sample = output;
                                output = null;
                            }

                            // process the sample (application)
                            if (application != null)    application.process(sample);

                            // Announce the sample at the end of the pipeline
                            Data.SampleProcessingEnd();

                            #if(DEBUG_SAMPLES_LOG_PERFORMANCE)

                                // add one to the number of samples 
                                samplesProcessed++;

                            #endif

                        }

                    }

                    // check if the thread is still running
                    if (running) {

                        // check if we are processing samples
                        if (process) {
                            // processing

			                // sleep (when there are no samples) to allow for other processes
			                if (threadLoopDelayProc != -1 && numberOfSamples == 0)
                                Thread.Sleep(threadLoopDelayProc);

                        } else {
                            // not processing

                            // sleep to allow for other processes
                            Thread.Sleep(threadLoopDelayNoProc);

                        }

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

            // destoy the filters
            for (int i = 0; i < filters.Count(); i++) filters[i].destroy();

            // destroy the view
            if (application != null)    application.destroy();

            // destroy the data class
            Data.Destroy();

            // log message
            logger.Debug("Thread stopped");

        }

        // 
        // Called when a new sample comes from the source, this method is called directly from the source object.
        // 
        // Not using delegate (or interface) events since there would be only one receiver, being this function called.
        // A direct method call will suffice and is probably even faster.
        public void eventNewSample(double[] sample) {
            
            lock(sampleBuffer.SyncRoot) {
                
                // check if the buffer is full
                if (numberOfSamples == sampleBufferSize) {

                    // message
                    logger.Error("Sample buffer full, the roundtrip of a sample through the filter and application takes longer than the sample frequency of the source");

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
         * Called when the GUI is dispatched and closed
         * 
         * Not using delegate (or interface) events since there would be only one receiver, being this function called.
         */
        public void eventGUIClosed() {

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


        /**
         * Static function to return 
         **/
        private static Parameters getFilterParameters(string filterName) {

            // find the filter, and return a reference to its paremeters
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

            // find the filter, and return a clone of its paremeters
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

        public static void saveParameterFile()
        {

            // TODO: indent with tabs
            // TODO: save with correct name in correct path

            // file name to save xml parameter file to 
            String xmlFileName = null;

            // create save file dialog box for user with standard filename set to current time
            SaveFileDialog savefile = new SaveFileDialog();
            savefile.FileName = DateTime.Now.ToString("yyyyMMdd_HHmm") + ".prm";

            // if user sucessfully selected location, save location and store file
            if (savefile.ShowDialog() == DialogResult.OK)
            {
                xmlFileName = savefile.FileName;

                // create parameter XML file with encoding declaration
                XmlDocument paramFile = new XmlDocument();
                XmlNode paramFileNode = paramFile.CreateXmlDeclaration("1.0", "UTF-8", null);
                paramFile.AppendChild(paramFileNode);

                // add root node
                XmlNode rootNode = paramFile.CreateElement("root");
                paramFile.AppendChild(rootNode);

                // VERSION NODES
                // create versions node and add to root node
                XmlNode versionsNode = paramFile.CreateElement("versions");
                rootNode.AppendChild(versionsNode);

                // get source name and version value, and create source version node 
                int sourceVersionValue = source.getClassVersion();
                String sourceVersionName = source.getClassName();
                XmlNode sourceVersionNode = paramFile.CreateElement("version");

                // add name attribute 
                XmlAttribute sourceNameAttr = paramFile.CreateAttribute("name");
                sourceNameAttr.Value = sourceVersionName;
                sourceVersionNode.Attributes.Append(sourceNameAttr);

                // add type attribute 
                XmlAttribute sourceTypeAttr = paramFile.CreateAttribute("type");
                sourceTypeAttr.Value = "source";
                sourceVersionNode.Attributes.Append(sourceTypeAttr);

                // add value attribute
                XmlAttribute sourceValueAttr = paramFile.CreateAttribute("value");
                sourceValueAttr.Value = sourceVersionValue.ToString();
                sourceVersionNode.Attributes.Append(sourceValueAttr);

                // add source version node to versions node
                versionsNode.AppendChild(sourceVersionNode);

                // cycle through filters
                foreach (IFilter filter in filters)
                {

                    // get filter version and create filter version node 
                    int filterVersionValue = filter.getClassVersion();
                    String filterVersionName = filter.getName();
                    XmlNode filterVersionNode = paramFile.CreateElement("version");

                    // add name attribute 
                    XmlAttribute filterNameAttr = paramFile.CreateAttribute("name");
                    filterNameAttr.Value = filterVersionName;
                    filterVersionNode.Attributes.Append(filterNameAttr);

                    // add type attribute 
                    XmlAttribute filterTypeAttr = paramFile.CreateAttribute("type");
                    filterTypeAttr.Value = "filter";
                    filterVersionNode.Attributes.Append(filterTypeAttr);

                    // add value attribute
                    XmlAttribute fillterValueAttr = paramFile.CreateAttribute("value");
                    fillterValueAttr.Value = filterVersionValue.ToString();
                    filterVersionNode.Attributes.Append(fillterValueAttr);

                    // add filter version node to versions node
                    versionsNode.AppendChild(filterVersionNode);
                }

                // get application version and create application version node 
                int applicationVersionValue = application.getClassVersion();
                String applicationVersionName = application.getClassName();
                XmlNode applicationVersionNode = paramFile.CreateElement("version");

                // add name attribute 
                XmlAttribute applicationNameAttr = paramFile.CreateAttribute("name");
                applicationNameAttr.Value = applicationVersionName;
                applicationVersionNode.Attributes.Append(applicationNameAttr);

                // add type attribute 
                XmlAttribute applicationTypeAttr = paramFile.CreateAttribute("type");
                applicationTypeAttr.Value = "application";
                applicationVersionNode.Attributes.Append(applicationTypeAttr);

                // add value attribute
                XmlAttribute applicationValueAttr = paramFile.CreateAttribute("value");
                applicationValueAttr.Value = applicationVersionValue.ToString();
                applicationVersionNode.Attributes.Append(applicationValueAttr);

                // add application version node to versions node
                versionsNode.AppendChild(applicationVersionNode);

                // PARAMETERSET NODES
                // get parameterSets
                Dictionary<string, Parameters> parameterSets = ParameterManager.getParameterSets();

                // cycle through parameter sets
                foreach (KeyValuePair<string, Parameters> entry in parameterSets)
                {

                    // get name of parameter set
                    String setname = entry.Key;
                    logger.Debug("Saving parameter set: " + setname);

                    // create parameterSet node 
                    XmlNode parameterSetNode = paramFile.CreateElement("parameterSet");

                    // add name attribute to parameterSet node and set equal to set name
                    XmlAttribute parameterSetName = paramFile.CreateAttribute("name");
                    parameterSetName.Value = setname;
                    parameterSetNode.Attributes.Append(parameterSetName);

                    // add parameterSet node to root node
                    rootNode.AppendChild(parameterSetNode);

                    // get Parameter object and iParams contained within
                    Parameters parameterSet = entry.Value;
                    List<iParam> parameters = parameterSet.getParameters();

                    // cycle thorugh iParams
                    foreach (iParam parameter in parameters)
                    {

                        // param name
                        String paramName = parameter.Name;
                        String paramType = parameter.GetType().ToString();
                        String paramValue = parameter.ToString();
                        logger.Debug("Saving parameter " + paramName + " of type " + paramType + " with value of " + paramValue + " to parameter file.");
                        
                        // create param node 
                        XmlNode paramNode = paramFile.CreateElement("param");

                        // add name attribute 
                        XmlAttribute paramNameAttr = paramFile.CreateAttribute("name");
                        paramNameAttr.Value = paramName;
                        paramNode.Attributes.Append(paramNameAttr);

                        // add type attribute 
                        XmlAttribute paramTypeAttr = paramFile.CreateAttribute("type");
                        paramTypeAttr.Value = paramType;
                        paramNode.Attributes.Append(paramTypeAttr);

                        // add value attribute
                        XmlAttribute paramValueAttr = paramFile.CreateAttribute("value");
                        paramValueAttr.Value = paramValue;
                        paramNode.Attributes.Append(paramValueAttr);

                        // add param node to parameterSet node
                        parameterSetNode.AppendChild(paramNode);

                    }

                }

                // check if filename is set and save parameter file
                if (!String.IsNullOrEmpty(xmlFileName))
                {

                    // save parameter XML file and give feedback
                    try
                    {
                        paramFile.Save(xmlFileName);
                        logger.Info("Saved parameter file " + xmlFileName);
                        MessageBox.Show("Saved parameter file " + xmlFileName);
                    }
                    catch (Exception e)
                    {
                        logger.Error("Unable to save parameter file " + xmlFileName + " (" + e.Message + ")");
                    }

                }
            }
        }

        public static void loadParameterFile()
        {

            // initialize stream and XmlDocument object
            Stream xmlParameters = null;
            XmlDocument paramFile = new XmlDocument();

            // open file dialog to open parameter file, starting in current directory, with filter for .prm files
            OpenFileDialog loadXmlFile = new OpenFileDialog();
            loadXmlFile.InitialDirectory = Directory.GetCurrentDirectory();
            loadXmlFile.Filter = "parameter files (*.prm)|*.prm|All files (*.*)|*.*";
            loadXmlFile.RestoreDirectory = true;            // restores current directory to the previously selected directory, potentially beneficial if other code relies on the currently set directory

            // if dialog is succesfully shown
            if (loadXmlFile.ShowDialog() == DialogResult.OK)
            {

                // load contents of parameter xml file to stream and then to XmlDocument object
                try
                {
                    if ((xmlParameters = loadXmlFile.OpenFile()) != null)
                    {
                        paramFile.Load(xmlParameters);
                    }
                }
                catch (Exception e)
                {
                    MessageBox.Show("Error: Could not read parameter file (" + e.Message + ")");
                }
            }

            // get versions, output for debug
            XmlNodeList versions = paramFile.GetElementsByTagName("version");
            for (int v = 0; v < versions.Count; v++) { logger.Debug("Current version of " + versions[v].Attributes["type"].Value + " " + versions[v].Attributes["name"].Value + " in parameter file: " + versions[v].Attributes["value"].Value); }

            // load current parametersSets
            Dictionary<string, Parameters> currParameterSets = ParameterManager.getParameterSets();

            // get parametersets and params from xml
            XmlNodeList parameterSets = paramFile.GetElementsByTagName("parameterSet");

            // for each parameter set
            for (int s = 0; s < parameterSets.Count; s++)
            {

                // get name of set
                string parameterSetName = parameterSets[s].Attributes["name"].Value;
                logger.Debug("Loading parameter set " + parameterSetName + "from parameter file.");

                // load current parameters of set
                Parameters currParameterSet = currParameterSets[parameterSetName];

                // set current parameters in set to values in xml
                if (parameterSets[s].HasChildNodes)
                {

                    // cycle through all parameters in this set contained in xml
                    for (int p = 0; p < parameterSets[s].ChildNodes.Count; p++)
                    {

                        // get name, type and value of param
                        string paramName = parameterSets[s].ChildNodes[p].Attributes["name"].Value;
                        string paramType = parameterSets[s].ChildNodes[p].Attributes["type"].Value;
                        string paramValue = parameterSets[s].ChildNodes[p].Attributes["value"].Value;

                        // get current type of this param
                        string currType = currParameterSet.getType(paramName);

                        // if type in xml is equal to current type, update value of param to value in xml
                        if (paramType == currType)
                        {
                            // logger.Debug("Parameter " + paramName + " is same type as currently registered.");
                            currParameterSet.setValue(paramName, paramValue);
                            logger.Debug("Parameter " + paramName + " is updated to " + paramValue + " (value taken from parameter file.)");
                            // TODO: try catch
                            // TODO: give feedback in logger and Dialog that loading was succesful
                            // TODO relaod GUI so any updated values are immediately visible

                        }
                        else
                        {
                            logger.Error("Parameter " + paramName + " is not same type as currently registered and is not updated to file in parameter file");
                        }

                        // debug
                        //logger.Debug(parameterSets[s].ChildNodes[p].Attributes["name"].Value);
                        //logger.Debug(parameterSets[s].ChildNodes[p].Attributes["type"].Value);
                        //logger.Debug(parameterSets[s].ChildNodes[p].Attributes["value"].Value);

                    }
                }
            }
        }

    }

}
