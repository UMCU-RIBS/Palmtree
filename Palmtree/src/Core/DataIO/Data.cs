/**
 * Data static class
 * 
 * This static class handles all the data and event output from the source-, filter- and application-modules
 * 
 * 
 * Copyright (C) 2017:  RIBS group (Nick Ramsey Lab), University Medical Center Utrecht (The Netherlands) & external contributors
 * Author(s):           Max van den Boom            (info@maxvandenboom.nl)
 *                      Benny van der Vijgh         (benny@vdvijgh.nl)
 * 
 * This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software
 * Foundation, either version 3 of the License, or (at your option) any later version. This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for
 * more details. You should have received a copy of the GNU General Public License along with this program. If not, see <https://www.gnu.org/licenses/>.
 */
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Xml;
using Palmtree.Core.Events;
using Palmtree.Core.Params;
using WebSocketSharp;
using WebSocketSharp.Server;
using System.Text.Json;

namespace Palmtree.Core.DataIO {

public class WSIO : WebSocketBehavior
    {
        private static NLog.Logger logger                            = LogManager.GetLogger("Data");

        public class DataStruct {
            public string eventState {get; set;}
            public string eventCode {get; set;}
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            DataStruct dataStruct = JsonSerializer.Deserialize<DataStruct>(e.Data);
            Data.logEvent(1, dataStruct.eventState, dataStruct.eventCode);
            // Send("Received");
        }
    }
    /// <summary>
    /// Data class
    /// Takes care of data storage and visualization.
    /// 
    /// 
    /// note: Static class over singleton pattern because we do not need an instance using an interface
    ///       or be passed around, also the static will be stored in stack (instead of heap) theoreically 
    ///       providing slightly better performance (important since the Data class is called upon very frequently)
    /// </summary>
    public static class Data {
        public static WebSocketServer wssv;

        private const string CLASS_NAME                         = "Data";
        private const int CLASS_VERSION                         = 3;

        private const string RUN_SUFFIX                         = "Run_";                       // suffix used to append to created files
        private const int MAX_EVENT_LOGLEVELS                   = 3;                            // maximal event log level that is available

        private static NLog.Logger logger                            = LogManager.GetLogger("Data");
        private static Parameters parameters = ParameterManager.GetParameters("Data", Parameters.ParamSetTypes.Data);
        private static Random rand = new Random(Guid.NewGuid().GetHashCode());                  // setup a random number generator
        private static double ticksPerMillisecond = Stopwatch.Frequency / 1000.0;               // calculate and set the ticks-per-millisecond (to be used later)
        private static bool running                             = false;                        // flag whether the data class is running (logging/recording)
        private static bool mCensorLogging                      = false;                        // flag whether the logging should be censored (zeros should be written instead)

        private static string dataDir                           = "";                           // location of data directory
        private static string sessionDir                        = null;                         // contains full path of directory all files of one sesison are written to
        private static string identifier                        = "";                           // file identifier, prefix in filename
        private static string fileName                          = "";                           // file name base, used as basis for all files created during life cycle of program    
        private static bool subDirPerRun                        = false;                        // whether or not a sub-directory must be made in the session directory to hold the generated files per run
        private static int run                                  = 0;                            // contains number of the current run
        private static long runStartEpoch                       = 0;                            // the epoch in milliseconds at the start of the run
        private static Stopwatch runStopWatch                   = new Stopwatch();              // creates stopwatch to measure the elapsed time since the start of the run

        // event logging
        private static bool mLogEvents                          = false;                 		// 
        private static int[] mEventLoggingLevels                = new int[0];
        private static List<FileStream> eventStreams            = new List<FileStream>(0);      // list of filestreams, one for each registered event level. Each filestream is fed to the binarywriter, containing the stream of events to be written to the .evt file
        private static List<StreamWriter> eventStreamWriters    = new List<StreamWriter>(0);    // list of writers, each writes events of specific log level to the .evt file

        // source streams
        private static bool mLogSourceInput                     = false;            			// source input logging enabled/disabled (by configuration parameter)
        private static int numSourceInputStreams                = 0;                            // the number of streams coming from the source input
        private static List<string> registeredSourceInputStreamNames = new List<string>(0);     // the names of the registered source input streams to store in the .src file
        private static List<StreamFormat> registeredSourceInputStreamFormat = new List<StreamFormat>(0);   // the formats of the registered source input streams to store in the .src file
        private static FileStream sourceStream                  = null;                         // filestream to write values to the src file
        private static uint sourceSamplePackageCounter          = 0;                            // the sample-package number that is being written to the .src file (acts as id)
        private static double sourceRunElapsedTime              = 0;                            // amount of time [ms] elapsed since the start of the run

        // pipeline input streams
        private static bool mLogPipelineInputStreams            = false;            			// stream logging for pipeline input enabled/disabled (by configuration parameter)
        private static int numPipelineInputStreams              = 0;                            // amount of pipeline input streams
        private static double pipelineSampleRate                = 0;                            // the sample rate of the pipeline (in Hz)

        // filters and application streams
        private static bool mLogFiltersAndApplicationStreams    = false;            			// stream logging for filters and application modules enabled/disabled (by configuration parameter)
        private static bool bufferPipelineValues                = false;            			// whether or not to buffer the incoming pipeline values  (by configuration parameter)


        // data streams (includes pipeline input, filter and application streams)
        private static int numDataStreams                       = 0;                            // the total number of data streams to be logged in the .dat file
        private static List<string> registeredDataStreamNames   = new List<string>(0);          // the names of the registered streams to store in the .dat file
        private static List<StreamFormat> registeredDataStreamFormats = new List<StreamFormat>(0); // the formats of the registered streams to store in the .dat file

        private static FileStream dataStream                    = null;                         // filestream that writes values to the .dat file
        private static uint dataSamplePackageCounter            = 0;                            // the sample-package number that is to be written to the .dat file (acts as id)
        private static int dataStreamIndex                      = 0;                            // the index of the stream of which we are currently expecting samples
        private static int pipelineExpectedSamplesPerTrip       = 0;                            // the total number of pipeline samples that is expected in one roundtrip
        private static int pipelineMaxSamplesStream             = 0;                            // the highest number of samples that any data stream in the pipeline would want to log
        private static int dataStreamsSampleCounter             = 0;                            // the total number of samples logged in the current pipeline tri
        private static double dataRunElapsedTime                = 0;                            // amount of time [ms] elapsed since the start of the run
        
        private static bool useDataStreamBuffer                 = false;                        // whether or not to use the data stream buffer     (buffer is only used if all streams log only one sample, or if pipeline buffering is set to enabled in the configuration)
        private static byte[] dataStreamBuffer                  = null;                         // buffer to hold the package headers and the values as bytes(!) of all data streams that are registered to be logged   (only used with data buffering - useDataStreamBuffer)
        private static int dataStreamBufferIndex                = 0;                            // the index of where to continue writing in the datastream buffer   (only used with data buffering - useDataStreamBuffer)

        // plugin streams
        private static bool mLogPluginInput = false;            								// plugin input logging enabled/disabled (by configuration parameter)
        private static int numPlugins = 0;                                                      // stores the amount of registered plugins
        private static List<string> registeredPluginNames       = new List<string>(0);          // stores the names of the registered plugins
        private static List<string> registeredPluginExtensions  = new List<string>(0);          // stores the extensions of the registered plugins
        private static List<double> registeredPluginSampleRate  = new List<double>(0);          // stores the sample-rate of the registered plugins
        private static List<List<string>> registeredPluginInputStreamNames = new List<List<string>>(0);                     // for each plugin, the names of the registered plugin input streams to store in the plugin data log file
        private static List<List<StreamFormat>> registeredPluginInputStreamFormats = new List<List<StreamFormat>>(0);       // for each plugin, the formats of the registered plugin input streams to store in the plugin data log file
        private static List<int> numPluginInputStreams          = new List<int>(0);             // for each plugin, the number of streams that will be logged

        private static List<double[]> pluginDataValues          = new List<double[]>(0);        // list of two dimensional arrays to hold plugin data as it comes in 
        private static int bufferSize                           = 10000;                        // TEMP size of buffer, ie amount of datavalues stored for each plugin before the buffer is flushed to the data file at sampleProcessingEnd
        private static List<int> bufferPointers                 = new List<int>(0);

        private static List<FileStream> pluginStreams           = new List<FileStream>(0);      // list of filestreams, one for each plugin, to be able to write to different files with different frequencies. Each filestream is fed to a binarywriter, containing the stream of values to be written to the plugin log file

        // visualization
        private static bool mEnableDataVisualization            = false;                        // data visualization enabled/disabled
        private static int numVisualizationStreams              = 0;                            // the total number of streams to visualize
        private static List<string> registeredVisualizationStreamNames = new List<string>(0);   // the names of the registered streams to visualize
        private static List<int> registeredVisualizationStreamTypes = new List<int>(0);         // the types of the registered streams to visualize
        private static double[] visualizationStreamValues       = null;
        private static int visualizationStreamValueCounter      = 0;

        // Visualization event(handler)s. An EventHandler delegate is associated with the event.
        // methods should be subscribed to this object
        public static event EventHandler<VisualizationValuesArgs> newVisualizationSourceInputValues = delegate { };
        public static event EventHandler<VisualizationValuesArgs> newVisualizationStreamValues = delegate { };
        public static event EventHandler<VisualizationEventArgs> newVisualizationEvent = delegate { };

        // 
        private static Object lockSource                        = new Object();                 // threadsafety lock for source buffer/events
        private static Object lockStream                        = new Object();                 // threadsafety lock for data-stream buffer/events
        private static Object lockPlugin                        = new Object();                 // threadsafety lock for plugin buffer/events


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

            /*
            parameters.addParameter<int>(
                "SourceInputMaxFilesize",
                "The maximum filesize for a source input data file.\nIf the data file exceeds this maximum, the data logging will continue in a sequentally numbered file with the same name.\n(set to 0 for no maximum)",
                "0");
            */

            parameters.addParameter<bool>(
                "LogPipelineInputStreams",
                "Enable/disable pipeline input stream logging.\n\nNote: The pipeline input streams need to be logged in order to playback the .dat file.",
                "1");

            parameters.addParameter<bool>(
                "LogFiltersAndApplicationStreams",
                "Enable/disable filters and application data stream logging.\nThis option will enable or disable the logging of data streams for the filters and application modules.\nEnabling or disabling data stream for a specific filter or application module has to be done in the module's settings\n\nNote: whether the streams that are being logged have values or zeros is dependent on the runtime configuration of the modules. It is possible\nthat the user, though an application module user-interface, sets certain streams to be (values) or not be (zeros) logged.",
                "1");

            parameters.addParameter<bool>(
                "BufferPipelineValues",
                "Whether or not to buffer the incoming pipeline values before writing.\nDisabling this options will result in more frequent (i.e. more spread out) disk write calls.\nBy enabling this option, all the pipeline values that need to be logged are\nbuffered with only a single write call at the end of the pipeline.\nIf all streams pass just a single sample (e.g. the number of samples per packages is 1), then the pipeline values are buffered by default.",
                "1");

            /*
            parameters.addParameter<int>(
                "SampleStreamMaxFilesize",
                "The maximum filesize for a stream data file.\nIf the data file exceeds this maximum, the data logging will continue in a sequentally numbered file with the same name.\n(set to 0 for no maximum)",
                "0");
            */

            parameters.addParameter<bool>(
                "LogEvents",
                "Enable/disable event logging.",
                "1");

            parameters.addParameter<int[]>(
                "EventLoggingLevels",
                "Indicate which levels of event logging are allowed.\n(leave empty to log all levels)\n\nNote: whether events are logged or not is also dependent on the runtime configuration of the modules. It is possible\nthat the user, though an application module user-interface, sets certain events to be or not be logged.",
                "0,1,2");

            parameters.addParameter<bool>(
                "LogPluginInput",
                "Enable/disable plugin input logging.\n\nNote: when there is no plugin input then no plugin data file will be created nor will there be any logging of plugin input",
                "1");
                parameters.addParameter<bool>(
                "WSPORT",
                "Enable/disable plugin input logging.\n\nNote: when there is no plugin input then no plugin data file will be created nor will there be any logging of plugin input",
                "21122");
           wssv = new WebSocketServer("ws://localhost:21122");
            wssv.AddWebSocketService<WSIO>("/");
            wssv.Start();

        }

        public static int getClassVersion() {
            return CLASS_VERSION;
        }

        public static string getClassName() {
            return CLASS_NAME;
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
            registeredSourceInputStreamFormat.Clear();
            numSourceInputStreams = 0;

            // clear the registered data streams
            registeredDataStreamNames.Clear();
            registeredDataStreamFormats.Clear();
            numDataStreams = 0;
            numPipelineInputStreams = 0;

            // clear the registered visualization streams
            registeredVisualizationStreamNames.Clear();
            registeredVisualizationStreamTypes.Clear();
            numVisualizationStreams = 0;

            // clear the registered plugin streams
            registeredPluginNames.Clear();
            registeredPluginExtensions.Clear();
            registeredPluginSampleRate.Clear();
            numPlugins = 0;
            registeredPluginInputStreamNames.Clear();
            registeredPluginInputStreamFormats.Clear();
            numPluginInputStreams.Clear();

            // check and transfer visualization parameter settings
            mEnableDataVisualization            = parameters.getValue<bool>("EnableDataVisualization");
            Globals.setValue<bool>("EnableDataVisualization", mEnableDataVisualization ? "1" : "0");

            // check and transfer file parameter settings
            mLogSourceInput                     = parameters.getValue<bool>("LogSourceInput");
            mLogPipelineInputStreams            = parameters.getValue<bool>("LogPipelineInputStreams");
            mLogFiltersAndApplicationStreams    = parameters.getValue<bool>("LogFiltersAndApplicationStreams");
            bufferPipelineValues                = parameters.getValue<bool>("BufferPipelineValues");
            mLogEvents                          = parameters.getValue<bool>("LogEvents");
            mEventLoggingLevels                 = parameters.getValue<int[]>("EventLoggingLevels");
            mLogPluginInput                     = parameters.getValue<bool>("LogPluginInput");

            // retrieve identifier
            identifier                          = parameters.getValue<string>("Identifier");
            if (string.IsNullOrEmpty(identifier)) {

                // message and return failure
                logger.Error("A identifier should be given for the data");
                return false;

            }

            // check if pipeline input streams logging is disabled
            if (!mLogPipelineInputStreams) {

                // message (warning)
                logger.Warn("The logging of pipeline input streams is disabled, the resulting .dat file cannot be used for playback");

            }

            //
            subDirPerRun                        = parameters.getValue<bool>("SubDirectoryPerRun");


            // if there is data to store, create data (sub-)directory
            if (mLogSourceInput || mLogPipelineInputStreams || mLogFiltersAndApplicationStreams || mLogEvents || mLogPluginInput) {

                // construct path name of data directory
                dataDir                         = parameters.getValue<string>("DataDirectory");
                dataDir = Path.Combine(Directory.GetCurrentDirectory(), dataDir);

                // construct path name of directory for this session
                string session  = identifier + "_" + DateTime.Now.ToString("yyyyMMdd");
                sessionDir = Path.Combine(dataDir, session);

                // create the session data directory 
                try {
                    if (!Directory.Exists(sessionDir)) {
                        Directory.CreateDirectory(sessionDir);
                        logger.Info("Created session data directory at " + sessionDir);
                    }
                } catch (Exception e) {
                    logger.Error("Unable to create sesion data directory at " + sessionDir + " (" + e.ToString() + ")");
                }
            }

            return true;

        }

        /**
         * Register a source input stream for future logging.
         * 
         * Every source module that wants to log a stream should announce/register their respective stream(s) before using any of the logSourceInputValues functions
         * (this should be called during configuration, by all sources that will log their samples)
         * 
         *    numSamples = The theoretical number of samples to be expected per log call (at the theoretical rate). In reality the
         *                 number of samples passed in a call to 'logSourceInputValues' can vary, since they will be written 
         *                 consecutively straight away (in contrast to Data-stream which might be buffered)
         *    rate       = The theoretical rate at which sample-packages should come in. Reality can deviate from this, however
         *                 this value will be stored in the header to give an indication of the netto sample throughput per second
         *            
         * Note: Deliberately not passing SamplePackageFormat to ensure that multiple streams (e.g. input channels of a source module) are registered
         *       by calling this function for each channel. This should prevent the expectation of all channels being registered in a single call with
         *       the number of channels being stored in the SamplePackageFormat object.
         **/
        public static void registerSourceInputStream(string streamName, int numSamples, double rate) {

            // register a new source stream
            registeredSourceInputStreamNames.Add(streamName);
            registeredSourceInputStreamFormat.Add(new StreamFormat(numSamples, rate));

            // check if newly registered stream registers with same expected amount of samples per call as rest of streams
            for (int i = 0; i < numSourceInputStreams; i++) {
                if (registeredSourceInputStreamFormat[i].numSamples != numSamples) 
                    logger.Error("Registered source input stream '" + streamName + "' contains " + numSamples + " samples per packet, which differs from other registered source input streams. This might be a mistake in the source module code, be sure that each source stream has the same number of samples per call to logSourceInputValues.");
            }  

            // add one to the total number of streams
            numSourceInputStreams++;
            
            // message
            logger.Debug("Registered source input stream '" + streamName + "'.");

        }

        public static int getNumberOfSourceInputStreams() {
            return numSourceInputStreams;
        }

        public static string[] getSourceInputStreamNames() {
            return registeredSourceInputStreamNames.ToArray();
        }
        
        // register the pipeline input based on the output format of the source
        public static void registerPipelineInput(SamplePackageFormat inputFormat) {

            // store the pipeline sample rate per second (Hz)
            pipelineSampleRate = inputFormat.packageRate * inputFormat.numSamples;
            
            // check if the pipeline input streams should be logged
            if (mLogPipelineInputStreams) {

                // use the number of input channels for the pipeline to the number of output channels from the source
                numPipelineInputStreams = inputFormat.numChannels;
                
                // register the streams
                for (int channel = 0; channel < numPipelineInputStreams; channel++)
                    Data.registerDataStream("Pipeline_Input_Ch" + (channel + 1), inputFormat.numSamples);

            }

            /*
            // TODO:
            // check if visualization is enabled
            if (mEnableDataVisualization) {

                // register the streams to visualize
                for (int channel = 0; channel < inputFormat.getNumberOfChannels(); channel++) {
                    Data.registerVisualizationStream(("Pipeline_Input_Ch" + (channel + 1)), inputFormat);

                }
            }
            */

        }

        /**
         * Register a data stream for future logging.
         * 
         * Every module that wants to log a stream should announce/register their respective stream(s) before using the logStreamValue function
         * (this should be called during configuration, by all sources/filters/application that will log their samples)
         * 
         *    numSamples = the number of samples to be expected per log call (this will most like be the number of samples per package that a module sends out)
         * 
         * Note: Deliberately not passing SamplePackageFormat to ensure that multiple streams (e.g. input/output channels of a module) are registered
         *       by calling this function for each channel. This should prevent the expectation of all channels being registered in a single call with
         *       the number of channels being stored in the SamplePackageFormat object.
         * 
         **/
        public static void registerDataStream(string streamName, int numSamples) {
            
            // register a new stream
            registeredDataStreamNames.Add(streamName);
            registeredDataStreamFormats.Add(new StreamFormat(numSamples, 0));
            
            // check if newly registered stream registers with same expected amount of samples per call as rest of streams
            for (int i = 0; i < numDataStreams; i++) {
                if (registeredDataStreamFormats[i].numSamples != numSamples) 
                    logger.Warn("Registered data stream '" + streamName + "' contains " + numSamples + " samples per packet, which differs from other registered data input streams. Therefore the samples in streams containing less samples per packet than other streams will be zeroed-out in .dat file.");
            }  

            // add one to the total number of streams
            numDataStreams++;

            // message
            logger.Debug("Registered data stream '" + streamName + "', expecting " + numSamples + " per logging call.");

        }

        /**
         * Register the visualization stream
         * Every module that wants to visualize a stream of samples should announce every stream respectively beforehand using this function
         * 
         * (this should be called during configuration, by all sources/filters/application that will log their samples)
         **/
        public static void registerVisualizationStream(string streamName, SamplePackageFormat streamFormat) {

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
        public static int registerPluginInputStream(string pluginName, string pluginExt, double pluginSampleRate, string[] streamNames, StreamFormat[] streamFormats) {

            // assign id to plugin 
            int pluginId = numPlugins;

            // make sure the extension is valid
            bool isValid = true;
            do {
                isValid = true;
                if (pluginExt.Length != 3 || string.Compare(pluginExt, "src", true) == 0 | string.Compare(pluginExt, "dat", true) == 0) {
                    isValid = false;
                } else {
                    for (int i = 0; i < registeredPluginExtensions.Count; i++) {
                        if (string.Compare(registeredPluginExtensions[i], pluginExt, true) == 0) {
                            isValid = false;
                            break;
                        }
                    }
                }
                if (!isValid) {

                    // generate random string
                    string randomChars = "abcdefghijklmnopqrstuvwxyz";
                    pluginExt = randomChars[rand.Next(randomChars.Length)].ToString();
                    pluginExt += randomChars[rand.Next(randomChars.Length)].ToString();
                    pluginExt += randomChars[rand.Next(randomChars.Length)].ToString();

                    // message
                    logger.Error("Plugin ('" + pluginName + "') extension is not 3 characters, is reserved ('src' or 'dat') or already exists, extension changed to '" + pluginExt + "', check code");
                }

            } while (!isValid);

            // register plugin
            registeredPluginNames.Add(pluginName);
            registeredPluginExtensions.Add(pluginExt);
            registeredPluginSampleRate.Add(pluginSampleRate);
            
            // register all streams for this plugin
            registeredPluginInputStreamNames.Add(new List<string>(streamNames));
            registeredPluginInputStreamFormats.Add(new List<StreamFormat>(streamFormats));
            numPluginInputStreams.Add(streamNames.Length);

            // create array to hold incoming data and set pointer in buffer to initital value
            // TODO: set size depending on difference in output frequency of plugin and source, now set to 10000 to be on the safe side
            double[] pluginData = new double[bufferSize];
            pluginDataValues.Add(pluginData);
            bufferPointers.Add(0);

            // add one to the total number of registered plugins
            numPlugins++;

            // message
            logger.Debug("Registered " + streamNames.Length + " streams for plugin " + pluginName + " with id " + pluginId + " and extension " + pluginExt);

            // return assigned id to plugin, so plugin can use this to uniquely identify the data sent to this class
            return pluginId;
        }

        public static int getNumberOfPlugins() {
            return numPlugins;
        }

        public static int getNumberOfPluginInputStreams(int pluginId) {
            return registeredPluginInputStreamNames[pluginId].Count;
        }

        // TODO: use parameterobject instead of paramterset (and name?)
        public static bool adjustXML(string parameterSet, string parameterName, string value) {

            // check if required input is given
            if (string.IsNullOrEmpty(parameterSet) || string.IsNullOrEmpty(parameterSet)) {
                logger.Error("Parameterset or parameter name for adjusting XML not given.");
                return false;
            } else {

                // initialize stream and XmlDocument object
                XmlDocument paramFile = new XmlDocument();
                string xmlFile = Path.Combine(sessionDir, fileName + ".prm");

                // load the param file
                try {
                    paramFile.Load(xmlFile);
                } catch (Exception e) {
                    logger.Error("Error: Could not read parameter file " + xmlFile + " (" + e.Message + ")");
                    return false;
                }

                // retrieve node (attribute) that needs to be adjusted
                string xpathString = "/root/parameterSet[@name='" + parameterSet + "']/param[@name='" + parameterName + "']/@value";
                XmlNode result = paramFile.SelectSingleNode(xpathString);

                // if result is not empty 
                if (result != null) {

                    // update node and save back to XML
                    result.Value = value;
                    try {
                        paramFile.Save(xmlFile);
                    } catch (Exception e) {
                        logger.Error("Error: Could not save adjusted parameter file " + xmlFile + " (" + e.Message + ")");
                        return false;
                    }
                    //logger.Debug("Saved new value: " + result.Value + " to node " + parameterName + " in parameter set " + parameterSet);
                    return true;
                } else {
                    logger.Info("Node for updating can not be found in parameter file.");
                    return false;
                }
            }
        }

        public static string[] getPluginInputStreamNames(int pluginId) {
            return registeredPluginInputStreamNames[pluginId].ToArray();
        }

        public static void start() {

            lock (lockSource) {
                lock (lockStream) {
                    lock (lockPlugin) {
                        
                        // if there is data to store, create data (sub-)directory
                        if (mLogSourceInput || mLogPipelineInputStreams || mLogFiltersAndApplicationStreams || mLogEvents || mLogPluginInput) {

                            // check to see if there are already log files in session directory
                            string[] files = Directory.GetFiles(sessionDir, "*" + RUN_SUFFIX + "*");
                            if (files.Length != 0) {

                                // init runs array to hold run numbers
                                int[] runs = new int[files.Length];

                                // cycle through files in sessionDir
                                for (int f = 0; f < files.Length; f++) {

                                    // get run numbers by removing part before runsuffix and then removing extension for each log file
                                    files[f] = files[f].Substring(files[f].LastIndexOf(RUN_SUFFIX) + RUN_SUFFIX.Length);
                                    files[f] = files[f].Substring(0, files[f].IndexOf('.'));

                                    // convert to ints for reliable sorting
                                    if (!int.TryParse(files[f], out runs[f])) 
                                        logger.Error("Can not convert filenames in session directory (" + sessionDir + "). Check if files have been manually added or renamed. Remove or restore these files and re-run application.");

                                }

                                // sort runs array and take last item (= highest), and increase by one
                                Array.Sort(runs);
                                run = runs[runs.Length - 1] + 1;

                            } else {
                                run = 0;
                            }

                            // get identifier and current time to use as filenames
                            fileName = identifier + "_" + DateTime.Now.ToString("yyyyMMdd") + "_" + RUN_SUFFIX + run;

                            // (re)set the source and data sample-package counters
                            sourceSamplePackageCounter = 0;
                            dataSamplePackageCounter = 0;

                            // create parameter file and save current parameters
                            Dictionary<string, Parameters> localParamSets = ParameterManager.getParameterSetsClone();
                            ParameterManager.saveParameterFile(Path.Combine(sessionDir, fileName + ".prm"), localParamSets);

                            // set the start epoch and start the run stopwatch
                            runStartEpoch = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                            runStopWatch = Stopwatch.StartNew();

                            // check if we want to log events
                            if (mLogEvents) {

                                // if no loglevels are defined, we log all events levels, otherwise get amount of event levels we are logging
                                if (mEventLoggingLevels.Length == 0) {
                                    mEventLoggingLevels = new int[MAX_EVENT_LOGLEVELS];
                                    for (int i = 0; i < MAX_EVENT_LOGLEVELS; i++)   mEventLoggingLevels[i] = i + 1;
                                }

                                // for each desired logging level, create writer and attach id
                                for (int i = 0; i < mEventLoggingLevels.Length; i++) {

                                    // retrieve the log level
                                    int logLevel = mEventLoggingLevels[i];

                                    // construct filepath of event file, with current time and loglevel as filename 
                                    string fileNameEvt = identifier + "_" + DateTime.Now.ToString("yyyyMMdd") + "_level" + logLevel + "_" + RUN_SUFFIX + run + ".evt";
                                    string path = Path.Combine(sessionDir, fileNameEvt);

                                    // create filestream: create file if it does not exists, allow to write, do not share with other processes and use buffer of 8192 bytes (roughly 1000 samples)
                                    try {
                                        eventStreams.Add(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 8192));
                                        eventStreamWriters.Add(new StreamWriter(eventStreams[i]));
                                        eventStreamWriters[i].AutoFlush = true;                                         // ensures that after every write operation content of stream is flushed to file

                                        logger.Info("Created event file for log level " + logLevel + " at " + path);

                                    } catch (Exception e) {
                                        logger.Error("Unable to create event file at " + path + " (" + e.ToString() + ")");
                                    }

                                    // build event header string
                                    string eventHeader = "Time "+ "Elapsed " + "src_sample_ID " + "dat_sample_ID " + "Event_code " + "Event_value";

                                    // write header to event file
                                    try {
                                        eventStreamWriters[i].WriteLine(eventHeader);
                                    } catch (IOException e) {
                                        logger.Error("Can't write header to event file: " + e.Message);
                                    }

                                }

                            }

                            // check if we want to log the source input
                            if (mLogSourceInput && numSourceInputStreams > 0) {

                                // log the source start event
                                logEvent(1, "SourceStart", "");

                                // construct filepath of source file, with current time as filename 
                                string fileNameSrc = fileName + ".src";
                                string path = Path.Combine(sessionDir, fileNameSrc);

                                // create filestream (buffer of 8192 bytes, roughly 1000 samples)
                                try {
                                    sourceStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 8192);
                                    logger.Info("Created source file at " + path);
                                } catch (Exception e) {
                                    logger.Error("Unable to create source file at " + path + " (" + e.ToString() + ")");
                                }

                                // create header
                                DataHeader header           = new DataHeader();
                                header.version              = 2;
                                header.code                 = "src";
                                header.runStartEpoch        = runStartEpoch;
                                header.fileStartEpoch       = runStartEpoch;
                                header.sampleRate           = MainThread.getSource().getInputSamplesPerSecond();
                                header.numPlaybackStreams   = numSourceInputStreams;
                                header.numStreams           = numSourceInputStreams;
                                header.columnNames          = registeredSourceInputStreamNames.ToArray();
                                for (int i = 0; i < numSourceInputStreams; i++) {

                                    // currently not used, but set to 0 = double
                                    header.streamDataTypes.Add(0);

                                    // the amount of samples in the source data is allowed to
                                    // vary per log call and is therefore set to 0
                                    header.streamDataSamplesPerPackage.Add(0);

                                }

                                // write header
                                if (sourceStream != null)
                                    DataWriter.writeBinaryHeader(sourceStream, header);
                                
                            }

                            // check if there are any samples streams to be logged
                            if ((mLogPipelineInputStreams || mLogFiltersAndApplicationStreams) && numDataStreams > 0) {
                                
                                // log the data start event
                                logEvent(1, "DataStart", "");

                                // construct filepath of data file, with current time as filename 
                                string fileNameDat          = fileName + ".dat";
                                string path                 = Path.Combine(sessionDir, fileNameDat);
                                try {

                                    // create filestream: create file if it does not exists, allow to write, do not share with other processes and use buffer of 8192 bytes (roughly 1000 samples)
                                    dataStream              = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 8192);

                                    // message
                                    logger.Info("Created data file at " + path);

                                } catch (Exception e) {
                                    logger.Error("Unable to create data file at " + path + " (" + e.ToString() + ")");
                                }

                                // create header
                                DataHeader header           = new DataHeader();
                                header.version              = 2;
                                header.code                 = "dat";
                                header.runStartEpoch        = runStartEpoch;
                                header.fileStartEpoch       = runStartEpoch;
                                header.sampleRate           = pipelineSampleRate;
                                header.numPlaybackStreams   = numPipelineInputStreams;
                                header.numStreams           = numDataStreams;
                                header.columnNames          = registeredDataStreamNames.ToArray();
                                for (int i = 0; i < numDataStreams; i++) {
                                    
                                    // type currently not used, but set to 0 = double
                                    header.streamDataTypes.Add(0);

                                    // set the amount of samples that are expected for each package
                                    header.streamDataSamplesPerPackage.Add((ushort)registeredDataStreamFormats[i].numSamples);

                                }

                                // write header
                                if (dataStream != null)
                                    DataWriter.writeBinaryHeader(dataStream, header);


                                // determine the expected samples per pipeline-trip and the maximum number of
                                // samples that any stream is expected to log (this is used to initialize a buffer if needed)
                                pipelineExpectedSamplesPerTrip = 0;
                                pipelineMaxSamplesStream = 0;
                                for (int i = 0; i < numDataStreams; i++) {
                                    pipelineExpectedSamplesPerTrip += registeredDataStreamFormats[i].numSamples;
                                    if (registeredDataStreamFormats[i].numSamples > pipelineMaxSamplesStream)
                                        pipelineMaxSamplesStream = registeredDataStreamFormats[i].numSamples;
                                }
                                
                                // determine whether to use a data stream buffer (either if all streams log only one sample, or if pipeline buffering is set to enabled)
                                useDataStreamBuffer = pipelineMaxSamplesStream == 1 || useDataStreamBuffer;

                                // initialize the data stream buffer (if needed)
                                if (useDataStreamBuffer) {

                                    // determine the pipeline buffer size
                                    // for every pipeline-trip of a package the sample-package ID (ulong) + elapsed (double) will be stored
                                    int pipelineSize = sizeof(ulong) + sizeof(double);
                                    if (pipelineMaxSamplesStream == 1) {
                                        // each of the streams expects just one single sample
                                    
                                        // include size for the exact number of streams (doubles)
                                        pipelineSize += numDataStreams * sizeof(double);

                                    } else {
                                        // if buffered (by config) with multiple samples per stream

                                        // as the required size assume the maximum, that every stream will be logged seperately with the maximum number of samples per pipeline-trip
                                        // include size for the number of pipeline streams (double) * (#streams (ushort) + #samples (ushort) + maxSamples (doubles))
                                        pipelineSize += numDataStreams * (sizeof(ushort) + sizeof(ushort) + (pipelineMaxSamplesStream * sizeof(double)));

                                    }

                                    // initialize the data stream array (in bytes)
                                    dataStreamBuffer = new byte[pipelineSize];

                                }

                            }

                            // check if we want to log the plugin input
                            if (mLogPluginInput && numPlugins > 0) {

                                // for each plugin, create stream, writer and file
                                for (int i = 0; i < numPlugins; i++) {

                                    // log the source start event
                                    logEvent(1, "PluginLogStart", "plugin id: " + i);

                                    // construct filepath of plugin data file, with current time and name of plugin as filename 
                                    string fileNamePlugin   = fileName + "." + registeredPluginExtensions[i];
                                    string path             = Path.Combine(sessionDir, fileNamePlugin);

                                    // create filestream: create file if it does not exists, allow to write, do not share with other processes and use buffer of 8192 bytes (roughly 1000 samples)
                                    try {
                                        pluginStreams.Add(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 8192));
                                        logger.Info("Created plugin data log file for plugin " + registeredPluginNames[i] + " at " + path);
                                    } catch (Exception e) {
                                        logger.Error("Unable to create plugin data log file for plugin " + registeredPluginNames[i] + " at " + path + " (" + e.ToString() + ")");
                                    }

                                    // create header
                                    DataHeader header       = new DataHeader();
                                    header.version          = 1;
                                    header.code             = registeredPluginExtensions[i];
                                    header.sampleRate       = registeredPluginSampleRate[i];
                                    header.numPlaybackStreams = numPluginInputStreams[i];
                                    header.numStreams       = numPluginInputStreams[i];
                                    header.columnNames      = registeredPluginInputStreamNames[i].ToArray();

                                    // write header
                                    if (pluginStreams[i] != null) {
                                        DataWriter.writeBinaryHeader(pluginStreams[i], header);
                                    }
                                }

                            }

                        }

                        /*
                        // TODO:
                        // check if data visualization is enabled
                        if (mEnableDataVisualization) {

                            // size the array to fit all the streams
                            visualizationStreamValues = new double[numVisualizationStreams];

                        }
                        */
                        
                        // flag the data class as running (logging/recording)
                        running = true;

                    }   // end lock

                }   // end lock

            }   // end lock

        }

        public static void stop() {

            lock (lockSource) {
                lock (lockStream) {
                    lock (lockPlugin) {
                        
                        // stop and close source stream 
                        if (sourceStream != null) {

                            // log the source stop event
                            logEvent(1, "SourceStop", "");

                            // close the source stream file
                            sourceStream.Close();
                            sourceStream = null;

                        }
 

                        // stop and close data stream 
                        if (dataStream != null) {

                            // log the data stop event
                            logEvent(1, "DataStop", "");

                            // if the .src and .dat files are not on the same sample-package id's and we are writing data to src
                            //if ((sourceSamplePackageCounter != dataSamplePackageCounter) & (mLogSourceInput && numSourceInputStreams > 0)) {
                                // logger.Error("source and data sample counter do not match");
                            //}

                            // close the data stream file
                            dataStream.Close();
                            dataStream = null;

                            // clear buffer
                            dataStreamBuffer = null;

                        }

                        //  flush any remaining data in plugin buffers to files
                        writePluginData(-1);

                        // stop and close all plugin data streams
                        for (int i = 0; i < pluginStreams.Count; i++) {

                            // if plugin data stream exists
                            if (pluginStreams[i] != null) {
                             
                                // log the plugin log stop event
                                logEvent(1, "PluginLogStop", "plugin id: " + i);

                                // close the plugin stream file
                                pluginStreams[i].Close();
                                pluginStreams[i] = null;

                                // clear buffers
                                pluginDataValues[i] = null;

                            }

                        }
                        pluginStreams.Clear();

                        // stop stopwatch
                        if (runStopWatch.IsRunning)     runStopWatch.Stop();

                        // lastly, close all event writers (so any remaining events can still be written)
                        for (int i = 0; i < eventStreams.Count; i++) {
                            if (eventStreamWriters[i] != null) {
                                eventStreamWriters[i].Close();
                                eventStreamWriters[i] = null;
                                eventStreams[i] = null;
                            }
                        }

                        // clear the lists containing event writers
                        eventStreamWriters.Clear();
                        eventStreams.Clear();

                        // flag the data class as stopped (logging/recording)
                        running = false;

                    }   // end lock
                }   // end lock
            }   // end lock

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
         * Called when a sample-package is at the beginning of the pipeline (before the first filter module)
         **/
        public static void pipelineProcessingStart() {

            // thread safety lock (data)stream
            lock (lockStream) {
                if (!running)   return;

                // check if data logging is enabled
                if (mLogPipelineInputStreams || mLogFiltersAndApplicationStreams) {

                    // reset the data-stream index to be at the first data stream
                    dataStreamIndex = 0;

                    // reset the sample counter and the buffer write cursor
                    dataStreamBufferIndex = 0;
                    dataStreamsSampleCounter = 0;

                    // store the milliseconds past since the start of the run
                    dataRunElapsedTime = runStopWatch.ElapsedTicks / ticksPerMillisecond;

                    // convert the package details to byte form
                    byte[] bDataSamplePackageCounter = BitConverter.GetBytes(dataSamplePackageCounter);
                    byte[] bDataElapsedTime = BitConverter.GetBytes(dataRunElapsedTime);

                    // store the sample-package ID and elapsed, either in the buffer or already write to
                    // the disk (so data chunk headers and values can be written straight after)
                    if (useDataStreamBuffer) {

                        Buffer.BlockCopy(bDataSamplePackageCounter, 0, dataStreamBuffer, dataStreamBufferIndex, bDataSamplePackageCounter.Length);
                        dataStreamBufferIndex += bDataSamplePackageCounter.Length;
                        Buffer.BlockCopy(bDataElapsedTime, 0, dataStreamBuffer, dataStreamBufferIndex, bDataElapsedTime .Length);
                        dataStreamBufferIndex += bDataElapsedTime.Length;

                    } else {
                        
                        if (dataStream != null) {
                            dataStream.Write(bDataSamplePackageCounter, 0, bDataSamplePackageCounter.Length);
                            dataStream.Write(bDataElapsedTime, 0, bDataElapsedTime.Length);
                        }

                    }

                }

            }   // end (data-)stream lock

            // reset the visualization data stream counter
            visualizationStreamValueCounter = 0;

        }

        /**
         * Called when a sample-package is at the end of the pipeline (after the application module)
         **/
        public static void pipelineProcessingEnd() {
            
            // thread safety lock (data)stream
            lock (lockStream) {
                if (!running)   return;
                
                // check if data logging is enabled
                if (mLogPipelineInputStreams || mLogFiltersAndApplicationStreams) {
                    
                    // integrity check of collected data stream values: if the pointer is not exactly at
                    // end of array, not all streams have been delivered or stored
                    if (dataStreamIndex != numDataStreams) {
                        logger.Error("A different number of data streams have been delivered this pipeline-trip (" + dataStreamIndex + ") than expected/registered (" + numDataStreams + "), unreliable .dat file, check code");
                        return;
                    }
                    
                    // integrity check of collected data values: if the sample counter is not exactly the same as
                    // the expected number of samples per trip, then not all values have been delivered or stored
                    if (dataStreamsSampleCounter != pipelineExpectedSamplesPerTrip) {
                        logger.Error("A different total of sample-values have been delived this pipeline-trip (" + dataStreamsSampleCounter + ") than expected (" + pipelineExpectedSamplesPerTrip + "), unreliable .dat file, check code");
                        return;
                    }
                    
                    // write the data in the buffer
                    if (useDataStreamBuffer && dataStream != null)
                        dataStream.Write(dataStreamBuffer, 0, dataStreamBufferIndex);

                    /*
                    // V1

                    // integrity check of collected data stream values: if the pointer is not exactly at end of array, not all values have been
                    // delivered or stored, else transform to bytes and write to file
                    if (dataStreamIndex != numDataStreams) {

                        // message
                        logger.Error("Less data values have been logged (" + dataStreamIndex + ") than expected/registered (" + numDataStreams + ") for logging, unreliable .dat file, check code");

                    } else {

                        // transform variables that will be stored in .dat to binary arrays (except for dataStreamBuffer array which is copied directly)
                        byte[] dataSampleCounterBinary = BitConverter.GetBytes(dataSampleCounter);
                        byte[] dataElapsedTimeBinary = BitConverter.GetBytes(dataRunElapsedTime);

                        // create new array to hold all bytes
                        int l1 = dataSampleCounterBinary.Length;
                        int l2 = dataElapsedTimeBinary.Length;
                        int l3 = dataStreamBuffer.Length * sizeof(double);
                        byte[] streamOut = new byte[l1 + l2 + l3];

                        // blockcopy all bytes to this array
                        Buffer.BlockCopy(dataSampleCounterBinary, 0, streamOut, 0, l1);
                        Buffer.BlockCopy(dataElapsedTimeBinary, 0, streamOut, l1, l2);
                        Buffer.BlockCopy(dataStreamBuffer, 0, streamOut, l1 + l2, l3);

                        // write data to file
                        if (dataStream != null) {
                            dataStream.Write(streamOut, 0, streamOut.Length);
                        }

                    }

                    // clear the array to 0
                    for (int i = 0; i < dataStreamBuffer.Length; i++) {
                        dataStreamBuffer[i] = 0;
                    }
                    */

                }

                // advance sample-package counter (controlled overflow)
                // note: also count when not logging in case we want to use the counter in visualization
                if (++dataSamplePackageCounter == uint.MaxValue)
                    dataSamplePackageCounter = 0;

            }   // end (data-)stream lock

            /*
            // TODO:
            // check if data visualization is enabled
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
            */

        }
        
        /**
         * Log raw source input values and push them to be written to the source input file (.src) 
         * 
         **/
        public static void logSourceInputValues(double[] values) {
            int totalNumValues = values.Length;

            lock (lockSource) {
                if (!running)   return;
                
                // check if source logging is enabled
                if (mLogSourceInput) {
                    
                    // integrity check, the incoming number of samples should be at least a multiple of the number of expected streams
                    if (totalNumValues % numSourceInputStreams != 0) {
                        logger.Error("Deviant amount of source samples arrived to be logged: " + totalNumValues + " samples. This number is not a multiple of the number of expected/registered source input streams (" + numSourceInputStreams + "), unreliable .src file, check code.");
                        return;
                    }

                    // if censorship should be applied, then create an array with zeros instead of values (do not change the size of the input)
                    if (mCensorLogging)     values = new double[totalNumValues];

                    // get the time since the start of the run
                    sourceRunElapsedTime = runStopWatch.ElapsedTicks / ticksPerMillisecond;

                    // calculate the total number of samples
                    ushort numSamples = (ushort)(totalNumValues / numSourceInputStreams);
                    
                    // write data to source stream
                    if (sourceStream != null) {

                        // convert the package details to byte form
                        byte[] bSourceSamplePackageCounter = BitConverter.GetBytes(sourceSamplePackageCounter);
                        byte[] bSourceElapsedTime = BitConverter.GetBytes(sourceRunElapsedTime);
                        byte[] bSourceNumSamples = BitConverter.GetBytes(numSamples);
                    
                        // package start
                        sourceStream.Write(bSourceSamplePackageCounter, 0, bSourceSamplePackageCounter.Length);
                        sourceStream.Write(bSourceElapsedTime, 0, bSourceElapsedTime.Length);
                        sourceStream.Write(bSourceNumSamples, 0, bSourceNumSamples.Length);

                        // package data
                        byte[] streamOut = new byte[totalNumValues * sizeof(double)];
                        Buffer.BlockCopy(values, 0, streamOut, 0, streamOut.Length);
                        sourceStream.Write(streamOut, 0, streamOut.Length);

                    }

                    /*
                    // old V1 data format

                    // calculate the length in number of bytes for various counters
                    lenSrcSmplCountBin          = BitConverter.GetBytes(sourceSamplePackageCounter).Length;
                    lenSrcElapsedBin            = BitConverter.GetBytes(sourceRunElapsedTime).Length;
                    lenSrcInSteamsBin           = numSourceInputStreams * sizeof(double);
                    lenSrcSampleBin             = lenSrcSmplCountBin + lenSrcElapsedBin + lenSrcInSteamsBin;

                    // create new array to hold the bytes for all of the incoming data
                    byte[] streamOut = null;

                    // check how many samples
                    if (totalNumValues > numSourceInputStreams) {
                        // multiple samples
                            
                        // initialize the array to be big enough for the multiple samples
                        streamOut = new byte[lenSrcSampleBin * (totalNumValues / numSourceInputStreams)];

                        // calculate the total number of bytes for all the incoming values
                        int lenTotalValuesBin = totalNumValues * sizeof(double);

                        // loop over the input values/samples (in steps of bytes of the total of the input-streams)
                        int inputArrayIndex = 0;
                        int outputArrayIndex = 0;
                        while (inputArrayIndex != lenTotalValuesBin) {

                            // copy all parts (counter, elapsed and sample-values) to the larger output array
                            // and increase the byte-index for the output array with each part
                            Buffer.BlockCopy(bSourceSamplePackageCounter, 0, streamOut, outputArrayIndex, lenSrcSmplCountBin);
                            outputArrayIndex += lenSrcSmplCountBin;
                            Buffer.BlockCopy(bSourceElapsedTime, 0, streamOut, outputArrayIndex, lenSrcElapsedBin);
                            outputArrayIndex += lenSrcElapsedBin;
                            Buffer.BlockCopy(sourceStreamValues, inputArrayIndex, streamOut, outputArrayIndex, lenSrcInSteamsBin);
                            outputArrayIndex += lenSrcInSteamsBin;

                            // increase the byte-index for the input array (to the next sample)
                            inputArrayIndex += lenSrcInSteamsBin;

                        }

                    } else {
                        // single sample

                        // blockcopy all bytes to this array
                        streamOut = new byte[lenSrcSampleBin];
                        Buffer.BlockCopy(bSourceSamplePackageCounter, 0, streamOut, 0, lenSrcSmplCountBin);
                        Buffer.BlockCopy(bSourceElapsedTime, 0, streamOut, lenSrcSmplCountBin, lenSrcElapsedBin);
                        Buffer.BlockCopy(sourceStreamValues, 0, streamOut, lenSrcSmplCountBin + lenSrcElapsedBin, lenSrcInSteamsBin);

                    }

                    // write source to file
                    if (sourceStream != null)
                        sourceStream.Write(streamOut, 0, streamOut.Length);
                    */

                }
                
                // advance sample-package counter (controller overflow)
                // note: also count when not logging in case we want to use the counter in visualization
                if (++sourceSamplePackageCounter == uint.MaxValue)
                    sourceSamplePackageCounter = 0;

                /*
                // TODO:
                // check if data visualization is enabled
                if (mEnableDataVisualization) {

                    // trigger a new source input values event for visualization
                    VisualizationValuesArgs args = new VisualizationValuesArgs();
                    args.values = sourceStreamValues;
                    newVisualizationSourceInputValues(null, args);

                }
                */

            }   // end lock
        }

        public static void logSourceInputValues(short[] values) {

            // TODO: temp until we have a standard format, we might want to store as short
            // but for now convert shorts to double and call the double[] overload
            double[] dblValues = new double[values.Length];
            for (int i = 0; i < values.Length; i++) {
                dblValues[i] = values[i];
            }
            logSourceInputValues(dblValues);

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
         * Log one or more pipeline input sample-values for all of the pipeline-streams to the data file (.dat)
         * 
         * values       = the sample-values that need to be logged
         **/        
        public static void logPipelineInputStreamValues(double[] values) {

            // check if pipeline input streams are logged and (if they are) log the value
            if (mLogPipelineInputStreams)  
                logDataStreamValues(numPipelineInputStreams, values);

        }

        /**
         * Log one or more sample-values of one or more streams to the data file (.dat)
         * 
         * numStreams   = the number of data-streams that are spanned by the given sample-values
         * values       = the sample-values that need to be logged
         **/
        public static void logDataStreamValues(int numStreams, double[] values) {
            if (numStreams == 0) {
                logger.Error("Cannot log 0 streams, check code");
                return;
            }

            lock (lockStream) {
                if (!running)   return;

                // check if data logging is enabled
                if (mLogPipelineInputStreams || mLogFiltersAndApplicationStreams) {
                    
                    // integrity check, check if the number of streams to be logged is within the range of expected streams
                    if (dataStreamIndex + numStreams > numDataStreams) {
                        logger.Error("More data streams are being delivered than have been registered for logging, discarded, check code (currently logging at " + dataStreamIndex + ")");
                        return;
                    }
                    
                    // integrity check, the incoming number of samples should be at least a multiple of the number of expected streams
                    if (values.Length % numStreams != 0) {
                        logger.Error("Deviant amount of data samples arrived to be logged: " + values.Length + " samples. This number of samples is not a multiple of the number of streams indicated in the function call (" + numStreams + "), unreliable .dat file, check code.");
                        return;
                    }

                    // prevent partial stream logging
                    if (numStreams == 1) {
                        // if only one stream is logged
                        
                        // the input values should hold the same amount of sample-values that are expected for that stream
                        if (values.Length < registeredDataStreamFormats[dataStreamIndex].numSamples) {
                            logger.Error("Trying to log more samples (" + values.Length + ") than were announced/expected for the current data stream (" + dataStreamIndex + " - " + registeredDataStreamNames[dataStreamIndex] + "). When logging for a single data stream, make sure the amount of values that are passed are equal to the amount of values that are expected for that stream.");
                            return;
                        }

                    } else {
                        // if multiple streams are logged

                        // TODO: extra integrity check, on multiple streams, check if the values exactly fill out the number of samples expected over the streams
                        //       requires looping from the current stream, check might have a performance hit when having a lot of streams
                        //       the total will be an integrity check on this as well (total should be equal to expected)

                    }

                    // calculate the number of samples
                    int numSamples = values.Length / numStreams;

                    // integrity check, check whether the total number of samples to be logged does not exceed the total number of expected samples
                    if (dataStreamsSampleCounter + numSamples > pipelineExpectedSamplesPerTrip) {
                        logger.Error("The total number of incoming data samples exceed the total number of samples that are expected, discarded, check code (currently logging at " + dataStreamIndex + " - " + registeredDataStreamNames[dataStreamIndex] + ")");
                        return;
                    }

                    // if censorship should be applied to logging, log 0's
                    if (mCensorLogging)     values = new double[values.Length];
                    
                    // write the samples (to either the buffer or disk)
                    if (useDataStreamBuffer) {
                        // to buffer

                        if (pipelineMaxSamplesStream > 1) {
                            // with multiple samples per stream (with sample-chunk-header)
                            
                            // write the sample-chunk-header
                            byte[] bNumStreams = BitConverter.GetBytes((ushort)numStreams);
                            byte[] bNumSamples = BitConverter.GetBytes((ushort)numSamples);
                            Buffer.BlockCopy(bNumStreams, 0, dataStreamBuffer, dataStreamBufferIndex, bNumStreams.Length);
                            dataStreamBufferIndex += bNumStreams.Length;
                            Buffer.BlockCopy(bNumSamples, 0, dataStreamBuffer, dataStreamBufferIndex, bNumSamples.Length);
                            dataStreamBufferIndex += bNumSamples.Length;
                            
                        }

                        // Note: when pipelineMaxSamplesStream == 1, then each of the streams expects just one single 
                        //       sample, so then each sample-package will have just one single sample and the general data header
                        //       provides all the information we need, therefore, we can write each pipeline-trip without the 
                        //       overhead of a sample-chunk-headers

                        // copy the sample-values (doubles) to the data buffer (in bytes)
                        Buffer.BlockCopy(values, 0, dataStreamBuffer, dataStreamBufferIndex, values.Length * sizeof(double));
                        dataStreamBufferIndex += values.Length * sizeof(double);
                            
                    } else {
                        // no use of buffer, write immediately
                        // (so at least one stream has more than 1 sample, which automatically means chunk-info should be written)

                        if (dataStream != null) {
                            
                            // write chunk-info
                            byte[] bNumStreams = BitConverter.GetBytes((ushort)numStreams);
                            byte[] bNumSamples = BitConverter.GetBytes((ushort)numSamples);
                            dataStream.Write(bNumStreams, 0, bNumStreams.Length);
                            dataStream.Write(bNumSamples, 0, bNumSamples.Length);
                            
                            // write data
                            byte[] bValues = new byte[values.Length * sizeof(double)];
                            Buffer.BlockCopy(values, 0, bValues, 0, bValues.Length);
                            dataStream.Write(bValues, 0, bValues.Length);
                            
                        }

                    }

                    // raise the stream index and count the total number of sample in the pipeline-trip
                    dataStreamIndex += numStreams;
                    dataStreamsSampleCounter += values.Length;

                }

            }

        }
        
        /**
         * Log events to the events file (.evt) 
         * 
         **/
        public static void logEvent(int level, string text, string value) {
            if (!running)   return;

            // retrieve the source and data sample
            string strsourceSamplePackageCounter = "";
            lock (lockSource) {
                strsourceSamplePackageCounter = sourceSamplePackageCounter.ToString();
            }
            string strDataSampleCounter = "";
            lock (lockStream) {
                strDataSampleCounter = dataSamplePackageCounter.ToString();
            }

            // check if event logging of this level is allowed
            if (mLogEvents) {

                // determine the index of the level in the array
                int levelIndex = Array.IndexOf(mEventLoggingLevels, level);
                if (levelIndex > -1) {

                    // get the data-time of event
                    DateTime eventTime = DateTime.Now;
                    
                    // claculate the milliseconds since the start of the run
                    double eventRunElapsedTime = runStopWatch.ElapsedTicks / ticksPerMillisecond;

                    // if no value given, log '-'for value to keep consistent number of fields per row in event file 
                    if (string.IsNullOrEmpty(value)) value = "-";

                    // construct event String    
                    string eventOut = eventTime.ToString("yyyyMMdd_HHmmss_fff") + " " + eventRunElapsedTime + " " + strsourceSamplePackageCounter + " " + strDataSampleCounter + " " + text + " " + value;
                wssv.WebSocketServices["/"].Sessions.Broadcast(eventOut);

                    // write event to event file
                    if (eventStreamWriters.Count > levelIndex && eventStreamWriters[levelIndex] != null) {

                        try {

                            // try to write the line
                            eventStreamWriters[levelIndex].WriteLine(eventOut);

                            // debug
                            //logger.Debug("Event logged: " + eventOut);

                        } catch (IOException e) {
                            logger.Error("Can't write to event ('" + eventOut + "') to file: " + e.Message);
                        }

                    } else {
                        logger.Error("Trying to write to a non-existing or closed writer, check code.");
                    }

                }

            }

        }

        /**
       * Log a plugin value to the buffer to later write to file
       * 
       **/
        public static void logPluginDataValue(int pluginId, double[] values) {
            
            // lock plugin for thread safety
            lock (lockPlugin) {
                if (!running)   return;

                // check if data logging is enabled
                if (mLogPluginInput) {
                    
                    // check if the buffer is full (+ 1 because of the sample-package id
                    if ((bufferPointers[pluginId] + (values.Length + 1)) > bufferSize) {
                        logger.Error("Plugin " + pluginId + " is trying to log more values than the buffer can hold, data discarded");
                        return;
                    }
                    
                    // if censorship should be applied, then log 0's
                    if (mCensorLogging)     values = new double[values.Length];

                    // TODO: lock for dataSampleCounter? Maybe no need to lock because nothing happens at this point (what about inbetween stop/start from other thread)
                    double dblDataSampleCounter;
                    //lock (lockStream) {
                        dblDataSampleCounter = dataSamplePackageCounter;
                    //}

                    // store current sample-package id to be able to synchronize the plugin data
                    pluginDataValues[pluginId][bufferPointers[pluginId]] = dblDataSampleCounter;
                    bufferPointers[pluginId]++;
                    //logger.Info("Logging input, logging sample counter: " + dblDataSampleCounter + "(" + bufferPointers[pluginId] + ")");

                    // store plugin data values
                    Buffer.BlockCopy(   values, 0, pluginDataValues[pluginId], bufferPointers[pluginId], values.Length * sizeof(double));
                    bufferPointers[pluginId] += values.Length;
                    
                }

            }   // end plugin lock

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

                    // if there is data in the buffer (buffer can be empty if plugin has lower sampling frequency than pipeline)
                    if (bufferPointers[plugin] != 0) {

                        // create binary version of plugin data
                        byte[] streamOut = new byte[bufferPointers[plugin] * sizeof(double)];
                        Buffer.BlockCopy(pluginDataValues[plugin], 0, streamOut, 0, bufferPointers[plugin] * sizeof(double));

                        // write to file
                        if (pluginStreams[plugin] != null)
                            pluginStreams[plugin].Write(streamOut, 0, streamOut.Length);

                        // clear buffer and reset buffer pointer
                        Array.Clear(pluginDataValues[plugin], 0, bufferSize);
                        bufferPointers[plugin] = 0;
                        //logger.Info("Writing, writing done, plugin buffer flushed" + "(" + bufferPointers[plugin] + ")");

                    }

                }

            } // end plugin lock

        }

        /**
         * Log a raw stream value to visualize
         * 
         **/
        public static void logVisualizationStreamValues(int numStreams, double[] values) {
            /*
            if (!running)   return;
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
            */
        }

        /**
         * Log raw source input data to visualize
         * 
         **/
        public static void logVisualizationEvent(int level, string text, string value) {
            if (!running)   return;
            if (!mEnableDataVisualization)  return;

            VisualizationEventArgs args = new VisualizationEventArgs();
            args.level = level;
            args.text = text;
            args.value = value;
            newVisualizationEvent(null, args);

        }

    }

}
