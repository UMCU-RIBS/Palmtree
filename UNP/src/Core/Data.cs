using NLog;
using System;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UNP.Core.Events;
using UNP.Core.Helpers;
using UNP.Core.Params;


namespace UNP.Core {
    
    // Data class. Takes care of data storage and visualization
    // 
    // 
    // static class over singleton pattern because we do not need an instance using an interface
    // or be passed around, also the static will be stored in stack (instead of heap) giving better
    // performance (important since the Data class is called upon frequently)
    public static class Data {

        private static Logger logger = LogManager.GetLogger("Data");
        private static Parameters parameters = ParameterManager.GetParameters("Data", Parameters.ParamSetTypes.Data);

        private static bool mLogSourceInput = false;            // source input logging enabled/disabled (by configuration parameter)
        private static bool mLogSourceInputRuntime = false;     // stores whether during runtime the source input should be logged    (if it was on, then it can be switched off, resulting in 0's being logged)
        private static bool mLogDataStreams = false;            // stream logging enabled/disabled (by configuration parameter)
        private static bool mLogDataStreamsRuntime = false;     // stores whether during runtime the streams should be logged    (if it was on, then it can be switched off, resulting in 0's being logged)
        private static bool mLogEvents = false;                 // 
        private static bool mLogEventsRuntime = false;          // stores whether during runtime the events should be logged    (if it was on, then it can be switched off, resulting in 0's being logged)

        private static bool mAllowDataVisualization = false;                                    // data visualization enabled/disabled

        private static int sourceInputChannels = 0;                                             // the number of channels coming from the source input
        private static int totalNumberOfStreams = 0;                                            // the total number of streams to be logged in the .dat file
        private static List<string> registeredStreamNames = new List<string>(0);                // the names of the registered streams to store in the .dat file
        private static List<int> registeredStreamTypes = new List<int>(0);                      // the types of the registered streams to store in the .dat file

        private static FileStream dataStream = null;                                            // filestream that is fed to the binarywriter, containing the stream of values to be written to the .dat file
        private static BinaryWriter dataStreamWriter = null;                                    // writer that writes values to the .dat file
        private static double[] streamValues = null;                                            // holds the values of all streams that are registered to be logged 
        private static uint sampleCounter = 0;                                                  // the current row of values being written to the .dat file, acts as id
        private static int valuePointer = 0;                                                    // the current location in the streamValues array that the incoming value is written to
        private static Stopwatch stopWatch = new Stopwatch();                                   // creates stopwatch to measure time difference between incoming samples
        private static double elapsedTime = 0;                                                  // amount of time [ms] elapsed since start of proccesing of previous sample

        private static int totalNumberOfVisualizationStreams = 0;                               // the total number of streams to visualize
        private static List<string> registeredVisualizationStreamNames = new List<string>(0);   // the names of the registered streams to visualize
        private static List<int> registeredVisualizationStreamTypes = new List<int>(0);         // the types of the registered streams to visualize

        // A 'collector' event(handler). An EventHandler delegate is associated with the event.
        // methods should be subscribed to this object
        public static event EventHandler<VisualizationValueArgs> newVisualizationSourceInputSample = delegate { };
        public static event EventHandler<VisualizationValueArgs> newVisualizationStreamSample = delegate { };
        public static event EventHandler<VisualizationEventArgs> newVisualizationEvent = delegate { };

        public static void Construct() {

            parameters.addParameter<bool>(
                "AllowDataVisualization",
                "Enable/disable the visualization of data.\nNote: Only turn this is off for performance benefits",
                "1");

            parameters.addParameter<string>(
                "DataDirectory",
                "Path to the directory to store the data in.\nThe path can be absolute (e.g. 'c:\\data\\') or relative (to the program executable, e.g. 'data\\').",
                "", "", "data\\");

            parameters.addParameter<string>(
                "Identifier",
                "A textual identifier.\nThe ID will be incorporated into the filename of the data files.",
                "", "", "");

            parameters.addParameter <bool>      (
                "LogSourceInput",
                "Enable/disable source input logging.\n\nNote: when there is no source input (for example when the source is a signal generator) then no source input data file will be created nor will there be any logging of source input",
                "1");

            parameters.addParameter<int>(
                "SourceInputMaxFilesize",
                "The maximum filesize for a source input data file.\nIf the data file exceeds this maximum, the data logging will continue in a sequentally numbered file with the same name.\n(set to 0 for no maximum)",
                "0");

            parameters.addParameter <bool>      (
                "LogDataStreams",
                "Enable/disable data stream logging.\nThis option will enable or disable the logging of data streams for all modules.\nEnabling or disabling specific data stream has to be done from the filter settings.\n\nNote: whether the streams that are being logged have values or zeros is dependent on the runtime configuration of the modules. It is possible\nthat the user, though an application module user-interface, sets certain streams to be (values) or not be (zeros) logged.",
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

        }

        /**
         * Configure the data class. Checks the values and application logic of the
         * parameters and, if valid, transfers the configuration parameters to local variables
         *
         * This will be called before all other configuration, which is why the registered stream are cleared here
         **/
        public static bool Configure() {

            // clear the registered source input
            sourceInputChannels = 0;

            // clear the registered streams
            registeredStreamNames.Clear();
            registeredStreamTypes.Clear();
            totalNumberOfStreams = 0;

            // clear the registered visualization streams
            registeredVisualizationStreamNames.Clear();
            registeredVisualizationStreamTypes.Clear();
            totalNumberOfVisualizationStreams = 0;

            // check and transfer visualization parameter settings
            mAllowDataVisualization = parameters.getValue<bool>("AllowDataVisualization");
            Globals.setValue<bool>("AllowDataVisualization", mAllowDataVisualization ? "1" : "0");

            // check and transfer file parameter settings
            mLogSourceInput = parameters.getValue<bool>("LogSourceInput");
            mLogSourceInputRuntime = mLogSourceInput;
            mLogDataStreams = parameters.getValue<bool>("LogDataStreams");
            mLogDataStreamsRuntime = mLogDataStreams;
            mLogEvents = parameters.getValue<bool>("LogEvents");
            mLogEventsRuntime = mLogEvents;
            // ...

            // if there is data to store, create data directory
            if (mLogSourceInput || mLogDataStreams || mLogEvents) {

                // construct path name of data directory
                String dataDir = parameters.getValue<String>("DataDirectory");
                String path = Path.Combine(Directory.GetCurrentDirectory(), dataDir);

                // create the data directory 
                try {
                    if (Directory.Exists(path)) { logger.Info("Data directory already exists."); }    
                    else {
                        Directory.CreateDirectory(path);
                        logger.Info("Created data directory at " + path );
	                 }    
                } catch (Exception e) {
                    logger.Error("Unable to create data directory at " + path + " (" + e.ToString() + ")");
                } 

            }
            return true;

        }

        /**
         * Register the format (the number of channels) in which the raw sample data from the source input will come in
         * 
         * (this should be called during configuration)
         **/
        public static void RegisterSourceInput(int numberOfChannels) {

            sourceInputChannels = numberOfChannels;

        }

        /**
         * Register the a stream
         * Every module that wants to log a stream of samples should announce every stream respectively beforehand using this function
         * 
         * (this should be called during configuration, by all sources/filters/application that will log their samples)
         **/
        public static void RegisterSampleStream(string streamName, SampleFormat streamType) {

            // register a new stream
            registeredStreamNames.Add(streamName);
            registeredStreamTypes.Add(0);

            // add one to the total number of streams
            totalNumberOfStreams++;

            // message
            logger.Debug("Registered stream '" + streamName + "' of the type ...");

        }

        /**
         * Register the visualization stream
         * Every module that wants to visualize a stream of samples should announce every stream respectively beforehand using this function
         * 
         * (this should be called during configuration, by all sources/filters/application that will log their samples)
         **/
        public static void RegisterVisualizationStream(string streamName, SampleFormat streamType) {

            // register a new stream
            registeredVisualizationStreamNames.Add(streamName);
            registeredVisualizationStreamTypes.Add(0);

            // add one to the total number of visualization streams
            totalNumberOfVisualizationStreams++;

            // message
            logger.Debug("Registered visualization stream '" + streamName + "' of the type ...");

        }

        public static void Start() {

            // TODO make subdirs (+ add param to set and check this)

            // create (and close) the parameter file

            // create the event file

            // check if we want to log the source input
            if (mLogSourceInput && sourceInputChannels > 0) {
                 
                // create a source input file

            }

            // check if there are any samples streams to be logged
            if (mLogDataStreams && totalNumberOfStreams > 0) {

                // (re)set sample counter
                sampleCounter = 1;

                // resize streamValues array in order to hold all values from all streams, and create binary version of streamValues array
                streamValues = new double[totalNumberOfStreams];

                // construct filepath of data file, with current time as filename
                String dataDir = parameters.getValue<String>("DataDirectory");
                String fileName = DateTime.Now.ToString("yyyyMMddHHmmssfff") + ".bin";
                String path = Path.Combine(Directory.GetCurrentDirectory(), dataDir, fileName);

                // create filestream: create file if it does not exists, allow to write, do not share with other processes and use buffer of 8192 bytes (roughly 1000 samples)
                try {
                    dataStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 8192);
                    dataStreamWriter = new BinaryWriter(dataStream);
                    logger.Info("Created data file at " + path);
                } catch (Exception e) { logger.Error("Unable to create data file at " + path + " (" + e.ToString() + ")"); } 
            }
        }

        public static void Stop() {

            // close the event file

            // close the source input file

            // close the data stream file
            dataStreamWriter.Close();

            // if there is still a stopwatch running, stop it
            if (stopWatch.IsRunning) { stopWatch.Stop(); }
        }

        public static void Destroy() {

            // stop the Data Class

            // Finalize stopwatch?

        }

        /**
         * Called when a sample is at the beginning of the pipeline (from before the first filter module till after the application module)
         **/
        public static void SampleProcessingStart() {

            // reset the array pointer, and start stopwatch 
            valuePointer = 0;

            // if stopwatch is running, stop and save elapsed time
            if (stopWatch.IsRunning) {
                stopWatch.Stop();
                elapsedTime = stopWatch.ElapsedMilliseconds;
                stopWatch.Reset();
            }

            // start stopwatch
            stopWatch.Start();

        }

        /**
         * Called when a sample is at the end of the pipeline (from before the first filter module till after the application module)
         **/
        public static void SampleProcessingEnd() {

            // debug, show data values being stored
            logger.Info(elapsedTime + " " + sampleCounter + " " + String.Join(",", streamValues.Select(p => p.ToString()).ToArray()));

            // data integrity check of collected values: if the pointer is not exactly at end of array, not all values have been delivered or stored, else transform to bytes and write to file
            if (valuePointer != totalNumberOfStreams) { logger.Error("Not all data values have been stored in the .dat file"); } 
            else {

                    // transform variables that will be stored in .dat to binary arrays
                    byte[] elapsedTimeBinary = BitConverter.GetBytes(elapsedTime);
                    byte[] sampleCounterBinary = BitConverter.GetBytes(sampleCounter);

                    byte[] streamValuesBinary = new byte[streamValues.Length * sizeof(double)];               
                    Buffer.BlockCopy(streamValues, 0, streamValuesBinary, 0, streamValues.Length * sizeof(double));

                    // create new array to hold all bytes
                    int l1 = elapsedTimeBinary.Length;
                    int l2 = sampleCounterBinary.Length;
                    int l3 = streamValuesBinary.Length;
                    byte[] streamOut = new byte[l1 + l2 + l3];

                    // blockcopy all bytes to this array
                    Buffer.BlockCopy(elapsedTimeBinary, 0, streamOut, 0, l1);
                    Buffer.BlockCopy(sampleCounterBinary, 0, streamOut, l1, l2);
                    Buffer.BlockCopy(streamValuesBinary, 0, streamOut, l1 + l2, l3);

                    //byte[] streamOut = elapsedTimeBinary.Concat(sampleCounterBinary, streamValuesBinary).ToArray();

                    // write data to file
                    dataStreamWriter.Write(streamOut);     
            }

            // advance sample counter, if gets to max value, reset to 0
            if (sampleCounter == uint.MaxValue) { sampleCounter = 0; } else { sampleCounter++; }
        }
        
        /**
         * Log a raw source input value to the source input file (.src) 
         * 
         **/
        public static void LogSourceInputValue(double value) {



        }

        /**
         * Log a raw stream value to the stream file (.dat) 
         * 
         **/
        public static void LogStreamValue(double value) {

            // store the incoming value in the array, advance the pointer
            if (valuePointer > totalNumberOfStreams) {
                logger.Error("More data values are coming in than the amount of registered streams. Not all data is stored in .dat file.");
            } else {
                streamValues[valuePointer] = value;
                valuePointer++;
            }
        }

        /**
         * Log events to the events file (.evt) 
         * 
         **/
        public static void LogEvent(int level, string text, string value) {

        }

        /**
         * Log a raw source input value to visualize
         * 
         **/
        public static void LogVisualizationSourceInputValue(double value) {

            VisualizationValueArgs args = new VisualizationValueArgs();
            args.value = value;
            newVisualizationSourceInputSample(null, args);

        }

        /**
         * Log a raw stream value to visualize
         * 
         **/
        public static void LogVisualizationStreamValue(double value) {

            VisualizationValueArgs args = new VisualizationValueArgs();
            args.value = value;
            newVisualizationStreamSample(null, args);

        }

        /**
         * Log raw source input data to visualize
         * 
         **/
        public static void LogVisualizationEvent(int level, string text, string value) {

            VisualizationEventArgs args = new VisualizationEventArgs();
            args.level = level;
            args.text = text;
            args.value = value;
            newVisualizationEvent(null, args);

        }

    }
}