using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        private static bool mLogSampleStreams = false;          // sample stream logging enabled/disabled (by configuration parameter)
        private static bool mLogSampleStreamsRuntime = false;   // stores whether during runtime the sample streams should be logged    (if it was on, then it can be switched off, resulting in 0's being logged)
        private static bool mLogEvents = false;                 // 
        private static bool mLogEventsRuntime = false;          // stores whether during runtime the events should be logged    (if it was on, then it can be switched off, resulting in 0's being logged)

        private static bool mAllowDataVisualization = false;                                    // data visualization enabled/disabled

        private static int sourceInputChannels = 0;                                             // the number of channels coming from the source input
        private static int totalNumberOfSampleStreams = 0;                                      // the total number of sample streams to be logged in the .dat file
        private static List<string> registeredSampleStreamNames = new List<string>(0);          // the names of the registered sample streams to store in the .dat file
        private static List<int> registeredSampleStreamTypes = new List<int>(0);                // the types of the registered sample streams to store in the .dat file

        private static int totalNumberOfVisualizationStreams = 0;                               // the total number of sample streams to visualize
        private static List<string> registeredVisualizationStreamNames = new List<string>(0);   // the names of the registered sample streams to visualize
        private static List<int> registeredVisualizationStreamTypes = new List<int>(0);         // the types of the registered sample streams to visualize


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
                "LogSampleStreams",
                "Enable/disable sample stream logging.\nThis option will enable or disable the logging of sample streams for all modules.\nEnabling or disabling specific sample streams has to be done from the filter settings.\n\nNote: whether the samples streams that are being logged have values or zeros is dependent on the runtime configuration of the modules. It is possible\nthat the user, though an application module user-interface, sets certain streams to be (values) or not be (zeros) logged.",
                "1");

            parameters.addParameter<int>(
                "SampleStreamMaxFilesize",
                "The maximum filesize for a sample stream data file.\nIf the data file exceeds this maximum, the data logging will continue in a sequentally numbered file with the same name.\n(set to 0 for no maximum)",
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

            // clear the registered sample streams
            registeredSampleStreamNames.Clear();
            registeredSampleStreamTypes.Clear();
            totalNumberOfSampleStreams = 0;

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
            mLogSampleStreams = parameters.getValue<bool>("LogSampleStreams");
            mLogSampleStreamsRuntime = mLogSampleStreams;
            mLogEvents = parameters.getValue<bool>("LogEvents");
            mLogEventsRuntime = mLogEvents;
            // ...

            if (mLogSourceInput || mLogSampleStreams || mLogEvents) {

                // check the path


                // build the filename


                // see if any of the files already exists

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
         * Register the a sample stream
         * Every module that wants to log a stream of samples should announce every stream respectively beforehand using this function
         * 
         * (this should be called during configuration, by all sources/filters/application that will log their samples)
         **/
        public static void RegisterSampleStream(string streamName, SampleFormat streamType) {

            // register a new stream
            registeredSampleStreamNames.Add(streamName);
            registeredSampleStreamTypes.Add(0);

            // add one to the total number of sample streams
            totalNumberOfSampleStreams++;

            // message
            logger.Debug("Registered sample stream '" + streamName + "' of the type ...");

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

            // create the parameter file

            // create the event file

            // check if we want to log the source input
            if (mLogSourceInput && sourceInputChannels > 0) {
                
                
                // create a source input file



            }

            // check if there are any samples streams to be logged
            if (mLogSampleStreams && totalNumberOfSampleStreams > 0) {

                // create a sample data file



            }

        }

        public static void Stop() {

            // close the sample data file

            // close the source input file

            // close the event file


        }
        
        /**
         * Log raw sample data to the source input file (.src) 
         * 
         **/
        public static void LogSourceInput(double sample) {

        }

        /**
         * Log raw sample data to the sample stream file (.dat) 
         * 
         **/
        public static void LogStreamSample(double sample) {
            
        }

        /**
         * Log raw sample data to visualize
         * 
         **/
        public static void LogVisualizationSample(double sample) {
            
        }

        /**
         * Log events to the events file (.evt) 
         * 
         **/
        public static void LogEvent(int level, string text, string value) {

        }

    }

}
