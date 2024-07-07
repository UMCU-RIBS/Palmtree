/**
 * The PlaybackSignal class
 * 
 * ...
 * 
 * 
 * Copyright (C) 2024:  RIBS group (Nick Ramsey Lab), University Medical Center Utrecht (The Netherlands) & external contributors
 * Author(s):           Max van den Boom            (info@maxvandenboom.nl)
 * 
 * This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software
 * Foundation, either version 3 of the License, or (at your option) any later version. This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for
 * more details. You should have received a copy of the GNU General Public License along with this program. If not, see <https://www.gnu.org/licenses/>.
 */
using NLog;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Palmtree.Core;
using Palmtree.Core.DataIO;
using Palmtree.Core.Params;

namespace Palmtree.Sources {

    /// <summary>
    /// The <c>PlaybackSignal</c> class.
    /// 
    /// ...
    /// </summary>
    public class PlaybackSignal : ISource {

        private const string CLASS_NAME                         = "PlaybackSignal";
        private const int CLASS_VERSION                         = 2;

        private const int constantThreadLoopDelayNoProc         = 200;                              // thread loop delay when not processing (1000ms / 5 run times per second = rest 200ms)
        private const int updateBufferThreadDelay               = 200;                              // the interval at which the input buffer thread updates (1000ms / 5 run times per second = rest 200ms)

        private static Logger logger                            = LogManager.GetLogger(CLASS_NAME);
        private static Parameters parameters                    = ParameterManager.GetParameters(CLASS_NAME, Parameters.ParamSetTypes.Source);
        
        // 
        private Thread signalThread                             = null;                             // the replay source thread
        private bool running                                    = true;                             // flag to define if the replay source thread should be running (setting to false will stop the source thread)
        private ManualResetEvent replayLoopManualResetEvent     = new ManualResetEvent(false);      // replay's manual reset event to call the WaitOne event on (this allows - in contrast to the sleep wait - to cancel the wait period at any point when closing the source) 
        private Stopwatch swTimePassed                          = new Stopwatch();                  // stopwatch object to give an exact amount to time passed inbetween loops
        private long highPrecisionWaitTillTime                  = 0;                                // stores the high-precision tick time until which the high precision timing waits before continueing

        private SamplePackageFormat outputFormat                = null;
        private bool configured                                 = false;
        private bool initialized                                = false;
        private bool started                                    = false;				            // flag to define if the source is started or stopped
        private Object lockStarted                              = new Object();

        // configuration variables
        private string inputFile                                = "";                               // [config] filepath to the file to replay
        private int inputFileType                               = -1;                               // type of file (src=0; dat=1) to replay
        private bool readEntireFileInMemory                     = false;                            // whether the entire file should be read into memory (on initialization)

        private double packageRate                              = 0;                                // store the amount of samples per second that the source outputs (used by the mainthread to convert seconds to number of samples)
        private bool timingByFile                               = false;                            // [config] whether the timing of the samples is based on the elapsed time in the data file
        private bool highPrecision                              = false;                            // whether the generator should have high precision intervals
        private bool redistributeEnabled                        = false;                            // [config] whether redistribution of input to output channels is enabled
        private int[] redistributeChannels                      = null;                             // [config] how the channels should be redistributed
        
        private int numInputChannels                            = 0;
        private int numOutputChannels                           = 0;
        private double outputSampleRate                         = 0;                                // output sample rate of source

        // V1 & V2 variables
        private Thread fileBufferThread                         = null;                             // thread to read the file and keep the buffers 'full'
        private ManualResetEvent fileBufferLoopManualResetEvent = new ManualResetEvent(false);      // file buffer's manual reset event to call the WaitOne event on (this allows - in contrast to the sleep wait - to cancel the wait period at any point when closing the source) 

        private double inputBufferSizeSeconds                   = 10.0;                             // [config] size of the memory buffer (seconds of data)
        private double inputBufferMinimumSeconds                = 6.0;                              // [config] mimumum fill of the memory buffer (seconds of data)
        
        private long inputBufferSize                            = 0;                                // the total size of the input buffer in number of packages (V2+) or rows (V1) 
        private long inputBufferMinTillRead                     = -1;                               // the minimum amount of rows until additional read
        private long inputBufferReadSize                        = 0;                                // the number of rows/packages to read per update

        private Object lockInputReader                          = new Object();                     // threadsafety lock for input reader
        private DataReader inputReader                          = null;
        private DataHeader inputHeader                          = null;

        private Object lockInputBuffer                          = new Object();                     // thread lock for input buffer
        private long inputBufferAddIndex                        = 0;                                // the index where in the (ring) input buffer the next row/package will be added
        private long inputBufferReadIndex                       = 0;                                // the index where in the (ring) input buffer the next row/package is that should be read

        private int constantIntervalMs                          = 0;                                // the interval (in ms) to wait before pushing the next package (i.e. to pauze the replay loop;if not taking timing into account)
        private long constantIntervalTicks                      = 0;                                // the interval (in ticks for high precision timing) to wait before pushing the next package (i.e. to pauze the replay loop;if not taking timing into account)

        private double[] nextSamplePackage                      = null;                             // next package (V2+) or row (V1; packed in a single package) ready to send into the pipeline

        // V1 variables
        private byte[] inputRowBuffer                           = null;                             // input (byte) ringbuffer
        private int inputBufferRowSize                          = 0;                                // The size of a single row (in bytes) of values. This value is a copy from the input file header, so we do not have to (thread)lock the reader/header objects when we just want to read the data
        private long numberOfRowsInBuffer                       = 0;                                // the number of rows in the (ring) input buffer

        // V2+ variables
        private double[][] inputPackageBuffer                   = null;                             // buffer to hold sample-packages read from the input file to send into the pipeline (sort of a ringbuffer)
        private double[] inputPackageBufferElapsedStamps        = null;                             // buffer to hold elapsed time (from the start of the run) for the sample-packages in inputPackageBuffer (sort of a ringbuffer)
        private long numberOfPackagesInBuffer                   = 0;                                // the number of sample-packages in the (ring-)buffer

        private int nextThreadLoopDelayNoProc                   = -1;                               // variable that can set the next not-processing delay to a specific value (is used to stick the main thread in a long wait
        private int nextSamplePackageElapsedMs                  = -1;                               // the elapsed time (in ms) at which to push the next package
        


        public PlaybackSignal() {
            
            parameters.addParameter<ParamFileString>(
                "Input",
                "The data input file(s) that should be used for playback.",
                "", "", "");

            parameters.addParameter<int[]>(
                "RedistributeChannels",
                "Using this parameter the channels from the input file can be selected and re-ordered.\n\n" +
                "Each value indicates the index (1-based) of an input channel from the data file that will becomes a replayed output channel.\n" +
                "The position of the value determines which output-channel it will become, as such the number of values here determines the number of output channels.\n" +
                "For example: if an input file has two input channels, entering '1 2' in this field would playback as recorded. However, entering '2 1' would switch the input channels for output.\n\n" +
                "Note: if left empty, all channels will be played back exactly as ordered in the input file",
                "", "", "");


            //
            //
            //
            parameters.addHeader("File and buffer I/O");

            parameters.addParameter<bool>(
                "ReadEntireFileInMemory",
                "Read the entire data file into memory at initialization.\n\n" +
                "Note: Dpending on the data file size, this could cause high memory usage.",
                "", "", "0");

            parameters.addParameter<double>(
                "InputBufferSize",
                "The size of the memory buffer (in number of seconds of data) to which the data from the input file is read and stored before being forwarded into the pipeline.\n" +
                "This buffer will initiatially be filled with data and - as data moves out of the buffer and into the pipeline during playback - will be refilled when it falls below a certain minimum.\n" +
                "In practice, the buffer is allocated to hold a number of sample-packages, which is calculated based on the number of seconds in this parameter and the input file (meta)data.\n\n" +
                "Note: When the entire file is read into memory, then this parameter is unused",
                "", "", "10s");

            parameters.addParameter<double>(
                "InputBufferMinimum",
                "The minimum amount of data (in seconds) that should be held in the memory buffer while more data is available in the input file.\n" +
                "Once the buffer - as data moves out of the buffer and into the pipeline during playback - reaches this minumum, new data will be read from the input file into the input buffer.\n\n" +
                "Note: When the entire file is read into memory, then this parameter is unused",
                "", "", "6s");


            //
            //
            //
            parameters.addHeader("Timing");

            parameters.addParameter<bool>(
                "TimingByFile",
                "The sample-packages stored in data format V2 or higher can be passed to the pipeline at (approximately) the same timing as they were recorded.\n" +
                "If disabled, the first data-package will be passed to the pipeline immediately after the start, while the subsequent packages will be passed at the average sample-package interval.",
                "", "", "1");

            parameters.addParameter<bool>(
                "HighPrecision",
                "Use high precision intervals when replaying the sample data.\n\n" +
                "Note 1: Enabling this option will claim one processor core, which possibly has an impact on the overall performance of your system.\n" +
                "Note 2: High precision will be enabled automatically when the sample rate (V1) or sample-package rate (V2+) is higher than 1000 hz.",
                "", "", "0");



            // message
            logger.Info("Source created (version " + CLASS_VERSION + ")");

            // start a new thread
            signalThread = new Thread(this.run);
            signalThread.Name = "Replay source main thread";
            signalThread.Priority = ThreadPriority.Highest;
            signalThread.Start();

        }

        public int getClassVersion() {
            return CLASS_VERSION;
        }

        public string getClassName() {
            return CLASS_NAME;
        }

        public Parameters getParameters() {
            return parameters;
        }

        public double getInputSamplesPerSecond() {
            return 0;
        }

        /**
         * function to retrieve the number of samples per second
         * 
         * Note: This value could be requested by the main thread and can be
         *       used to convert parameters from seconds to samples
         **/
        public double getOutputSamplesPerSecond() {

            // check if the source is not configured yet
            if (!configured) {

                // error message and return 0 samples
                logger.Error("Trying to retrieve the samples per second before the source was configured, first configure the source, returning 0");
                return 0;

            }

            // return the samples per second
            return outputSampleRate;

        }

        public bool configure(out SamplePackageFormat output) {
            #pragma warning disable 0162            // for constant checks, conscious ignore
            output = null;

            // retrieve the input file and determine the extension
            inputFile = parameters.getValue<string>("Input");
            int extIndex = inputFile.LastIndexOf('.');
            string ext = "";
            if (extIndex != -1)     ext = inputFile.Substring(extIndex);

            // check if the file exists
            if (string.IsNullOrEmpty(inputFile) || !File.Exists(inputFile)) {
                logger.Error("Could not find input file '" + inputFile + "'");
                return false;
            }


            // retrieve whether the file should be read into memory
            readEntireFileInMemory = parameters.getValue<bool>("ReadEntireFileInMemory");

            // retrieve the buffer sizes
            inputBufferSizeSeconds = parameters.getValue<double>("InputBufferSize");
            inputBufferMinimumSeconds = parameters.getValue<double>("InputBufferMinimum");
            if (!readEntireFileInMemory) {

                if (inputBufferSizeSeconds < 2) {
                    logger.Error("The buffer size cannot be less than 2 seconds, provide a larger value for the 'InputBufferSize' parameter");
                    return false;
                }
                if (inputBufferMinimumSeconds < 1) {
                    logger.Error("The buffer minimum cannot be less than 1 seconds, provide a larger value for the 'InputBufferMinimum' parameter");
                    return false;

                }
                if (inputBufferMinimumSeconds > inputBufferSizeSeconds) {
                    logger.Error("The buffer minimum is larger than the buffer, either adjust the 'InputBufferSize' parameter or the 'InputBufferMinimum' parameter");
                    return false;
                }

            }

            // retrieve the timing by file setting
            timingByFile = parameters.getValue<bool>("TimingByFile");

            // retrieve the high precision setting
            highPrecision = parameters.getValue<bool>("HighPrecision");

            // thread safety
            lock (lockInputReader) {
                lock (lockInputBuffer) {

                    // check if there is still an inputreader open, if so close
                    if (inputReader != null) {
                        inputReader.close();
                        inputReader = null;
                        inputHeader = null;
                        inputBufferRowSize = 0;
                    }

                    // read the data header
                    inputHeader = DataReader.readHeader(inputFile);
                    if (inputHeader == null) {
                        logger.Error("Could not read header data from input file '" + inputFile + "'");
                        return false;
                    }
                    
                    // check if the internal code is 'src' or 'dat'
                    if (string.Compare(inputHeader.code, "src") != 0 && string.Compare(inputHeader.code, "dat") != 0) {
                        logger.Error("The input file is internally marked as '" + inputHeader.code + "', while either a source ('src') file or a data stream ('dat') file is required");
                        return false;
                    }

                    // determine the file type
                    inputFileType = inputHeader.code.Equals("src", StringComparison.OrdinalIgnoreCase) ? 0 : 1;

                    // check version 1 (exclude .src file)
                    if (inputHeader.version == 1 && inputFileType == 0) {
                        logger.Error("Source (.src) files stored in the V1 format cannot be replayed");
                        return false;
                    }
                     
                    // check if the number of replay input streams is higher than 0
                    if (inputFileType == 0 && inputHeader.numStreams <= 0) {
                        logger.Error("The input .src file has no source streams, these are required for playback, make sure the LogSourceInput setting (data tab) is switched on while recording data for replay");
                        return false;
                    }
                    if (inputFileType == 1 && inputHeader.numPlaybackStreams <= 0) {
                        logger.Error("The input .dat file has no playback streams, these are required for playback, make sure the LogPipelineInputStream setting (data tab) is switched on while recording data for replay");
                        return false;
                    }

                    // determine the number of input streams
                    if (inputFileType == 0)         numInputChannels = inputHeader.numStreams;
                    else if (inputFileType == 1)    numInputChannels = inputHeader.numPlaybackStreams;
                    
                    // retrieve the redistribution of channels
                    redistributeChannels = parameters.getValue<int[]>("RedistributeChannels");
                    redistributeEnabled = redistributeChannels.Length > 0;
                    for (int i = 0; i < redistributeChannels.Length; i++) {

                        if (redistributeChannels[i] < 1 || redistributeChannels[i] % 1 != 0) {
                            logger.Error("The values in the RedistributeChannels parameter should be positive integers (note that the channel numbering is 1-based)");
                            return false;
                        }

                        if (inputFileType == 0 && redistributeChannels[i] > numInputChannels) {
                            logger.Error("One of the values in the RedistributeChannels parameter exceeds the number of (input) streams in the .src input file (#numStreams: " + numInputChannels + ")");
                            return false;
                        }
                        if (inputFileType == 1 && redistributeChannels[i] > numInputChannels) {
                            logger.Error("One of the values in the RedistributeChannels parameter exceeds the number of (input) streams in the .dat input file (#playbackStreams: " + numInputChannels + ")");
                            return false;
                        }

                        // lower each channel value (1-based), so it can be used immediately to point to the right value in the input stream array (0-based)
                        redistributeChannels[i]--;

                    }


                    // check if the channels are redistributed
                    if (redistributeEnabled) {

                        // set the number of output channels to the number of redistributed channels
                        numOutputChannels = redistributeChannels.Length;

                    } else {

                        // set the number of output channels for this source based on the streams in the file
                        numOutputChannels = numInputChannels;

                    }

                    //
                    // calculate and prepare intervals, rates, buffers etc
                    //

                    if (inputHeader.version == 2 || inputHeader.version == 3) {

                        // calculate the sample-package interval
                        constantIntervalMs = (int)Math.Floor(inputHeader.averagePackageInterval);
                        
                        // check average package interval
                        if (constantIntervalMs <= 0) {
                            logger.Error("The rounded average package interval in the input file <= 0.\nA valid average interval is required to estimate output package- and sample-rates.");
                            return false;
                        }

                        // calculate and check the package rate                       
                        packageRate = (double)1000 / constantIntervalMs;
                        if (packageRate <= 0) {
                            logger.Error("The calculated package-rate (" + packageRate + "Hz) based on the average package interval <= 0.\nA valid package-rate is required to estimate output package- and sample-rates.");
                            return false;
                        }

                        // create an output format
                        int numSamplesPerPackage = (int)(inputHeader.sampleRate / packageRate);
                        output = new SamplePackageFormat(numOutputChannels, numSamplesPerPackage, packageRate, SamplePackageFormat.ValueOrder.SampleMajor);

                        // calculate the input buffer size in packages
                        if (!readEntireFileInMemory) {
                            inputBufferSize = (long)Math.Floor(inputBufferSizeSeconds * packageRate);
                            if (inputBufferSize == 0) {
                                logger.Error("The buffer size of " + inputBufferSizeSeconds + "s at a package-rate of " + packageRate + "Hz is too small (" + inputBufferSize + " packages) when combined with the sample rate, provide a larger value for INPUT_BUFFER_SIZE_SECONDS");
                                return false;
                            }
                        }

                        // the entire file should be read into memory, or if the number of packages in the file is equal to or smaller than
                        // the input buffer packages, then the buffer size will be based on the file package-size
                        if (readEntireFileInMemory || inputHeader.totalPackages <= inputBufferSize) {

                            // input buffer will be the same size as the data in the input file
                            inputBufferSize = inputHeader.totalPackages;

                            // not necessary as the entire file will be read in on initialize
                            inputBufferMinTillRead = -1;
                            inputBufferReadSize = -1;

                        } else { 

                            // calculate the minimum amount of packages until additional read
                            inputBufferMinTillRead = (long)Math.Floor(inputBufferMinimumSeconds * packageRate);
                            if (inputBufferMinTillRead == 0) {
                                logger.Error("The buffer minimum (" + inputBufferMinimumSeconds + "s at a package-rate of " + packageRate + " = " + inputBufferMinTillRead + " packages) is too small when combined with the package-rate, provide a larger value for the 'InputBufferMinimum' parameter");
                                return false;
                            }

                            // calculate the number of packages to read when the minimum is reached
                            // (note: a smaller read step is also possible; current just refilling the buffer by taking the difference between the minimum and total buffer size)
                            inputBufferReadSize = inputBufferSize - inputBufferMinTillRead;

                            // check if the number of rows per read is not too big
                            // note: should not happen since it is calculated based on the buffer size and minimum, just in case)
                            if (inputBufferReadSize > inputBufferSize - inputBufferMinTillRead) {
                                logger.Error("Number of packages per read (" + inputBufferReadSize + ") should be smaller than the space in the buffer that is open when the buffer minimum is reached ('" + (inputBufferSize - inputBufferMinTillRead) + ")");
                                return false;
                            }

                        }

                    } else if (inputHeader.version == 1) {
                        // data version 1

                        // set the sample rate for this source based on the .dat file
                        packageRate = inputHeader.sampleRate;

                        // check the sample rate
                        if (packageRate <= 0) {
                            logger.Error("The sample rate in the (header of the) .dat file is 0 or lower, invalid sample rate");
                            return false;
                        }

                        // create a sampleformat
                        // Note: at this point we only playback .dat files with the pipeline input streams, in data format 1 these always have 1 single sample per package.
                        //       since the number of samples is 1 per package, the given samplerate is the packagerate)
                        output = new SamplePackageFormat(numOutputChannels, 1, packageRate, SamplePackageFormat.ValueOrder.SampleMajor);

                        // calculate the input buffer size in rows
                        if (!readEntireFileInMemory) {
                            inputBufferSize = (long)Math.Floor(inputBufferSizeSeconds * inputHeader.sampleRate);
                            if (inputBufferSize == 0) {
                                logger.Error("The buffer size " + inputBufferSizeSeconds + "s at a sample rate of " + inputHeader.sampleRate + " is too small (" + inputBufferSize + " samples), provide a larger value for INPUT_BUFFER_SIZE_SECONDS");
                                return false;
                            }
                        }

                        // the entire file should be read into memory, or if the number of rows in the file are equal to or smaller than
                        // the input buffer rows, then the buffer size will be based on the file row-size
                        if (readEntireFileInMemory || inputHeader.numRows <= inputBufferSize) {

                            // input buffer will be the same size as the data in the input file
                            inputBufferSize = inputHeader.numRows;

                            // not necessary as the entire file will be read in on initialize
                            inputBufferMinTillRead = -1;
                            inputBufferReadSize = -1;

                        } else {

                            // calculate the minimum amount of rows until additional read
                            inputBufferMinTillRead = (long)Math.Floor(inputBufferMinimumSeconds * inputHeader.sampleRate);
                            if (inputBufferMinTillRead == 0) {
                                logger.Error("The buffer minimum is too small when combined with the sample-rate, provide a larger value for the 'InputBufferMinimum' parameter");
                                return false;
                            }

                            // calculate the number of rows to read when the minimum is reached
                            // (note: a smaller read step is also possible; current just refilling the buffer by taking the difference between the minimum-till-read and total buffer size)
                            inputBufferReadSize = inputBufferSize - inputBufferMinTillRead;

                            // check if the number of rows per read is not too big
                            // note: should not happen since it is calculated based on the buffer size and min-till-read, just in case)
                            if (inputBufferReadSize > inputBufferSize - inputBufferMinTillRead) {
                                logger.Error("Number of rows per read (" + inputBufferReadSize + ") should be smaller than the space in the buffer that is open when the buffer min-till-read is reached ('" + (inputBufferSize - inputBufferMinTillRead) + ")");
                                return false;
                            }

                        }
                        
                        // calculate the sample interval
                        constantIntervalMs = (int)Math.Floor(1000.0 / packageRate);
    
                    }   // end V1 config


                    // set (theoretical) output rate (hz) to what is in the header of the input file
                    // Note: the sample-rate might not be the same as the package-rate
                    outputSampleRate = inputHeader.sampleRate;

                } // end lock
            } // end lock


            // check if the packageRate is above 1000hz
            if (packageRate > 1000) {

                // enable the high precision timing
                highPrecision = true;

                // message
                logger.Warn("Because the sample/package rate is larger than 1000hz, the high precision timer is used");

            }

            // check if high precision timing is enabled
            if (highPrecision) {

                // calculate the sample interval for the high precision timing
                constantIntervalTicks = (long)Math.Round(Stopwatch.Frequency * (1.0 / packageRate));

                // message
                logger.Warn("High precision timer enabled, one core will be claimed entirely (which might have consequences for your overal system performance)");

            }

            // store a reference to the output format
            outputFormat = output;

            // print configuration
            printLocalConfiguration();

            // flag as configured
            configured = true;

            // return success
            return true;

        }

        private void printLocalConfiguration() {

            // debug output
            logger.Debug("--- Source configuration: " + CLASS_NAME + " ---");

            logger.Debug("Input file: " + inputFile);
            logger.Debug("Input file version: " + inputHeader.version);
            logger.Debug("Input file code: " + inputHeader.code);
            logger.Debug("Input file sample-rate: " + inputHeader.sampleRate + "Hz");
            if (inputHeader.version == 1) {
                logger.Debug("Number of rows: " + inputHeader.numRows);
            } else {
                logger.Debug("Number of packages: " + inputHeader.totalPackages);
            }

            if (inputFileType == 0)     logger.Debug("Number of input channel streams in file: " + inputHeader.numStreams);
            else                        logger.Debug("Number of input playback streams in file: " + inputHeader.numPlaybackStreams);

            logger.Debug("Channel redistribution: " + (redistributeEnabled ? "Yes" : "No"));
            if (redistributeEnabled) { 
                logger.Debug("Redistribution (channels are 1-based):");
                for (int i = 0; i < redistributeChannels.Length; i++)
                    logger.Debug("    In-channel " + (redistributeChannels[i] + 1) + " -> Out-channel " + (i + 1));
            }

            logger.Debug("Output channels: " + outputFormat.numChannels);
            logger.Debug("Output estimated package-rate: " + outputFormat.packageRate + "Hz");
            logger.Debug("Output estimated #samples-per-package: " + outputFormat.numSamples);
            logger.Debug("Output sample-rate: " + outputSampleRate + "Hz");

            logger.Debug("Read entire input file to memory: " + (readEntireFileInMemory ? "Yes" : "No"));
            if (!readEntireFileInMemory) {
                if (inputHeader.version == 1) {
                    logger.Debug("Input buffer size: " + inputBufferSize + " rows (" + inputBufferSizeSeconds + "s)");
                    logger.Debug("Input buffer minimum: " + inputBufferMinTillRead + " rows (" + inputBufferMinimumSeconds + "s)");
                    logger.Debug("Number of rows to read when minimum is reached: " + inputBufferReadSize);
                    

                } else {
                    logger.Debug("Input buffer size: " + inputBufferSize + " sample-packages (" + inputBufferSizeSeconds + "s)");
                    logger.Debug("Input buffer minimum: " + inputBufferMinTillRead + " sample-packages (" + inputBufferMinimumSeconds + "s)");
                    logger.Debug("Number of sample-packages to read when minimum is reached: " + inputBufferReadSize);
                }
                
            }
            logger.Debug("Package timing by file: " + (timingByFile ? "Yes" : "No"));
            logger.Debug("High-precision timing: " + (highPrecision ? "On" : "Off"));
            

        }

        public bool initialize() {

            lock (lockInputReader) {

                // check if there is still an inputreader open, if so close
                // (is also done in configure, but just in case these do not follow each other)
                if (inputReader != null) {
                    inputReader.close();
                    inputReader = null;
                    inputHeader = null;
                    inputBufferRowSize = 0;
                }

                // open the input reader
                inputReader = new DataReader(inputFile);
                inputReader.open();

                // (re-)retrieve the header (already read on open)
                inputHeader = inputReader.getHeader();

                // copy the row/package size to a local variable
                // Note: a copy because this way we do not have to (thread)lock the reader/header objects when we just want to read the data
                if (inputHeader.version == 1) {
                    inputBufferRowSize = inputHeader.rowSize;
                }

            }   // end lock

            // initialize the input buffer and already fill it with data
            fillInputBuffer();

            // flag as initialized and return success
            initialized = true;
            return true;

        }

        /**
	     * Start
	     */
        public void start() {

            // check if configured and the source was initialized
            if (!configured || !initialized) {
                return;
            }

            // check if the playback was not already started
            lock (lockStarted) {
                if (started) return;
            }

            // completely (re-)fill the input buffer
            // Note: For V2+ data files, either initialize or stop() will have already have called 'fillInputBuffer' (to shift the read delay away from start)
            //       For V1+ data files, on the first run, only the initialization will already have called 'fillInputBuffer'. However, after stopping a run, the
            //                           buffer will be cleared. If another run is started the buffer needs to be re-filled before starting
            if  (inputHeader.version == 1 && inputRowBuffer == null)
                fillInputBuffer();

            //
            Debug.Assert(nextThreadLoopDelayNoProc != constantThreadLoopDelayNoProc, "nextThreadLoopDelayNoProc cannot be the same as constantThreadLoopDelayNoProc");
            Debug.Assert(nextThreadLoopDelayNoProc != 10000, "nextThreadLoopDelayNoProc cannot be 10000, this amount is reserved for a long delay to time the onset");

            // set a relatively long delay for the main thread to fall into, this wait allows
            // us later to trigger (i.e. interrupt the thread) on an exact moment
            nextThreadLoopDelayNoProc = 10000;

            // interrupt any wait to ensure we fall into a long wait
            if (highPrecision)  highPrecisionWaitTillTime = 0;
            replayLoopManualResetEvent.Set();

            // wait until the main thread has reset the delay, meaning it is in the long delay
            Thread.Sleep(5);
            while (nextThreadLoopDelayNoProc != -1) {
                Thread.Sleep(1);
            }
                
            // 
            if (inputHeader.version == 2 || inputHeader.version == 3) {

                // take the first package from the input buffer
                double dblNextSamplePackageElapsedMs = -1;
                getNextInputPackage(ref dblNextSamplePackageElapsedMs, ref nextSamplePackage);

                // warning if start is late
                if (Data.getDataRunElapsedTime() >= dblNextSamplePackageElapsedMs)
                    logger.Warn("The playback start-time has already passed the first-package elapsed time, the onset of the replay will be off");
                
                // if timing by file, set the elapsedMs (causing the loop not to use a constant value)
                if (timingByFile)
                    nextSamplePackageElapsedMs = (int)dblNextSamplePackageElapsedMs;

            } else if (inputHeader.version == 1) {

                // take the first row from the input buffer
                nextSamplePackage = getNextInputRow_v1();
                
            }
        
            // flag as started playback
            started = true;

            // interrupt the no-proc loop wait
            //   In V1 data files, causing an immediate start and switching to the processing waittime
            //   In V2+ data files, causing an interrupt in the long wait
            replayLoopManualResetEvent.Set();

            // check if we need to update the buffer (i.e. if the file is not entirely read into memory)
            if (!readEntireFileInMemory) {

                // start a new thread to keep the input buffer updated
                fileBufferThread = new Thread(this.runUpdateInputBuffer);
                fileBufferThread.Name = "Replay source file buffer thread";
                fileBufferThread.Start();

            }

        }


        /**
	     * Stop
	     */
        public void stop() {

            // if not initialized than nothing needs to be stopped
            if (!initialized)   return;
            
            lock (lockStarted) {

                // clear any specific timing delays
                nextThreadLoopDelayNoProc = -1;
                nextSamplePackageElapsedMs = -1;

                // stop playback
                started = false;
            }

            // interrupt the high precision loop if one is running
            if (highPrecision)
                highPrecisionWaitTillTime = 0;

            // interrupt the playback loop wait, allowing the loop to continue (in case it was waiting the threadLoopDelay interval)
            // switching to the no-processing waittime
            replayLoopManualResetEvent.Set();

            // check if there is a update input buffer thread
            if (fileBufferThread != null) {

                // interrupt the loop wait, allowing the loop to continue and exit
                fileBufferLoopManualResetEvent.Set();

                // wait until the buffer input thread stopped
                int waitCounter = 5000;
                while (fileBufferThread.IsAlive && waitCounter > 0) {
                    Thread.Sleep(1);
                    waitCounter--;
                }

                // clear the input buffer thread reference
                fileBufferThread = null;

            }

            lock (lockInputBuffer) {

                if (inputHeader.version == 2 || inputHeader.version == 3) {
                    
                    // clear the input buffer and input buffer variables
                    inputPackageBuffer = null;
                    inputPackageBufferElapsedStamps = null;
                    numberOfPackagesInBuffer = 0;
                    inputBufferReadIndex = 0;
                    inputBufferAddIndex = 0;

                } else if (inputHeader.version == 1) {

                    // clear the input buffer and input buffer variables
                    inputRowBuffer = null;
                    numberOfRowsInBuffer = 0;
                    inputBufferReadIndex = 0;
                    inputBufferAddIndex = 0;

                }

            }   // end lock


            // On V2+ data files, completely (re-)fill the input buffer after stopping
            // Note: When stopping the buffer will be reset. If we would refill the buffer on start(), there is a delay in the start.
            //       Since we want (in V2+ data files) the first-package to be pushed on the elapsed time, we need to minimize any delay
            //       during the start; which is why we fill the buffer at stop already
            if  ((inputHeader.version == 2 || inputHeader.version == 3) && inputPackageBuffer == null)
                fillInputBuffer();

        }

	    /**
	     * Returns whether the signalgenerator is generating signal
	     * 
	     * @return Whether the signal generator is started
	     */
	    public bool isStarted() {
            lock(lockStarted) {
                return started;
            }
	    }


	    /**
	     * 
	     */
	    public void destroy() {
        
            // stop source
            // Note: At this point stop will probably have been called from the mainthread before destroy, however there is a slight
            // chance that in the future someone accidentally will put something in the configure/initialize that should have
            // actually been put in the start. If start is not called in the mainthread, then stop will also not be called at the
            // modules. For these accidents we do an extra stop here.
		    stop();

            // flag the thread to stop running (when it reaches the end of the loop)
            running = false;

            // interrupt the wait in the loop
            // (because if the sample rate is low, we might have to wait for a long time for the thread to end)
            replayLoopManualResetEvent.Set();

            // wait until the thread stopped
            int waitCounter = 500;
            while (signalThread.IsAlive && waitCounter > 0) {
                Thread.Sleep(10);
                waitCounter--;
            }

            // clear the thread reference
            signalThread = null;
            
	    }
	
	    /**
	     * Returns whether the source thread is still running
	     * Note: 'running' just determines whether the source thread is running; start(), stop() and 
         * isStarted() manage whether samples are played-back into Palmtree
	     * 
	     * @return Whether the source thread is running
	     */
	    public bool isRunning() {
		    return running;
	    }


        /**
	     * Source read data file thread
	     */
        private void runUpdateInputBuffer() {
            
            // check if we are running and playback has been started
            while (running && started) {

                // check the input buffer and (if needed) read
                updateInputBuffer();

                // reset the manual reset event, so it is sure to block on the next call to WaitOne
                // 
                // Note: not using AutoResetEvent because it could happen that .Set is called while not in WaitOne yet, when
                // using AutoResetEvent this will cause it to skip the next WaitOne call
                fileBufferLoopManualResetEvent.Reset();

                // wait (using WaitOne, making it interruptable)
                fileBufferLoopManualResetEvent.WaitOne(updateBufferThreadDelay);

            }

        }


        /**
	     * Source playback thread
	     */
        private void run() {

            // log message
            logger.Debug("Thread started");

            // should prevent "normal" processes from interrupting
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.RealTime;  	

            // set an initial start for the stopwatch
            swTimePassed.Start();

		    // loop while running
		    while(running) {

                // lock start
                lock(lockStarted) {

			        // check if we are playing-back packages
			        if (started) {
                        
                        // check if there is a next sample/package
                        if (nextSamplePackage == null) {
                            // no next sample

                            // message
                            logger.Info("Playback of the file is finished (or read buffer was found empty), calling stop");

                            // send a stop signal to the mainThread
                            MainThread.stop(false);


                        } else {
                            // there is a next sample/package (previously retrieved)

                            if (inputHeader.version == 2 || inputHeader.version == 3) {

                                if (redistributeEnabled) {
                                    // redistribute

                                    int outCounter = 0;

                                    // determine the number of samples in the package
                                    int numSamples = nextSamplePackage.Length / numInputChannels;

                                    // create sample-package to return
                                    double[] reSamplePackage = new double[numOutputChannels * numSamples];

                                    // loop over each sample
                                    for (int iSample = 0; iSample < nextSamplePackage.Length; iSample += numInputChannels) {

                                        // loop through the redistrubution channels
                                        for (int iChan = 0; iChan < numOutputChannels; iChan++) {

                                            // 
                                            reSamplePackage[outCounter] = nextSamplePackage[iSample + redistributeChannels[iChan]];
                                            outCounter++;
                                        }

                                    }

                                    // pass the sample-package with redistributed channels
                                    MainThread.eventNewSample(reSamplePackage);
                            
                                } else {

                                    // pass the sample
                                    MainThread.eventNewSample(nextSamplePackage);

                                }

                            } else if (inputHeader.version == 1) {

                                // create sample-package to return
                                double[] rowSamplePackage = new double[numOutputChannels];

                                if (redistributeEnabled) {
                                    // redistribute

                                    // pick and copy
                                    for (int i = 0; i < numOutputChannels; i++) {
                                        rowSamplePackage[i] = nextSamplePackage[redistributeChannels[i] + 1];        // '+ 1' = skip the elapsed time column
                                    }

                                } else {
                                    // set values for the generated sample

                                    // copy values
                                    for (int i = 0; i < numOutputChannels; i++) {
                                        rowSamplePackage[i] = nextSamplePackage[i + 1];                              // '+ 1' = skip the elapsed time column
                                    }

                                }

                                // pass the sample-package
                                MainThread.eventNewSample(rowSamplePackage);

                            }


                        }

                        // (try to) retrieve the next sample
                        if (inputHeader.version == 2 || inputHeader.version == 3) {

                            double dblNextSamplePackageElapsedMs = -1;
                            getNextInputPackage(ref dblNextSamplePackageElapsedMs, ref nextSamplePackage);

                            // if timing by file, set the elapsedMs (causing the loop not to use a constant value)
                            if (timingByFile)
                                nextSamplePackageElapsedMs = (int)dblNextSamplePackageElapsedMs;

                        } else if (inputHeader.version == 1)
                            nextSamplePackage = getNextInputRow_v1();

                    }   // end conditional started
                }   // end lock started

                
			    // if still running then wait to allow other processes
			    if (running) {
                    
                    // check if we are generating
                    // Note: we deliberately do not lock the started variable here, locking will delay/lock out 'start()' during the wait here, and
                    //       if these are out-of-sync, the worst thing that can happen is that it does waits one loop extra, which is no problem)
                    if (started) {
                        
                        if (nextSamplePackageElapsedMs != -1) {
                            loopWaitOneTillDataElapsed(ref nextSamplePackageElapsedMs);

                        } else {

                            if (highPrecision)
                                loopWaitOneHP(ref constantIntervalTicks, ref nextSamplePackageElapsedMs);
                            else
                                loopWaitOne(constantIntervalMs, ref nextSamplePackageElapsedMs); // nextSamplePackageElapsedMs is not used here

                        }
                        
                    } else {

                        // wait
                        loopWaitOne(constantThreadLoopDelayNoProc, ref nextThreadLoopDelayNoProc);

                        // a specific delay interval is available
                        if (nextSamplePackageElapsedMs != -1)
                            loopWaitOneTillDataElapsed(ref nextSamplePackageElapsedMs);

                    }

                    // restart the timer to measure the loop time
                    swTimePassed.Restart();

                }   // end check if-running

            }   // end run loop (while running)
            
            // log message
            logger.Debug("Thread stopped");

        }

        private void loopWaitOne(int constIntervalMs, ref int nextIntervalMs) {
            int threadLoopDelay;

            if (nextIntervalMs > -1) {
                // a specific delay interval is available
                
                threadLoopDelay = nextIntervalMs;

                // reset the specific delay
                nextIntervalMs = -1;

            } else {
                // constant delay

                //threadLoopDelay = constIntervalMs;     // choose not to correct for elapsed ms. This result in a slightly higher wait time, which at lower Hz comes closer to the expected samplecount per second
                threadLoopDelay = constIntervalMs - (int)swTimePassed.ElapsedMilliseconds;

            }
            
            // wait for the remainder of the sample interval to get as close to the sample rate as possible (if there is a remainder)
            if (threadLoopDelay >= 0) {

                // reset the manual reset event, so it is sure to block on the next call to WaitOne
                // 
                // Note: not using AutoResetEvent because it could happen that .Set is called while not in WaitOne yet, when
                //       using AutoResetEvent this will cause it to skip the next WaitOne call
                replayLoopManualResetEvent.Reset();
                                
                // wait (using WaitOne, making it interruptable)
                replayLoopManualResetEvent.WaitOne(threadLoopDelay);
                                
            }

        }

        private void loopWaitOneHP(ref long constIntervalTicks, ref int nextIntervalMs) {
            
            if (nextIntervalMs > -1) {
                // a specific delay interval (in ms!) is available

                // convert from ms to ticks and set an end time (in ticks)
                highPrecisionWaitTillTime = Stopwatch.GetTimestamp() + ((long)(nextIntervalMs * (Stopwatch.Frequency / 1000.0)));
                
                // reset the specific delay
                nextIntervalMs = -1;

            } else {
                // constant delay

                // determine the tick delay
                highPrecisionWaitTillTime = Stopwatch.GetTimestamp() + constIntervalTicks;     // choose not to correct for elapsed ticks. This result in a slightly higher wait time, which at lower Hz comes closer to the expected samplecount per second
                //highPrecisionWaitTillTime = Stopwatch.GetTimestamp() + constIntervalTicks - swTimePassed.ElapsedTicks;

            }

            // wait (spin until)
            while (Stopwatch.GetTimestamp() <= highPrecisionWaitTillTime) ;

        }

        private void loopWaitOneTillDataElapsed(ref int nextDataElapsedTimeMs) {
            
            int threadLoopDelay = nextDataElapsedTimeMs - (int)Data.getDataRunElapsedTime();

            // reset the specific delay
            nextDataElapsedTimeMs = -1;

            // check if there is a need to wait
            if (threadLoopDelay >= 0) {

                if (highPrecision) {

                    // convert from ms to ticks and set an end time (in ticks)
                    highPrecisionWaitTillTime = Stopwatch.GetTimestamp() + ((long)(threadLoopDelay * (Stopwatch.Frequency / 1000.0)));
                
                    // reset the specific delay
                    nextDataElapsedTimeMs = -1;

                    // wait (spin until)
                    while (Stopwatch.GetTimestamp() <= highPrecisionWaitTillTime) ;

                } else {
                    // low precision

                    // reset the manual reset event, so it is sure to block on the next call to WaitOne
                    // 
                    // Note: not using AutoResetEvent because it could happen that .Set is called while not in WaitOne yet, when
                    //       using AutoResetEvent this will cause it to skip the next WaitOne call
                    replayLoopManualResetEvent.Reset();
                                
                    // wait (using WaitOne, making it interruptable)
                    replayLoopManualResetEvent.WaitOne(threadLoopDelay);
                                
                }
            }

        }


        /// <summary>
        /// Fill the input buffer with as many packages/rows as possible for replay
        /// Note: the number of packages/rows is only limited by the size of buffer or number available in the input file
        /// </summary>
        private void fillInputBuffer() {

            lock (lockInputReader) {
                lock (lockInputBuffer) {

                    // set the data pointer of the input reader to the start
                    inputReader.resetDataPointer();

                    // 
                    if (inputHeader.version == 2 || inputHeader.version == 3) {
                        
                        // read from the input file and fill the package input-buffer
                        long packagesRead = inputReader.readNextPackages(inputBufferSize, out inputPackageBuffer, out inputPackageBufferElapsedStamps);

                        // set the input ringbuffer variables
                        inputBufferAddIndex = 0;
                        //inputBufferAddIndex = packagesRead;
                        //if (inputBufferAddIndex >= inputPackageBuffer.Length)   inputBufferAddIndex -= inputPackageBuffer.Length;
                        numberOfPackagesInBuffer = packagesRead;
                        
                    } else if (inputHeader.version == 1) {

                        // read from the input file and fill the row input-buffer (full)
                        long rowsRead = inputReader.readNextRows_V1(inputBufferSize, out inputRowBuffer);

                        // set the input ringbuffer variables
                        inputBufferAddIndex = rowsRead * inputHeader.rowSize;
                        if (inputBufferAddIndex >= inputRowBuffer.Length) inputBufferAddIndex -= inputRowBuffer.Length;
                        numberOfRowsInBuffer = rowsRead;

                    }

                    // set the input buffer read index at the start (since the buffer was filled from 0)
                    inputBufferReadIndex = 0;

                }   // end of lock
            }   // end of lock
        }

        
        /// <summary>
        /// Retrieve the next package from the package input-buffer (used for data files V2 or higher)
        /// </summary>
        /// <returns>The next sample-package from the buffer, or null no next package is available</returns>
        private void getNextInputPackage(ref double elapsed, ref double[] data) {
            elapsed = -1;
            data = null;

            lock (lockInputBuffer) {

                // if there are no packages in the buffer, return null
                if (numberOfPackagesInBuffer == 0)      return;

                // pass the package reference
                double[] retData = inputPackageBuffer[inputBufferReadIndex];
                double retElapsed = inputPackageBufferElapsedStamps[inputBufferReadIndex];

                // clear the packages reference
                inputPackageBuffer[inputBufferReadIndex] = null;
                inputPackageBufferElapsedStamps[inputBufferReadIndex] = -1;

                // set the read index to the next package
                inputBufferReadIndex++;
                if (inputBufferReadIndex == inputPackageBuffer.Length) inputBufferReadIndex = 0;

                // decrease the amount of packages in the buffer as this package will be processed
                numberOfPackagesInBuffer--;

                // return the data (last)
                data = retData;
                elapsed = retElapsed;

            }   // end of lock
        }


        /// <summary>
        /// Retrieve the next row of samples from the input-buffer (used for data files V1, can be considered a sample-package with a single sample)
        /// </summary>
        /// <returns>The next sample-row from the buffer, or null if no further data is available</returns>
        private double[] getNextInputRow_v1() {

            lock (lockInputBuffer) {

                // if there are no rows in the buffer, return null
                if (numberOfRowsInBuffer == 0)      return null;

                // read the values
                double[] values = new double[inputHeader.numColumns - 1];
                Buffer.BlockCopy(inputRowBuffer, (int)(inputBufferReadIndex + sizeof(uint)), values, 0, inputBufferRowSize - (sizeof(uint)));

                // set the read index to the next row
                inputBufferReadIndex += inputBufferRowSize;
                if (inputBufferReadIndex == inputRowBuffer.Length) inputBufferReadIndex = 0;

                // decrease the amount of rows in the buffer as this row will be processed (to make space for another row in the input buffer)
                numberOfRowsInBuffer--;

                // return the data
                return values;

            }   // end of lock
        }



        private void updateInputBuffer() {

            // variable to flag whether an update should be done
            bool doUpdate = false;

            // thread safety
            lock (lockInputReader) {
                lock (lockInputBuffer) {

                    // check if the inputread is not null
                    if (inputReader == null)    return;

                    // check if there is nothing left to read, if so return directly
                    if (inputReader.reachedEnd())   return;

                    // check if the minimum amount of rows in the buffer has been reached
                    if (inputHeader.version == 2 || inputHeader.version == 3) {
                        doUpdate = numberOfPackagesInBuffer <= inputBufferMinTillRead;
                    } else if (inputHeader.version == 1) {
                        doUpdate = numberOfRowsInBuffer <= inputBufferMinTillRead;

                    }

                }
            }

            // if an update is required
            if (doUpdate) {
                if (inputHeader.version == 2 || inputHeader.version == 3) {

                    // check if the buffer is not big enough to contain the rows that are set to be read
                    if ((inputBufferSize - numberOfPackagesInBuffer) < inputBufferReadSize) {
                        logger.Warn("Input buffer is not empty enough to update with new sample-packages, skipping update now until buffer is more empty");
                        return;
                    }
                     
                    // create variables for reading
                    double[][] packageData = null;
                    double[] packageElapsedStamps = null;
                    long packagesRead = -1;

                    // read new data from file
                    // Note: thread safety (seperate here because with big data, a read action could some time and 
                    //       doing it like this prevents keeps the inputRowBuffer available for reading during that time).
                    lock (lockInputReader) {
                        packagesRead = inputReader.readNextPackages(inputBufferReadSize, out packageData, out packageElapsedStamps);
                    }
                    
                    // check if the reading was succesfull
                    if (packagesRead == -1) {
                        // error while reading (end of file is also possible, but is checked before using 'reachedEnd')
                        logger.Error("Error while updating buffer, reading sample-packages from file failed");
                        return;
                    }

                    // successfully retrieved new input packages
                    // Note: thread safety (seperate here, so the inputPackageBuffer will be locked as
                    //       short as possible to allow the least interruption/wait while reading)
                    lock (lockInputBuffer) {

                        // move the sample-packages into the buffer
                        for (int i = 0; i < packagesRead; i++) {
                            inputPackageBuffer[inputBufferAddIndex] = packageData[i];
                            inputPackageBufferElapsedStamps[inputBufferAddIndex] = packageElapsedStamps[i];

                            // move the input to add buffer
                            inputBufferAddIndex++;
                            if (inputBufferAddIndex >= inputPackageBuffer.Length)   inputBufferAddIndex -= inputPackageBuffer.Length;

                        }

                        // count to the total number of packages in the buffer
                        numberOfPackagesInBuffer += packagesRead;

                    } // end lock


                } else if (inputHeader.version == 1) {

                    // check if the buffer is not big enough to contain the rows that are set to be read
                    if ((inputBufferSize - numberOfRowsInBuffer) < inputBufferReadSize) {
                        logger.Warn("Input buffer is not empty enough to update with new rows, skipping update now until buffer is more empty");
                        return;
                    }

                    // create variables for reading
                    byte[] rowData = null;
                    long rowsRead = -1;

                    // read new data from file
                    // Note: thread safety (seperate here because with big data, a read action could some time and 
                    //       doing it like this prevents keeps the inputRowBuffer available for reading during that time).
                    lock (lockInputReader) {
                        rowsRead = inputReader.readNextRows_V1(inputBufferReadSize, out rowData);
                    }

                    // check if the reading was succesfull
                    if (rowsRead == -1) {
                        // error while reading (end of file is also possible, but is checked before using 'reachedEnd')

                        logger.Error("Error while updating buffer, reading input-rows from file failed");
                        return;

                    }

                    // successfully retrieved new input rows
                    // Note: thread safety (seperate here, so the inputRowBuffer will be locked as
                    //       short as possible to allow the least interruption/wait while reading)
                    lock (lockInputBuffer) {

                        // determine how much fits in the buffer from the add pointer (inputBufferAddIndex) till the end of the buffer
                        // and determine how goes after wrapping around
                        long length = rowData.Length;
                        long lengthLeft = 0;
                        if (inputRowBuffer.Length - inputBufferAddIndex < length) {
                            length = inputRowBuffer.Length - inputBufferAddIndex;
                            lengthLeft = rowData.Length - length;
                        }

                        // write the first part to the buffer
                        Buffer.BlockCopy(rowData, 0, inputRowBuffer, (int)inputBufferAddIndex, (int)(length));

                        // if it needs to wrap around, write the second past from the start of the buffer
                        if (lengthLeft != 0)
                            Buffer.BlockCopy(rowData, (int)length, inputRowBuffer, 0, (int)(lengthLeft));

                        // update input buffer variables
                        inputBufferAddIndex += rowsRead * inputBufferRowSize;
                        if (inputBufferAddIndex >= inputRowBuffer.Length) inputBufferAddIndex -= inputRowBuffer.Length;
                        numberOfRowsInBuffer += rowsRead;

                    } // end lock

                }  // end of data-version conditional

            }   // end of doUpdate conditional

        }   // end function


    }   // end of class

}
