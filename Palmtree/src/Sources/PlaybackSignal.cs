/**
 * The PlaybackSignal class
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
using System.Diagnostics;
using System.IO;
using System.Threading;
using Palmtree.Core;
using Palmtree.Core.DataIO;
using Palmtree.Core.Helpers;
using Palmtree.Core.Params;

namespace Palmtree.Sources {

    /// <summary>
    /// The <c>PlaybackSignal</c> class.
    /// 
    /// ...
    /// </summary>
    public class PlaybackSignal : ISource {

        private const string CLASS_NAME = "PlaybackSignal";
        private const int CLASS_VERSION = 1;

        private const int threadLoopDelayNoProc = 200;                                  // thread loop delay when not processing (1000ms / 5 run times per second = rest 200ms)

        private const double INPUT_BUFFER_SIZE_SECONDS = 20.0;                          // the size of the input buffer, defined as the number of seconds of data it should hold
        private const double INPUT_BUFFER_MIN_READ_SECONDS = 5.0;                       // the minimum to which the input buffer should be filled before another read, defined as the number of seconds of data

        private static Logger logger = LogManager.GetLogger(CLASS_NAME);
        private static Parameters parameters = ParameterManager.GetParameters(CLASS_NAME, Parameters.ParamSetTypes.Source);
        
        private Thread signalThread = null;                                                     // the source thread
        private bool running = true;                                                            // flag to define if the source thread should be running (setting to false will stop the source thread)
        private ManualResetEvent playbackLoopManualResetEvent = new ManualResetEvent(false);    // Manual reset event to call the WaitOne event on (this allows - in contrast to the sleep wait - to cancel the wait period at any point when closing the source) 
        private Stopwatch swTimePassed = new Stopwatch();                                       // stopwatch object to give an exact amount to time passed inbetween loops
        private int sampleIntervalMs = 0;                                                       // interval between the samples in milliseconds
        private long sampleIntervalTicks = 0;                                                   // interval between the samples in ticks (for high precision timing)
        private int threadLoopDelay = 0;
        private long highPrecisionWaitTillTime = 0;                                             // stores the time until which the high precision timing waits before continueing

        private bool configured = false;
        private bool initialized = false;
        private bool started = false;				                                            // flag to define if the source is started or stopped
        private Object lockStarted = new Object();

        private int outputChannels = 0;
        private double sampleRate = 0;                                                          // hold the amount of samples per second that the source outputs (used by the mainthead to convert seconds to number of samples)
        private bool timingByFile = false;                                                      // hold whether the timing of the samples is based on the elapsed time in the data file
        private bool highPrecision = false;                                                     // hold whether the generator should have high precision intervals
        private bool redistributeEnabled = false;                                               // hold whether redistribution of input to output channels is enabled
        private int[] redistributeChannels = null;                                              // holds how the channels should be redistributed
        

        private string inputFile = "";                                                          // filepath to the file(s) to use for playback
        private bool readEntireFileInMemory = false;                                            // whether the entire file should be read into memory (on initialization)

        private Thread bufferThread = null;                                                     // input buffer thread (checks the buffer fill, and read new data from the file
        private ManualResetEvent bufferLoopManualResetEvent = new ManualResetEvent(false);      //The input buffer's manual reset event to call the WaitOne event on (this allows - in contrast to the sleep wait - to cancel the wait period at any point when closing the source) 
        private long inputBufferSize = 0;                                                       // the total size of the input buffer in number of rows 
        private long inputBufferMinTillRead = 0;                                                // the minimum amount of rows until additional read
        private long inputBufferRowsPerRead = 0;                                                // the number of rows per read call

        private Object lockInputReader = new Object();                                          // threadsafety lock for input reader
        private DataReader inputReader = null;
        private DataHeader inputHeader = null;

        private Object lockInputBuffer = new Object();                                          // threadsafety lock for input buffer
        private byte[] inputBuffer = null;                                                      // input (byte) ringbuffer
        private int inputBufferRowSize = 0;                                                     // rowsize (in bytes). A copy of the value from the header. A copy because this way we do not have to (thread)lock the reader/header objects when we just want to read the data
        private long inputBufferAddIndex = 0;                                                   // the index where in the (ring) input buffer the next row will be added
        private long inputBufferReadIndex = 0;                                                  // the index where in the (ring) input buffer the next row is that should be read
        private long numberOfRowsInBuffer = 0;                                                  // the number of added but unread rows in the (ring) input buffer

        private double[] nextSample = null;                                                     // next sample to send into the pipeline


        public PlaybackSignal() {
            
            parameters.addParameter<ParamFileString>(
                "Input",
                "The data input file(s) that should be used for playback.\nWhich file of a set is irrelevant as long as the set has the same filename (the file extension is ignored as multiple files might be used).",
                "", "", "");

            parameters.addParameter<int[]>(
                "RedistributeChannels",
                "Redistributes the channels.\nIf this field is set, then the number of values here determines the number of output channels of the source, where every value becomes one output channel.\nThe value given represents the input channel from the data file to take as output.\nFor example when a source has two input channels, entering '1 2' in this field would playback as recorded, however, entering '2 1' would switch the input channels for output.\n\nNote: if left empty it will playback the channels as specified in the data file",
                "", "", "");

            parameters.addParameter<bool>(
                "ReadEntireFileInMemory",
                "Read the entire data file into memory at initialization. Note: that - depending on the data file size - could cause high memory usage.",
                "", "", "0");

            parameters.addParameter<bool>(
                "TimingByFile",
                "Base the sample timing on the interval values of the data file instead of using the samplerate. Not recommended to switch this on.",
                "", "", "0");

            parameters.addParameter<bool>(
                "HighPrecision",
                "Use high precision intervals when generating sample.\nNote 1: Enabling this option will claim one processor core entirely, possibly causing your system to slow down or hang.\nNote 2: High precision will be enabled automatically when a sample rate is set to more than 1000 hz.",
                "", "", "0");

            // message
            logger.Info("Source created (version " + CLASS_VERSION + ")");

            // start a new thread
            signalThread = new Thread(this.run);
            signalThread.Name = "PlaybackSignal Playback Run Thread";
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
         * This value could be requested by the main thread and is used to allow parameters
         * to be converted from seconds to samples
         **/
        public double getOutputSamplesPerSecond() {

            // check if the source is not configured yet
            if (!configured) {

                // message
                logger.Error("Trying to retrieve the samples per second before the source was configured, first configure the source, returning 0");

                // return 0
                return 0;

            }

            // return the samples per second
            return sampleRate;

        }

        public bool configure(out PackageFormat output) {
            #pragma warning disable 0162            // for constant checks, conscious ignore
            
            // retrieve whether the file should be read into memory
            readEntireFileInMemory = parameters.getValue<bool>("ReadEntireFileInMemory");

            // retrieve the input file and remove the extension
            inputFile = parameters.getValue<string>("Input");
            int extIndex = inputFile.LastIndexOf('.');
            if (extIndex != -1)     inputFile = inputFile.Substring(0, extIndex);

            // check if the .dat file exists
            string inputDatFile = inputFile + ".dat";
            if (string.IsNullOrEmpty(inputDatFile) || !File.Exists(inputDatFile)) {
                
                // message
                logger.Error("Could not find playback input .dat file '" + inputDatFile + "'");

                // return
                output = null;
                return false;

            }

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
                    DataHeader header = DataReader.readHeader(inputDatFile);
                    if (header == null) {

                        // message
                        logger.Error("Could not read header data from input .dat file '" + inputDatFile + "'");

                        // return
                        output = null;
                        return false;

                    }

                    // check if the internal code is 'dat'
                    if (string.Compare(header.code, "dat") != 0) {

                        // message
                        logger.Error("The input .dat file is internally marked as '" + header.code + "', while a data stream ('dat') file is required");

                        // return
                        output = null;
                        return false;

                    }

                    // check if the number of playback input streams in the .dat is higher than 0
                    if (header.numPlaybackStreams <= 0) {

                        // message
                        logger.Error("The input .dat file has no playback input streams, these are required for playback, make sure the LogPipelineInputStream setting (data tab) is switched on while recording data for replay");

                        // return
                        output = null;
                        return false;

                    }

                    // retrieve the redistribution of channels
                    redistributeChannels = parameters.getValue<int[]>("RedistributeChannels");
                    redistributeEnabled = redistributeChannels.Length > 0;
                    for (int i = 0; i < redistributeChannels.Length; i++) {

                        if (redistributeChannels[i] < 1 || redistributeChannels[i] % 1 != 0) {
                            logger.Error("The values in the RedistributeChannels parameter should be positive integers (note that the channel numbering is 1-based)");
                            output = null;
                            return false;
                        }
                        if (redistributeChannels[i] > header.numPlaybackStreams) {
                            logger.Error("One of the values in the RedistributeChannels parameter exceeds the number of playback (input) streams in the data file (#playbackStreams: " + header.numPlaybackStreams + ")");
                            output = null;
                            return false;
                        }

                        // lower each channel value (1-based), so it can be used immediately to point to the right value in the input stream array (0-based)
                        redistributeChannels[i]--;

                    }

                    // check if the channels are redistributed
                    if (redistributeEnabled) {

                        // set the number of output channels to the number of redistributed channels
                        outputChannels = redistributeChannels.Length;

                    } else {

                        // set the number of output channels for this source based on the playback streams in the .dat file
                        outputChannels = header.numPlaybackStreams;

                    }

                    // set the sample rate for this source based on the .dat file
                    sampleRate = header.sampleRate;

                    // check the sample rate
                    if (sampleRate <= 0) {
                        logger.Error("The sample rate in the (header of the) .dat file is 0 or lower, invalid sample rate");
                        output = null;
                        return false;
                    }

                    // create a sampleformat
                    // (at this point we can only playback .day files with the pipeline input streams, these always have 1 sample per package.
                    // since the number of samples is 1 per package, the given samplerate is the packagerate)
                    output = new PackageFormat(outputChannels, 1, sampleRate);      

                    // check the constants (buffer size in combination with buffer min read)
                    if (INPUT_BUFFER_MIN_READ_SECONDS < 2) {
                        logger.Error("The buffer minimum-till-read should not be less than two seconds, provide a larger value for INPUT_BUFFER_MIN_READ_SECONDS");
                        return false;

                    }
                    if (INPUT_BUFFER_MIN_READ_SECONDS > INPUT_BUFFER_SIZE_SECONDS) {
                        logger.Error("The buffer minimum-till-read is larger than the buffer, either adjust INPUT_BUFFER_SIZE_SECONDS or INPUT_BUFFER_MIN_READ_SECONDS");
                        return false;
                    }

                    // calculate the input buffer size (the size of the inputbuffer is defined as number of seconds of data to hold)
                    inputBufferSize = (long)Math.Floor(INPUT_BUFFER_SIZE_SECONDS * header.sampleRate);
                    if (inputBufferSize == 0) {
                        logger.Error("The buffer size is too small when combined with the sample rate, provide a larger value for INPUT_BUFFER_SIZE_SECONDS");
                        return false;
                    }

                    // calculate the minimum amount of rows until additional read
                    inputBufferMinTillRead = (long)Math.Floor(INPUT_BUFFER_MIN_READ_SECONDS * header.sampleRate);
                    if (inputBufferMinTillRead == 0) {
                        logger.Error("The buffer minimum-till-read is too small when combined with the sample rate, provide a larger value for INPUT_BUFFER_MIN_READ_SECONDS");
                        return false;
                    }
                    if (inputBufferMinTillRead >= inputBufferSize) {
                        logger.Error("The buffer minimum-till-read should be smaller than the input buffer size, provide a smaller value for INPUT_BUFFER_MIN_READ_SECONDS");
                        return false;
                    }

                    // calculate the number of rows to read when the minimum is reached
                    // (note: a smaller read step is also possible; current just refilling the buffer by taking the difference between the minimum-till-read and total buffer size)
                    inputBufferRowsPerRead = inputBufferSize - inputBufferMinTillRead;

                    // the entire file should be read into memory, or if the number of rows in the file are equal to or smaller than the input buffer rows,
                    // then the buffer size will be based on the file row-size
                    if (readEntireFileInMemory || header.numRows <= inputBufferSize) {

                        // input buffer will be the same size as the data in the input file
                        inputBufferSize = header.numRows;

                        // not necessary as the entire file will be read in on initialize
                        inputBufferMinTillRead = 1;
                        inputBufferRowsPerRead = 1;

                    }

                    // check if the number of rows per read is not too big
                    // note: should not happen since it is calculated based on the buffer size and min-till-read, just in case)
                    if (inputBufferRowsPerRead > inputBufferSize - inputBufferMinTillRead) {
                        logger.Error("Number of rows per read (" + inputBufferRowsPerRead + ") should be smaller than the space in the buffer that is open when the buffer min-till-read is reached ('" + (inputBufferSize - inputBufferMinTillRead) + ")");
                        return false;
                    }

                    // write some playback information
                    logger.Info("Playback data file: " + inputDatFile);
                    logger.Info("Data file version: " + header.version);
                    logger.Info("Pipeline sample rate: " + sampleRate);
                    logger.Info("Number of input streams in file: " + header.numPlaybackStreams);
                    logger.Info("Number of output channels: " + outputChannels);
                    logger.Info("Number of rows: " + header.numRows);

                } // end lock
            } // end lock

            // retrieve the timing by file setting
            timingByFile = parameters.getValue<bool>("TimingByFile");

            // retrieve the high precision setting
            highPrecision = parameters.getValue<bool>("HighPrecision");

            // calculate the sample interval
            sampleIntervalMs = (int)Math.Floor(1000.0 / sampleRate);

            // check if the samplerate is above 1000hz
            if (sampleRate > 1000) {

                // enable the high precision timing
                highPrecision = true;

                // message
                logger.Warn("Because the sample rate is larger than 1000hz, the high precision timer is used");

            }

            // check if high precision timing is enabled
            if (highPrecision) {

                // calculate the sample interval for the high precision timing
                sampleIntervalTicks = (long)Math.Round(Stopwatch.Frequency * (1.0 / sampleRate));

                // message
                logger.Warn("High precision timer enabled, as one core will be claimed entirely this might have consequences for your system performance");

            }

            // flag as configured
            configured = true;

            // return success
            return true;

        }

        public void initialize() {

            // thread safety
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
                string inputDatFile = inputFile + ".dat";
                inputReader = new DataReader(inputDatFile);
                inputReader.open();

                // retrieve the header
                inputHeader = inputReader.getHeader();

                // copy the rowsize to a local variable
                // (A copy because this way we do not have to (thread)lock the reader/header objects when we just want to read the data)
                inputBufferRowSize = inputHeader.rowSize;

            }

            // initialize the input buffer and already fill it with the rows
            initInputBuffer();

            // flag the initialization as complete
            initialized = true;

        }

        /**
	     * Start
	     */
        public void start() {

            // check if configured and the source was initialized
            if (!configured || !initialized) {
                return;
            }

            // lock for thread safety
            lock(lockStarted) {

                // check if the generator was not already started
                if (started) return;

                if (inputBuffer == null) {

                    // initialize the input buffer and already fill it with the rows
                    initInputBuffer();

                }

                // take the first sample from the input buffer
                nextSample = getNextInputRow();

                // start playback
                started = true;

                // interrupt the loop wait, allowing the loop to continue (in case it was waiting the noproc interval)
                // causing an immediate start and switching to the processing waittime
                playbackLoopManualResetEvent.Set();

                // check if the entire file was not already read
                if (!readEntireFileInMemory) {

                    // start a new thread to keep the input buffer updated
                    bufferThread = new Thread(this.runUpdateInputBuffer);
                    bufferThread.Name = "PlaybackSignal Update Inputbuffer Run Thread";
                    bufferThread.Start();

                }

            }
		
        }


        /**
	     * Stop
	     */
        public void stop() {

            // if not initialized than nothing needs to be stopped
            if (!initialized)   return;

            // lock for thread safety
            lock (lockStarted) {

                // check if the source is generating signals
                if (started) {

                    // stop playback
                    started = false;

                }

            }

            // interrupt the playback loop wait, allowing the loop to continue (in case it was waiting the proc interval)
            // switching to the no-processing waittime
            playbackLoopManualResetEvent.Set();

            // check if there is a update input buffer thread
            if (bufferThread != null) {

                // interrupt the loop wait, allowing the loop to continue and exit
                bufferLoopManualResetEvent.Set();

                // wait until the buffer input thread stopped
                // try to stop the main loop using running
                int waitCounter = 5000;
                while (bufferThread.IsAlive && waitCounter > 0) {
                    Thread.Sleep(1);
                    waitCounter--;
                }

                // clear the buffer thread reference
                bufferThread = null;

            }

            // thread safety
            lock (lockInputBuffer) {

                // clear the input buffer and input buffer variables
                inputBuffer = null;
                numberOfRowsInBuffer = 0;
                inputBufferReadIndex = 0;
                inputBufferAddIndex = 0;

            }

        }

	    /**
	     * Returns whether the signalgenerator is generating signal
	     * 
	     * @return Whether the signal generator is started
	     */
	    public bool isStarted() {

            // lock for thread safety
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
            // (this is done because if the sample rate is low, we might have to wait for a long time for the thread to end)
            playbackLoopManualResetEvent.Set();

            // wait until the thread stopped
            // try to stop the main loop using running
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
	     * Note, this is something different than actually generating
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
                bufferLoopManualResetEvent.Reset();

                // Sleep wait
                bufferLoopManualResetEvent.WaitOne(threadLoopDelayNoProc);      // using WaitOne because this wait is interruptable (in contrast to sleep)

            }

        }

        /**
	     * Source playback thread
	     */
        private void run() {

            // name this thread
            if (Thread.CurrentThread.Name == null) {
                Thread.CurrentThread.Name = "Source Thread";
            }

            // log message
            logger.Debug("Thread started");

            // set an initial start for the stopwatch
            swTimePassed.Start();

		    // loop while running
		    while(running) {

                // lock for thread safety
                lock(lockStarted) {

			        // check if we are generating
			        if (started) {

                        // check if there is a next sample
                        if (nextSample == null) {
                            // no next sample

                            // message
                            logger.Info("Playback of the file is finished, calling stop.");

                            // send a stop signal to the mainThread
                            MainThread.stop(false);


                        } else {
                            // there is a next sample (previously retrieved)

                            
                            // create sample to return
                            double[] sample = new double[outputChannels];

                            // check if redistribution of channels is enabled
                            if (redistributeEnabled) {

                                // redistribute
                                for (int i = 0; i < outputChannels; i++) {
                                    sample[i] = nextSample[redistributeChannels[i] + 1];        // '+ 1' = skip the elapsed time column
                                }

                            } else {
                                // set values for the generated sample
                                
                                for (int i = 0; i < outputChannels; i++) {
                                    sample[i] = nextSample[i + 1];                              // '+ 1' = skip the elapsed time column
                                }

                            }


                            // pass the sample
                            MainThread.eventNewSample(sample);
                            


                        }

                        // (try to) retrieve the next sample
                        nextSample = getNextInputRow();

                    }

                }

                
			    // if still running then wait to allow other processes
			    if (running) {
                    
                    // check if we are generating
                    // (note: we deliberately do not lock the started variable here, the locking will delay/lock out 'start()' during the wait here
                    //  and if these are not in sync, the worst thing that can happen is that it does waits one loop extra, which is no problem)
                    if (started) {

                        if (highPrecision) {
                            // high precision timing

                            // spin for the required amount of ticks
                            highPrecisionWaitTillTime = Stopwatch.GetTimestamp() + sampleIntervalTicks;     // choose not to correct for elapsed ticks. This result in a slightly higher wait time, which at lower Hz comes closer to the expected samplecount per second
                            //highPrecisionWaitTillTime = Stopwatch.GetTimestamp() + sampleIntervalTicks - swTimePassed.ElapsedTicks;
                            while (Stopwatch.GetTimestamp() <= highPrecisionWaitTillTime) ;

                        } else {
                            // low precision timing

                            threadLoopDelay = sampleIntervalMs;     // choose not to correct for elapsed ms. This result in a slightly higher wait time, which at lower Hz comes closer to the expected samplecount per second
                            //threadLoopDelay = sampleIntervalMs - (int)swTimePassed.ElapsedMilliseconds;

                            // wait for the remainder of the sample interval to get as close to the sample rate as possible (if there is a remainder)
                            if (threadLoopDelay >= 0) {

                                // reset the manual reset event, so it is sure to block on the next call to WaitOne
                                // 
                                // Note: not using AutoResetEvent because it could happen that .Set is called while not in WaitOne yet, when
                                // using AutoResetEvent this will cause it to skip the next WaitOne call
                                playbackLoopManualResetEvent.Reset();

                                // Sleep wait
                                playbackLoopManualResetEvent.WaitOne(threadLoopDelay);      // using WaitOne because this wait is interruptable (in contrast to sleep)
                                
                            }

                        }

                    } else {

                        // reset the manual reset event, so it is sure to block on the next call to WaitOne
                        // 
                        // Note: not using AutoResetEvent because it could happen that .Set is called while not in WaitOne yet, when
                        // using AutoResetEvent this will cause it to skip the next WaitOne call
                        playbackLoopManualResetEvent.Reset();

                        // Sleep wait
                        playbackLoopManualResetEvent.WaitOne(threadLoopDelayNoProc);      // using WaitOne because this wait is interruptable (in contrast to sleep)

                    }

                    // restart the timer to measure the loop time
                    swTimePassed.Restart();

                }

            }
            
            // log message
            logger.Debug("Thread stopped");

        }

        
        private void initInputBuffer() {

            // thread safety
            lock (lockInputReader) {
                lock (lockInputBuffer) {

                    // set the data pointer of the input reader to the start
                    inputReader.resetDataPointer();

                    // read the input buffer (full)
                    long rowsRead = inputReader.readNextRows(inputBufferSize, out inputBuffer);

                    // set the input ringbuffer variables
                    inputBufferAddIndex = rowsRead * inputHeader.rowSize;
                    numberOfRowsInBuffer = rowsRead;

                    // set the input buffer read index at the start (since the buffer was filled from 0)
                    inputBufferReadIndex = 0;

                }

            }

        }

        private double[] getNextInputRow() {

            // thread safety
            lock (lockInputBuffer) {

                // if there are no rows in the buffer, return null
                if (numberOfRowsInBuffer == 0)      return null;

                // read the values
                double[] values = new double[inputHeader.numColumns - 1];
                Buffer.BlockCopy(inputBuffer, (int)(inputBufferReadIndex + sizeof(uint)), values, 0, inputBufferRowSize - (sizeof(uint)));

                // set the read index to the next row
                inputBufferReadIndex += inputBufferRowSize;
                if (inputBufferReadIndex == inputBuffer.Length) inputBufferReadIndex = 0;

                // decrease the amount of rows in the buffer as this row will be processed (to make space for another row in the input buffer)
                numberOfRowsInBuffer--;

                // return the data
                return values;

            }

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
                    if (numberOfRowsInBuffer <= inputBufferMinTillRead) {

                        // retrieve 
                        doUpdate = true;

                    }
                }
            }

            // if an update is required
            if (doUpdate) {

                // check if the buffer is not big enough to contain the rows that are set to be read
                // (note, extra since the stepsize should be smaller than 
                if ((inputBufferSize - numberOfRowsInBuffer) < inputBufferRowsPerRead) {

                    // message
                    logger.Warn("Input buffer is not empty enough to update with new rows, skipping update now until buffer is more empty");

                    // return without updating
                    return;

                }

                // create variables for reading
                byte[] rowData = null;
                long rowsRead = -1;

                // thread safety (seperate here because with big data, a read action could some time and 
                // doing it like this prevents keeps the inputBuffer available for reading during that time).
                lock (lockInputReader) {

                    // read new data from file
                    rowsRead = inputReader.readNextRows(inputBufferRowsPerRead, out rowData);

                }

                // check if the reading was succesfull
                if (rowsRead == -1) {
                    // error while reading (end of file is also possible, but is checked before using 'reachedEnd')

                    // message
                    logger.Error("Error while updating buffer, reading inputrows from file failed");

                } else {
                    // successfully retrieved new input rows
                    
                    // thread safety (seperate here, so the inputbuffer will be locked as short as possible to allow the least
                    // interruption/wait while reading)
                    lock (lockInputBuffer) {

                        // determine how much fits in the buffer from the add pointer (inputBufferAddIndex) till the end of the buffer
                        // and determine how goes after wrapping around
                        long length = rowData.Length;
                        long lengthLeft = 0;
                        if (inputBuffer.Length - inputBufferAddIndex < length) {
                            length = inputBuffer.Length - inputBufferAddIndex;
                            lengthLeft = rowData.Length - length;
                        }

                        // write the first part to the buffer
                        Buffer.BlockCopy(rowData, 0, inputBuffer, (int)inputBufferAddIndex, (int)(length));

                        // if it needs to wrap around, write the second past from the start of the buffer
                        if (lengthLeft != 0)
                            Buffer.BlockCopy(rowData, (int)length, inputBuffer, 0, (int)(lengthLeft));
                        
                        // update input buffer variables
                        inputBufferAddIndex += rowsRead * inputBufferRowSize;
                        if (inputBufferAddIndex > inputBuffer.Length) inputBufferAddIndex = inputBufferAddIndex - inputBuffer.Length;
                        numberOfRowsInBuffer += rowsRead;

                    } // end lock

                }

            } // end function


        }


    }

}
