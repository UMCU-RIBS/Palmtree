using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UNP.Core.Events;
using UNP.Core.Helpers;
using UNP.Core.Params;
using System.Linq;

namespace UNP.Core {

    // Data class. Takes care of data storage and visualization
    // 
    // 
    // static class over singleton pattern because we do not need an instance using an interface
    // or be passed around, also the static will be stored in stack (instead of heap) giving better
    // performance (important since the Data class is called upon frequently)
    public static class Data {

        private const int DATAFORMAT_VERSION = 1;

        private static Logger logger = LogManager.GetLogger("Data");
        private static Parameters parameters = ParameterManager.GetParameters("Data", Parameters.ParamSetTypes.Data);

        private static string dataDir = "";                                                     // location of data directory
        private static string sessionDir = null;                                                  // contains full path of directory all files of one sesison are written to
        private static string currDir = "";                                                     // contains full path of current directory files are written in
        private static string identifier = "";                                                  // file identifier, prefix in filename
        private static bool subDirPerRun = false;                                               // whether or not a sub-directory must be made in the session directory to hold the generated files per run
        private static int run = 0;                                                             // contains number of current run
        private static bool mCensorLogging = false;                                             // flag whether the logging should be censored (zeros should be written instead)

        // event logging
        private static bool mLogEvents = false;                 								// 
        private static int[] mEventLoggingLevels = new int[0];

        private static FileStream eventStream = null;                                           // filestream that is fed to the binarywriter, containing the stream of events to be written to the .evt file
        private static StreamWriter eventStreamWriter = null;                                   // writer that writes values to the .evt file

        // source streams
        private static bool mLogSourceInput = false;            								// source input logging enabled/disabled (by configuration parameter)
        private static int numSourceInputStreams = 0;                                           // the number of streams coming from the source input
        private static List<string> registeredSourceInputStreamNames = new List<string>(0);     // the names of the registered source input streams to store in the .src file
        private static List<int> registeredSourceInputStreamTypes = new List<int>(0);           // the types of the registered source input streams to store in the .src file

        private static FileStream sourceStream = null;                                          // filestream that is fed to the binarywriter, containing the stream of values to be written to the .src file
        private static BinaryWriter sourceStreamWriter = null;                                  // writer that writes values to the .src file
        private static uint sourceSampleCounter = 0;                                            // the current row of values being written to the .src file, acts as id
        private static Stopwatch sourceStopWatch = new Stopwatch();                             // creates stopwatch to measure time difference between incoming samples
        private static double sourceElapsedTime = 0;                                            // amount of time [ms] elapsed since start of proccesing of previous sample

        // pipeline input streams
        private static bool mLogPipelineInputStreams = false;            				        // stream logging for pipeline input enabled/disabled (by configuration parameter)
        private static int numPipelineInputStreams = 0;                                         // amount of pipeline input streams

        // filters and application streams
        private static bool mLogFiltersAndApplicationStreams = false;            				// stream logging for filters and application modules enabled/disabled (by configuration parameter)

        // data streams (includes pipeline input, filter and application streams)
        private static int numDataStreams = 0;                                                  // the total number of data streams to be logged in the .dat file
        private static List<string> registeredDataStreamNames = new List<string>(0);            // the names of the registered streams to store in the .dat file
        private static List<int> registeredDataStreamTypes = new List<int>(0);                  // the types of the registered streams to store in the .dat file

        private static FileStream dataStream = null;                                            // filestream that is fed to the binarywriter, containing the stream of values to be written to the .dat file
        private static BinaryWriter dataStreamWriter = null;                                    // writer that writes values to the .dat file
        private static double[] dataStreamValues = null;                                        // holds the values of all data streams that are registered to be logged 
        private static uint dataSampleCounter = 0;                                              // the current row of values being written to the .dat file, acts as id
        private static int dataValuePointer = 0;                                                // the current location in the dataStreamValues array that the incoming value is written to
        private static Stopwatch dataStopWatch = new Stopwatch();                               // creates stopwatch to measure time difference between incoming samples
        private static double dataElapsedTime = 0;                                              // amount of time [ms] elapsed since start of proccesing of previous sample

        // plugin streams
        private static bool mLogPluginInput = false;            								// plugin input logging enabled/disabled (by configuration parameter)
        private static int numPlugins = 0;                                                      // stores the amount of registered plugins
        private static List<string> registeredPluginNames = new List<string>(0);                // stores the names of the registered plugins

        private static List<int> numPluginInputStreams = new List<int>(0);                                  // the number of streams that will be logged for each plugin
        private static List<List<string>> registeredPluginInputStreamNames = new List<List<string>>(0);     // for each plugin, the names of the registered plugin input streams to store in the plugin data log file
        private static List<List<int>> registeredPluginInputStreamTypes = new List<List<int>>(0);           // for each plugin, the types of the registered plugin input streams to store in the plugin data log file

        private static List<double[]> pluginDataValues = new List<double[]>(0);               // list of two dimensional arrays to hold plugin data as it comes in 
        private static int bufferSize = 10000;                                                  // TEMP size of buffer, ie amount of datavalues stored for each plugin before the buffer is flushed to the data file at sampleProcessingEnd
        private static List<int> bufferPointers = new List<int>(0);

        private static List<FileStream> pluginStreams = new List<FileStream>(0);                // list of filestreams, one for each plugin, to be able to write to different files with different frequencies. Each filestream is fed to a binarywriter, containing the stream of values to be written to the plugin log file
        private static List<BinaryWriter> pluginStreamWriters = new List<BinaryWriter>(0);      // list of writers, one for each plugin, each writes values specific for that plugin to the plugin data log file

        // visualization
        private static bool mEnableDataVisualization = false;                                    // data visualization enabled/disabled
        private static int numVisualizationStreams = 0;                                         // the total number of streams to visualize
        private static List<string> registeredVisualizationStreamNames = new List<string>(0);   // the names of the registered streams to visualize
        private static List<int> registeredVisualizationStreamTypes = new List<int>(0);         // the types of the registered streams to visualize
        private static double[] visualizationStreamValues = null;
        private static int visualizationStreamValueCounter = 0;

        // Visualization event(handler)s. An EventHandler delegate is associated with the event.
        // methods should be subscribed to this object
        public static event EventHandler<VisualizationValuesArgs> newVisualizationSourceInputValues = delegate { };
        public static event EventHandler<VisualizationValuesArgs> newVisualizationStreamValues = delegate { };
        public static event EventHandler<VisualizationEventArgs> newVisualizationEvent = delegate { };


        private static Object lockPlugin = new Object();                                               // threadsafety lock for plugin buffer/event

        public static void construct() {

            parameters.addParameter<bool>(
                "EnableDataVisualization",
                "Enable/disable the visualization of data.\nNote: Only turn this is off for performance benefits",
                "1");

            parameters.addParameter<string>(
                "DataDirectory",
                "Path to the directory to store the data in.\nThe path can be absolute (e.g. 'c:\\data\\') or relative (to the program executable, e.g. 'data\\').",
                "", "", "data\\");

            parameters.addParameter<bool>(
                "SubDirectoryPerRun",
                "Store all files generated during each run in a seperate sub-directory within the data directory.",
                "1");

            parameters.addParameter<string>(
                "Identifier",
                "A textual identifier.\nThe ID will be incorporated into the filename of the data files.",
                "", "", "test");

            parameters.addParameter<bool>(
                "LogSourceInput",
                "Enable/disable source input logging.\n\nNote: when there is no source input (for example when the source is a signal generator) then no source input data file will be created nor will there be any logging of source input",
                "1");

            parameters.addParameter<int>(
                "SourceInputMaxFilesize",
                "The maximum filesize for a source input data file.\nIf the data file exceeds this maximum, the data logging will continue in a sequentally numbered file with the same name.\n(set to 0 for no maximum)",
                "0");

            parameters.addParameter<bool>(
                "LogPipelineInputStreams",
                "Enable/disable pipeline input stream logging.\n\nNote: The pipeline input streams need to be logged in order to playback the .dat file.",
                "1");

            parameters.addParameter<bool>(
                "LogFiltersAndApplicationStreams",
                "Enable/disable filters and application data stream logging.\nThis option will enable or disable the logging of data streams for the filters and application modules.\nEnabling or disabling data stream for a specific filter or application module has to be done in the module's settings\n\nNote: whether the streams that are being logged have values or zeros is dependent on the runtime configuration of the modules. It is possible\nthat the user, though an application module user-interface, sets certain streams to be (values) or not be (zeros) logged.",
                "1");

            parameters.addParameter<int>(
                "SampleStreamMaxFilesize",
                "The maximum filesize for a stream data file.\nIf the data file exceeds this maximum, the data logging will continue in a sequentally numbered file with the same name.\n(set to 0 for no maximum)",
                "0");

            parameters.addParameter<bool>(
                "LogEvents",
                "Enable/disable event logging.",
                "1");

            parameters.addParameter<int[]>(
                "EventLoggingLevels",
                "Indicate which levels of event logging are allowed.\n(leave empty to log all levels)\n\nNote: whether events are logged or not is also dependent on the runtime configuration of the modules. It is possible\nthat the user, though an application module user-interface, sets certain events to be or not be logged.",
                "0");

            parameters.addParameter<bool>(
                "LogPluginInput",
                "Enable/disable plugin input logging.\n\nNote: when there is no plugin input then no plugin data file will be created nor will there be any logging of plugin input",
                "1");

        }

        /**
         * Configure the data class. Checks the values and application logic of the
         * parameters and, if valid, transfers the configuration parameters to local variables
         *
         * This will be called before all other configuration, which is why the registered stream are cleared here
         **/
        public static bool configure() {

            // clear the registered source input streams
            registeredSourceInputStreamNames.Clear();
            registeredSourceInputStreamTypes.Clear();
            numSourceInputStreams = 0;

            // clear the registered data streams
            registeredDataStreamNames.Clear();
            registeredDataStreamTypes.Clear();
            numDataStreams = 0;
            numPipelineInputStreams = 0;

            // clear the registered visualization streams
            registeredVisualizationStreamNames.Clear();
            registeredVisualizationStreamTypes.Clear();
            numVisualizationStreams = 0;

            // clear the registered plugin streams
            registeredPluginNames.Clear();
            numPlugins = 0;
            registeredPluginInputStreamNames.Clear();
            registeredPluginInputStreamTypes.Clear();
            numPluginInputStreams.Clear();

            // check and transfer visualization parameter settings
            mEnableDataVisualization = parameters.getValue<bool>("EnableDataVisualization");
            Globals.setValue<bool>("EnableDataVisualization", mEnableDataVisualization ? "1" : "0");

            // check and transfer file parameter settings
            mLogSourceInput = parameters.getValue<bool>("LogSourceInput");
            mLogPipelineInputStreams = parameters.getValue<bool>("LogPipelineInputStreams");
            mLogFiltersAndApplicationStreams = parameters.getValue<bool>("LogFiltersAndApplicationStreams");
            mLogEvents = parameters.getValue<bool>("LogEvents");
            mEventLoggingLevels = parameters.getValue<int[]>("EventLoggingLevels");
            mLogPluginInput = parameters.getValue<bool>("LogPluginInput");

            // retrieve identifier
            identifier = parameters.getValue<string>("Identifier");
            if (string.IsNullOrEmpty(identifier)) {

                // message
                logger.Error("A identifier should be given for the data");

                // return failure
                return false;

            }


            subDirPerRun = parameters.getValue<bool>("SubDirectoryPerRun");

            // ...

            // if there is data to store, create data (sub-)directory
            if (mLogSourceInput || mLogPipelineInputStreams || mLogFiltersAndApplicationStreams || mLogEvents || mLogPluginInput) {

                // construct path name of data directory
                dataDir = parameters.getValue<string>("DataDirectory");
                currDir = Path.Combine(Directory.GetCurrentDirectory(), dataDir);

                // construct path name of directory for this session
                string sub = DateTime.Now.ToString("yyyyMMdd_HHmm");
                currDir = Path.Combine(currDir, sub);

                // create the data (sub-)directory 
                try {

                    if (Directory.Exists(currDir)) {
                        logger.Info("Data (sub-)directory already exists.");
                    } else {
                        Directory.CreateDirectory(currDir);
                        logger.Info("Created data (sub-)directory at " + currDir);
                    }

                } catch (Exception e) {
                    logger.Error("Unable to create data (sub-)directory at " + currDir + " (" + e.ToString() + ")");
                }

            }
            return true;

        }

        /**
         * Register a source input stream
         * Every source that wants to log input should announce every stream respectively beforehand using this function
         * 
         * (this should be called during configuration, by all sources that will log their samples)
         **/
        public static void registerSourceInputStream(string streamName, SampleFormat streamType) {

            // register a new stream
            registeredSourceInputStreamNames.Add(streamName);
            registeredSourceInputStreamTypes.Add(0);

            // add one to the total number of streams
            numSourceInputStreams++;

            // message
            logger.Debug("Registered source input stream '" + streamName + "' of the type ...");

        }

        public static int getNumberOfSourceInputStreams() {
            return numSourceInputStreams;
        }

        public static string[] getSourceInputStreamNames() {
            return registeredSourceInputStreamNames.ToArray();
        }
        
        // register the pipeline input streams based on the output format of the source
        public static void registerPipelineInputStreams(SampleFormat output) {

            // use the number of input channels for the pipeline to the number of output channels from the source
            numPipelineInputStreams = (int)output.getNumberOfChannels();

            // check if the pipeline input streams should be logged
            if (mLogPipelineInputStreams) {

                // register the streams
                for (int channel = 0; channel < numPipelineInputStreams; channel++)
                    Data.registerDataStream(("Pipeline_Input_Ch" + (channel + 1)), output);

            }

            // check if visualization is enabled
            if (mEnableDataVisualization) {

                // register the streams to visualize
                for (int channel = 0; channel < numPipelineInputStreams; channel++) {
                    Data.registerVisualizationStream(("Pipeline_Input_Ch" + (channel + 1)), output);

                }
            }

        }

        /**
         * Register a data stream
         * Every module that wants to log a stream should announce every stream respectively beforehand using this function
         * 
         * (this should be called during configuration, by all sources/filters/application that will log their samples)
         **/
        public static void registerDataStream(string streamName, SampleFormat streamType) {

            // register a new stream
            registeredDataStreamNames.Add(streamName);
            registeredDataStreamTypes.Add(0);

            // add one to the total number of streams
            numDataStreams++;

            // message
            logger.Debug("Registered data stream '" + streamName + "' of the type ...");

        }

        /**
         * Register the visualization stream
         * Every module that wants to visualize a stream of samples should announce every stream respectively beforehand using this function
         * 
         * (this should be called during configuration, by all sources/filters/application that will log their samples)
         **/
        public static void registerVisualizationStream(string streamName, SampleFormat streamType) {

            // register a new stream
            registeredVisualizationStreamNames.Add(streamName);
            registeredVisualizationStreamTypes.Add(0);

            // add one to the total number of visualization streams
            numVisualizationStreams++;

            // message
            logger.Debug("Registered visualization stream '" + streamName + "' of the type ...");

        }

        public static int getNumberOfVisualizationDataStreams() {
            return numVisualizationStreams;
        }

        public static string[] getVisualizationDataStreamNames() {
            return registeredVisualizationStreamNames.ToArray();
        }

        /**
         * Register all input streams for a plugin
         * Every plugin that wants to log input should announce every stream respectively beforehand using this function
         * This should be called during configuration, by all plugins that will log their samples).
         * 
         * (in contrast to registering source and datastreams this function takes all streams in one go, because plugins are assigned an id at this point to allow to write the data from all streams from one plugin to one file)  
         **/
        public static int registerPluginInputStream(string pluginName, string[] streamNames, SampleFormat[] streamTypes) {

            // assign id to plugin 
            int pluginId = numPlugins;

            // register plugin name
            registeredPluginNames.Add(pluginName);

            // register al  streams for this plugin
            for (int i = 0; i < streamNames.Length; i++) {
                registeredPluginInputStreamNames.Add(streamNames.ToList());
                registeredPluginInputStreamTypes.Add(new List<int>(streamNames.Length));
            }

            // create array to hold incoming data and set pointer in buffer to initital value
            // TODO: set size depending on difference in output frequency of plugin and source, now set to 10000 to be on the safe side
            double[] pluginData = new double[bufferSize];
            pluginDataValues.Add(pluginData);
            bufferPointers.Add(0);

            // add one to the total number of registered plugins
            numPlugins++;

            // message
            logger.Debug("Registered " + streamNames.Length + " streams for plugin " + pluginName + " with id " + pluginId + "(" + numPlugins + ")");

            // return assigned id to plugin, so plugin can use this to uniquely identify the data sent to this class
            return pluginId;
        }

        public static int getNumberOfPlugins() {
            return numPlugins;
        }

        public static int getNumberOfPluginInputStreams(int pluginId) {
            return registeredPluginInputStreamNames[pluginId].Count;
        }

        public static string[] getPluginInputStreamNames(int pluginId) {
            return registeredPluginInputStreamNames[pluginId].ToArray();
        }

        public static void start() {

            // increase run number
            run++;

            // get location of data directory and current time to use as timestamp for files to be created
            string fileName = identifier + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");

            // create subdirectory for this run if required
            if (subDirPerRun) {

                // set directory for this session if was not already set   
                if (sessionDir == null)     sessionDir = currDir;

                // set pointer for current directory to directory for this run
                String runDir = "run" + run;
                currDir = Path.Combine(sessionDir, runDir);

                // create subdirectory for this run
                try {

                    if (Directory.Exists(currDir)) {
                        logger.Info("Data directory for this run already exists.");
                    } else {
                        Directory.CreateDirectory(currDir);
                        logger.Info("Created data directory for run " + run + " at " + currDir);
                    }
                } catch (Exception e) {
                    logger.Error("Unable to create data directory for run " + run + " at " + currDir + " (" + e.ToString() + ")");
                }

            }

            // create parameter file and save current parameters
            Dictionary<string, Parameters> localParamSets = ParameterManager.getParameterSetsClone();
            ParameterManager.saveParameterFile(fileName + ".prm", localParamSets);

            // check if we want to log events
            if (mLogEvents) {

                // construct filepath of event file, with current time as filename 
                string fileNameEvt = fileName + ".evt";
                string path = Path.Combine(currDir, fileNameEvt);

                // create filestream: create file if it does not exists, allow to write, do not share with other processes and use buffer of 8192 bytes
                try {
                    eventStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 8192);
                    eventStreamWriter = new StreamWriter(eventStream);
                    eventStreamWriter.AutoFlush = true;                                         // ensures that after every write operation content of stream is flushed to file
                    logger.Info("Created event file at " + path);
                } catch (Exception e) {
                    logger.Error("Unable to create event file at " + path + " (" + e.ToString() + ")");
                }

                // build event header string
                string eventHeader = "Time " + "ID source sample " + "ID data sample " + "Event code " + "Event value";

                // write header to event file
                try {
                    eventStreamWriter.WriteLine(eventHeader);
                } catch (IOException e) {
                    logger.Error("Can't write to event file: " + e.Message);
                }

            }

            // check if we want to log the source input
            if (mLogSourceInput && numSourceInputStreams > 0) {

                // (re)start the stopwatch here, so the elapsed timestamp of the first sample will be the time from start till the first sample coming in
                sourceStopWatch.Restart();

                // log the source start event
                logEvent(1, "SourceStart", "");

                // (re)set sample counter
                sourceSampleCounter = 0;

                // construct filepath of source file, with current time as filename 
                string fileNameSrc = fileName + ".src";
                string path = Path.Combine(currDir, fileNameSrc);

                // create filestream: create file if it does not exists, allow to write, do not share with other processes and use buffer of 8192 bytes (roughly 1000 samples)
                try {
                    sourceStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 8192);
                    sourceStreamWriter = new BinaryWriter(sourceStream);
                    logger.Info("Created source file at " + path);
                } catch (Exception e) {
                    logger.Error("Unable to create source file at " + path + " (" + e.ToString() + ")");
                }

                // write header
                writeHeader(registeredSourceInputStreamNames, sourceStreamWriter, false);
            }

            // check if there are any samples streams to be logged
            if ((mLogPipelineInputStreams || mLogFiltersAndApplicationStreams) && numDataStreams > 0) {

                // (re)start the stopwatch here, so the elapsed timestamp of the first sample will be the time from start till the first sample coming in
                dataStopWatch.Restart();

                // log the data start event
                logEvent(1, "DataStart", "");

                // (re)set sample counter
                dataSampleCounter = 0;

                // resize dataStreamValues array in order to hold all values from all data streams
                dataStreamValues = new double[numDataStreams];

                // construct filepath of data file, with current time as filename 
                string fileNameDat = fileName + ".dat";
                string path = Path.Combine(currDir, fileNameDat);
                
                try {

                    // create filestream: create file if it does not exists, allow to write, do not share with other processes and use buffer of 8192 bytes (roughly 1000 samples)
                    dataStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 8192);
                    dataStreamWriter = new BinaryWriter(dataStream);

                    // message
                    logger.Info("Created data file at " + path);

                } catch (Exception e) {

                    logger.Error("Unable to create data file at " + path + " (" + e.ToString() + ")");

                }

                // write header
                writeHeader(registeredDataStreamNames, dataStreamWriter, false);

            }

            // check if we want to log the plugin input
            if (mLogPluginInput && numPlugins > 0) {

                // for each plugin, create stream, writer and file
                for (int i = 0; i < numPlugins; i++) {

                    // log the source start event
                    logEvent(1, "PluginLogStart", "plugin id: " + i);

                    // construct filepath of plugin data file, with current time and name of plugin as filename 
                    string fileNamePlugin = fileName + "_" + registeredPluginNames[i] + ".dat";
                    string path = Path.Combine(currDir, fileNamePlugin);

                    // create filestream: create file if it does not exists, allow to write, do not share with other processes and use buffer of 8192 bytes (roughly 1000 samples)
                    try {
                        pluginStreams.Add(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 8192));
                        pluginStreamWriters.Add(new BinaryWriter(pluginStreams[i]));
                        logger.Info("Created plugin data log file for plugin " + registeredPluginNames[i] + " at " + path);
                    } catch (Exception e) {
                        logger.Error("Unable to create plugin data log file for plugin " + registeredPluginNames[i] + " at " + path + " (" + e.ToString() + ")");
                    }

                    // write header
                    writeHeader(registeredPluginInputStreamNames[i], pluginStreamWriters[i], true);
                }

            }

            // check if data visualization is enabled
            if (mEnableDataVisualization) {

                // size the array to fit all the streams
                visualizationStreamValues = new double[numVisualizationStreams];

            }

        }

        public static void stop() {

            // TODO: close the event file,(and more closing things/variables?)

            // stop and close data stream 
            if (dataStreamWriter != null) {

                // log the data stop event
                logEvent(1, "DataStop", "");

                // close the data stream file
                dataStreamWriter.Close();
                dataStreamWriter = null;
                dataStream = null;
            }

            // stop and close source stream 
            if (sourceStreamWriter != null) {

                // log the source stop event
                logEvent(1, "SourceStop", "");

                // close the source stream file
                sourceStreamWriter.Close();
                sourceStreamWriter = null;
                sourceStream = null;
            }

            //  flush any remaining data in plugin buffers to files
            writePluginData(-1);

            // stop and close all plugin data streams
            for (int i = 0; i < numPlugins; i++) {

                if (pluginStreamWriters[i] != null) {

                    // log the plugin log stop event
                    logEvent(1, "PluginLogStop", "plugin id: " + i);

                    // close the plugin stream file
                    pluginStreamWriters[i].Close();
                    pluginStreamWriters[i] = null;
                    pluginStreams[i] = null;
                }
            }

            // if there is still a stopwatch running, stop it
            if (dataStopWatch.IsRunning)        dataStopWatch.Stop();
            if (sourceStopWatch.IsRunning)      sourceStopWatch.Stop();

        }

        public static void destroy() {

            // stop the data Class
            // Note: At this point stop will probably have been called from the mainthread before destroy, however there is a slight
            // chance that in the future someone accidentally will put something in the configure/initialize that should have
            // actually been put in the start. If start is not called in the mainthread, then stop will also not be called at the
            // modules. For these accidents we do an extra stop here.
            stop();

            // TODO: Finalize stopwatches


        }

        /**
         * Called when a sample is at the beginning of the pipeline (from before the first filter module till after the application module)
         **/
        public static void sampleProcessingStart() {

            // check if data logging is allowed
            if (mLogPipelineInputStreams || mLogFiltersAndApplicationStreams) {

                // reset the array pointers for samples in data stream
                dataValuePointer = 0;

                // store the milliseconds past since the last sample and reset the data stopwatch timer
                dataElapsedTime = dataStopWatch.ElapsedMilliseconds;
                dataStopWatch.Restart();

            }

            // reset the visualization data stream counter
            visualizationStreamValueCounter = 0;

        }

        /**
         * Called when a sample is at the end of the pipeline (from before the first filter module till after the application module)
         **/
        public static void sampleProcessingEnd() {

            // debug, show data values being stored
            logger.Debug("To .dat file: " + dataSampleCounter + " " + dataElapsedTime + " " + string.Join(" |", dataStreamValues));

            // TODO: cutting up files based on the maximum size limit
            // TODO? create function that stores data that can be used for both src and dat?

            // integrity check of collected data stream values: if the pointer is not exactly at end of array, not all values have been
            // delivered or stored, else transform to bytes and write to file
            if (dataValuePointer != numDataStreams) {
                
                // message
                logger.Error("Less data values have been logged (" + dataValuePointer + ") than expected/registered (" + numDataStreams + ") for logging, unreliable .dat file, check code");

            } else {

                // check if data logging is allowed
                if (mLogPipelineInputStreams || mLogFiltersAndApplicationStreams) {

                    // transform variables that will be stored in .dat to binary arrays (except for dataStreamValues array which is copied directly)
                    byte[] dataSampleCounterBinary = BitConverter.GetBytes(dataSampleCounter);
                    byte[] dataElapsedTimeBinary = BitConverter.GetBytes(dataElapsedTime);

                    // create new array to hold all bytes
                    int l1 = dataSampleCounterBinary.Length;
                    int l2 = dataElapsedTimeBinary.Length;
                    int l3 = dataStreamValues.Length * sizeof(double);
                    byte[] streamOut = new byte[l1 + l2 + l3];

                    // blockcopy all bytes to this array
                    Buffer.BlockCopy(dataSampleCounterBinary, 0, streamOut, 0, l1);
                    Buffer.BlockCopy(dataElapsedTimeBinary, 0, streamOut, l1, l2);
                    Buffer.BlockCopy(dataStreamValues, 0, streamOut, l1 + l2, l3);

                    // write data to file
                    dataStreamWriter.Write(streamOut);

                }

            }

            // advance sample counter, if gets to max value, reset to 0
            if (++dataSampleCounter == uint.MaxValue) dataSampleCounter = 0;

            // check if data visualization is allowed
            if (mEnableDataVisualization) {

                // check if the number of values that came in is the same as the number of streams we expected to come in (registered)
                if (visualizationStreamValueCounter == numVisualizationStreams) {
                    // number of values matches

                    // trigger a new data stream values event for visualization
                    VisualizationValuesArgs args = new VisualizationValuesArgs();
                    args.values = visualizationStreamValues;
                    newVisualizationStreamValues(null, args);

                } else {
                    // number of values mismatches

                    // message
                    logger.Error("Not the same number of visualization streams (" + visualizationStreamValueCounter + ") are logged than have been registered (" + numVisualizationStreams + ") for logging, discarded, check code");

                }

            }

            // flush all plugin buffers to file
            writePluginData(-1);

        }

        /**
         * Log raw source input values to the source input file (.src) 
         * 
         **/
        public static void logSourceInputValues(double[] sourceStreamValues) {

            // get time since last source sample
            sourceElapsedTime = sourceStopWatch.ElapsedMilliseconds;
            sourceStopWatch.Restart();

            // debug
            //logger.Debug("To .src file: " + sourceElapsedTime + " " + sourceSampleCounter + " " + string.Join("|", sourceStreamValues));

            // integrity check of collected source values
            if (sourceStreamValues.Length != numSourceInputStreams) {

                // message
                logger.Error("Less data streams have been logged (" + sourceStreamValues.Length + ") than have been registered (" + numSourceInputStreams + ") for logging, unreliable .src file, check code");


            } else {

                // check if source logging is allowed
                if (mLogSourceInput) {

                    // if censorship should be applied, then zero out the array of measured source samples
                    if (mCensorLogging)    Array.Clear(sourceStreamValues, 0, sourceStreamValues.Length);

                    // transform variables that will be stored in .src to binary arrays (except for sourceStreamValues array which is copied directly)
                    byte[] sourceSampleCounterBinary = BitConverter.GetBytes(sourceSampleCounter);
                    byte[] sourceElapsedTimeBinary = BitConverter.GetBytes(sourceElapsedTime);

                    // create new array to hold all bytes
                    int l1 = sourceSampleCounterBinary.Length;
                    int l2 = sourceElapsedTimeBinary.Length;
                    int l3 = sourceStreamValues.Length * sizeof(double);
                    byte[] streamOut = new byte[l1 + l2 + l3];

                    // blockcopy all bytes to this array
                    Buffer.BlockCopy(sourceSampleCounterBinary, 0, streamOut, 0, l1);
                    Buffer.BlockCopy(sourceElapsedTimeBinary, 0, streamOut, l1, l2);
                    Buffer.BlockCopy(sourceStreamValues, 0, streamOut, l1 + l2, l3);

                    // write source to file
                    sourceStreamWriter.Write(streamOut);
                }

            }

            // advance sample counter, if gets to max value, reset to 0
            if (++sourceSampleCounter == uint.MaxValue) sourceSampleCounter = 0;

            // check if data visualization is allowed
            if (mEnableDataVisualization) {

                // trigger a new source input values event for visualization
                VisualizationValuesArgs args = new VisualizationValuesArgs();
                args.values = sourceStreamValues;
                newVisualizationSourceInputValues(null, args);

            }

        }

        public static void logSourceInputValues(ushort[] values) {

            // TODO: temp until we have a standard format, we might want to store as ushort
            // but for now convert ushorts to double and call the double[] overload
            double[] dblValues = new double[values.Length];
            for (int i = 0; i < values.Length; i++) {
                dblValues[i] = values[i];
            }
            logSourceInputValues(dblValues);

        }

        /**
         * Log a pipeline input stream value to the stream file (.dat) 
         * 
         **/
        public static void logPipelineInputStreamValue(double value) {
            //Console.WriteLine("logPipelineInputStreamValue");

            // check if pipeline input streams are logged and (if they are) log the value
            if (mLogPipelineInputStreams)   logStreamValue(value);

        }

        /**
         * Log a raw stream value to the stream file (.dat) 
         * 
         **/
        public static void logStreamValue(double value) {
            //Console.WriteLine("LogStreamValue");

            // check if data logging is allowed
            if (mLogPipelineInputStreams || mLogFiltersAndApplicationStreams) {

                // check if the counter is within the size of the value array
                if (dataValuePointer >= numDataStreams) {

                    // message
                    logger.Error("More data streams are logged than have been registered for logging, discarded, check code (currently logging at values array index " + dataValuePointer + ")");

                } else {

                    // if censorship should be applied to logging, log 0's
                    if (mCensorLogging)    value = 0;

                    // store the incoming value in the array, advance the pointer
                    dataStreamValues[dataValuePointer] = value;
                    dataValuePointer++;

                }
            }
        }

        /**
         * Log events to the events file (.evt) 
         * 
         **/
        public static void logEvent(int level, string text, string value) {

            // check if event logging of this level is allowed
            if (mLogEvents && Array.IndexOf(mEventLoggingLevels, level) > -1) {

                // get time of event
                DateTime eventTime = DateTime.Now;

                // if no value given, log '-'for value to keep consistent number of fields per row in event file 
                if (string.IsNullOrEmpty(value))    value = "-";

                // construct event String    
                string eventOut = eventTime.ToString("yyyyMMdd_HHmmss_fff") + " " + sourceSampleCounter.ToString() + " " + dataSampleCounter.ToString() + " " + text + " " + value;

                // write event to event file
                try {
                    eventStreamWriter.WriteLine(eventOut);
                } catch (IOException e) {
                    logger.Error("Can't write to event file: " + e.Message);
                }

                // debug
                logger.Info("Event logged: " + eventOut);

            }

        }

        /**
       * Log a plugin value to the buffer to later write to file
       * 
       **/
        public static void logPluginDataValue(double[] values, int pluginId) {

            // check if data logging is allowed
            if (mLogPluginInput) {

                // if censorship should be applied, then log 0's
                if (mCensorLogging)    Array.Clear(values, 0, values.Length);

                // lock plugin for thread safety
                lock (lockPlugin) {

                    // check if there is still room in buffer
                    if ((bufferPointers[pluginId] + values.Length) < bufferSize) {

                        // store current sample id to be able to synchronize the plugin data
                        pluginDataValues[pluginId][bufferPointers[pluginId]] = (double) dataSampleCounter;
                        bufferPointers[pluginId]++;
                        //logger.Info("Logging input, logging sample counter: " + dataSampleCounter + "(" + bufferPointers[pluginId] + ")");

                        // store plugin data values
                        for (int i = 0; i < values.Length; i++) {
                            pluginDataValues[pluginId][bufferPointers[pluginId]] = values[i];
                            bufferPointers[pluginId]++;
                            //logger.Info("Logging input, logging plugin value: " + values[i] + "(" + bufferPointers[pluginId] + ")");
                        }

                    } else {

                        // if no room left in buffer, flush buffer to file and try to log plugin data again
                        writePluginData(pluginId);
                        logPluginDataValue(values, pluginId);
                    }

                } // end lock

            }
        }

        public static void writePluginData(int pluginId) {

            // lock plugin for thread safety
            lock (lockPlugin) { 

                // set begin and end of plugin id's to flush all plugin buffers
                int beginFlush = 0;
                int endFlush = numPlugins;

                // if pluginId is given, flush only buffer of this plugin
                if (pluginId != -1) {
                    beginFlush = pluginId;
                    endFlush = pluginId + 1;
                }

                // for all given plugins, flush buffers to respective files
                for (int plugin = beginFlush; plugin < endFlush; plugin++) {

                    // if there is data in the buffer, flush it (buffer can be empty if plugin has lower sampling frequency than pipeline)
                    if (bufferPointers[plugin] != 0) {

                        // create binary version of plugin data
                        byte[] streamOut = new byte[bufferPointers[plugin] * sizeof(double)];
                        Buffer.BlockCopy(pluginDataValues[plugin], 0, streamOut, 0, bufferPointers[plugin] * sizeof(double));

                        // write to file
                        pluginStreamWriters[plugin].Write(streamOut);

                        // clear buffer and reset buffer pointer
                        Array.Clear(pluginDataValues[plugin], 0, bufferSize);
                        bufferPointers[plugin] = 0;
                        //logger.Info("Writing, writing done, plugin buffer flushed" + "(" + bufferPointers[plugin] + ")");

                    }

                }
            } // lock
        }

        /**
         * Log a raw stream value to visualize
         * 
         **/
        public static void logVisualizationStreamValue(double value) {
            if (!mEnableDataVisualization) return;

            // check if the counter is within the size of the value array
            if (visualizationStreamValueCounter >= numVisualizationStreams) {
                // not within the size of the values array, more visualization streams are logged than have been registered for logging

                // message
                logger.Error("More visualization streams are logged than have been registered for logging, discarded, check code (currently logging at values array index " + visualizationStreamValueCounter + ")");

            } else {
                // within the size of the values array

                // set the value
                visualizationStreamValues[visualizationStreamValueCounter] = value;
                visualizationStreamValueCounter++;

            }

        }

        /**
         * Log raw source input data to visualize
         * 
         **/
        public static void logVisualizationEvent(int level, string text, string value) {

            VisualizationEventArgs args = new VisualizationEventArgs();
            args.level = level;
            args.text = text;
            args.value = value;
            newVisualizationEvent(null, args);

        }

        /**
         * Create header of .dat (plugin or filter) or .src file
         * 
         * boolean timing either includes (true) or excludes (false) extra column in header file for storing elapsed time 
         **/
        private static void writeHeader(List<string> streamNames, BinaryWriter writer, bool plugin) {

            // get number of columns (columns with data values + sample id column)
            int ncol = streamNames.Count + 1;

            // convert list with names of streams to String, with tabs between names 
            string header = string.Join("\t", streamNames.ToArray());

            // create sample id column
            string col = "Sample\t";

            // create timing column if desired
            if (!plugin) { 
                col = col + "Elapsed_ms\t";
                ncol++;
            }

            // add sample id (and if desired timing column) to header
            header = col + header;

            // add version number and create header
            byte[] headerBinary = Encoding.ASCII.GetBytes(header);

            // store number of columns and of source channels [bytes] 
            byte[] pluginBinary = BitConverter.GetBytes(plugin);
            byte[] versionBinary = BitConverter.GetBytes(DATAFORMAT_VERSION);
            byte[] ncolBinary = BitConverter.GetBytes(ncol);
            byte[] pipelineInputStreamsBinary = BitConverter.GetBytes(numPipelineInputStreams);

            // store length [bytes] of header 
            int headerLen = headerBinary.Length;
            byte[] headerLenBinary = BitConverter.GetBytes(headerLen);

            // creat output byte array and copy number of cols, length of header, and header itself into this array
            byte[] headerOut = new byte[1 + (4 * sizeof(int)) + headerLen];
            int filePointer = 0;
            Buffer.BlockCopy(pluginBinary, 0, headerOut, filePointer, sizeof(bool));
            filePointer += sizeof(bool);
            Buffer.BlockCopy(versionBinary, 0, headerOut, filePointer, sizeof(int));
            filePointer += sizeof(int);
            Buffer.BlockCopy(pipelineInputStreamsBinary, 0, headerOut, filePointer, sizeof(int));
            filePointer += sizeof(int);
            Buffer.BlockCopy(ncolBinary, 0, headerOut, filePointer, sizeof(int));
            filePointer += sizeof(int);
            Buffer.BlockCopy(headerLenBinary, 0, headerOut, filePointer, sizeof(int));
            filePointer += sizeof(int);
            Buffer.BlockCopy(headerBinary, 0, headerOut, filePointer, headerLen);

            // write header to file
            writer.Write(headerOut);
        }
        
    }
}
