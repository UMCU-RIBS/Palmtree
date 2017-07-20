using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using UNP.Core;
using UNP.Core.DataIO;
using UNP.Core.Helpers;
using UNP.Core.Params;

namespace UNP.Sources {

    public class PlaybackSignal : ISource {

        private const string CLASS_NAME = "PlaybackSignal";
        private const int CLASS_VERSION = 0;

        private const double INPUT_BUFFER_SIZE_SECONDS = 20.0;                          // the size of the input buffer, defined as the number of seconds of data it should hold
        private const double INPUT_BUFFER_MIN_READ_SECONDS = 5.0;                       // the minimum to which the input buffer should be filled before another read, defined as the number of seconds of data

        private static Logger logger = LogManager.GetLogger(CLASS_NAME);
        private static Parameters parameters = ParameterManager.GetParameters(CLASS_NAME, Parameters.ParamSetTypes.Source);

        private MainThread main = null;

        private Thread signalThread = null;                                             // the source thread
        private bool running = true;					                                // flag to define if the source thread should be running (setting to false will stop the source thread)
        private ManualResetEvent loopManualResetEvent = new ManualResetEvent(false);    // Manual reset event to call the WaitOne event on (this allows - in contrast to the sleep wait - to cancel the wait period at any point when closing the source) 
        private Stopwatch swTimePassed = new Stopwatch();                               // stopwatch object to give an exact amount to time passed inbetween loops
        private int sampleInterval = 1000;                                              // interval between the samples in milliseconds
        private int threadLoopDelay = 0;

        private bool configured = false;
        private bool initialized = false;
        private bool started = false;				                                    // flag to define if the source is started or stopped
        private Object lockStarted = new Object();

        private Random rand = new Random(Guid.NewGuid().GetHashCode());
        private int outputChannels = 0;
        private double sampleRate = 0;                                                  // hold the amount of samples per second that the source outputs (used by the mainthead to convert seconds to number of samples)

        private string inputFile = "";                                                  // filepath to the file(s) to use for playback
        private bool readEntireFileInMemory = false;                                    // whether the entire file should be read into memory (on initialization)

        private long inputBufferSize = 0;                                               // the total size of the input buffer in number of rows 
        private long inputBufferMinTillRead = 0;                                        // the minimum amount of rows until additional read
        private long inputBufferRowsPerRead = 0;                                        // the number of rows per read call

        private Object lockInputReader = new Object();                                  // threadsafety lock for input reader
        private DataReader inputReader = null;
        private DataHeader inputHeader = null;

        private Object lockInputBuffer = new Object();                                  // threadsafety lock for input buffer
        private byte[] inputBuffer = null;                                              // input (byte) ringbuffer
        private long inputBufferAddIndex = 0;                                           // the index where in the (ring) input buffer the next row will be added
        private long inputBufferReadIndex = 0;                                          // the index where in the (ring) input buffer the next row is that should be read
        private long numberOfRowsInBuffer = 0;                                          // the number of added but unread rows in the (ring) input buffer
        
        public PlaybackSignal(MainThread main) {

            // set the reference to the main
            this.main = main;
            
            parameters.addParameter<string>(
                "Input",
                "The data input file(s) that should be used for playback.\nWhich file of a set is irrelevant as long as the set has the same filename (the file extension is ignored as multiple files might be used).",
                "", "", "");

            parameters.addParameter<bool>(
                "ReadEntireFileInMemory",
                "Read the entire data file into memory at initialization. Note: that - depending on the data file size - could cause high memory usage.",
                "", "", "0");

            // start a new thread
            signalThread = new Thread(this.run);
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

        /**
         * function to retrieve the number of samples per second
         * 
         * This value could be requested by the main thread and is used to allow parameters
         * to be converted from seconds to samples
         **/
        public double getSamplesPerSecond() {

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

        public bool configure(out SampleFormat output) {
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

                    // check if the internal data type is dat
                    if (string.Compare(header.extension, "dat") != 0) {

                        // message
                        logger.Error("The input .dat file is internally marked as '" + header.extension + "', while a data stream file is required");

                        // return
                        output = null;
                        return false;

                    }

                    // check if the number of pipeline input streams in the .dat is higher than 0
                    if (header.pipelineInputStreams <= 0) {

                        // message
                        logger.Error("The input .dat file has no pipeline input streams, these are required for playback, make sure the LogPipelineInputStream setting (data tab) is switched on while recording data for replay");

                        // return
                        output = null;
                        return false;

                    }

                    // set the number of output channels for this source based on the .dat file
                    outputChannels = header.pipelineInputStreams;

                    // set the sample rate for this source based on the .dat file
                    sampleRate = header.pipelineSampleRate;

                    // check the sample rate
                    if (sampleRate <= 0) {
                        logger.Error("The sample rate in the (header of the) .dat file is 0 or lower, invalid sample rate");
                        output = null;
                        return false;
                    }

                    // create a sampleformat
                    output = new SampleFormat(outputChannels, sampleRate);

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
                    inputBufferSize = (long)Math.Floor(INPUT_BUFFER_SIZE_SECONDS * header.pipelineSampleRate);
                    if (inputBufferSize == 0) {
                        logger.Error("The buffer size is too small when combined with the pipeline sample rate, provide a larger value for INPUT_BUFFER_SIZE_SECONDS");
                        return false;
                    }

                    // calculate the minimum amount of rows until additional read
                    inputBufferMinTillRead = (long)Math.Floor(INPUT_BUFFER_MIN_READ_SECONDS * header.pipelineSampleRate);
                    if (inputBufferMinTillRead == 0) {
                        logger.Error("The buffer minimum-till-read is too small when combined with the pipeline sample rate, provide a larger value for INPUT_BUFFER_MIN_READ_SECONDS");
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
                    logger.Info("Number of pipeline input streams / output channels: " + outputChannels);
                    logger.Info("Number of rows: " + header.numRows);

                } // end lock
            } // end lock

            // flag as configured
            configured = true;

            // return success
            return true;

        }

        public void initialize() {

            // interrupt the loop wait. The loop will reset the wait lock (so it will wait again upon the next WaitOne call)
            // this will make sure the newly set sample rate interval is applied in the loop
            loopManualResetEvent.Set();

            // thread safety
            lock (lockInputReader) {

                // check if there is still an inputreader open, if so close
                // (is also done in configure, but just in case these do not follow each other)
                if (inputReader != null) {
                    inputReader.close();
                    inputReader = null;
                    inputHeader = null;
                }

                // open the input reader
                string inputDatFile = inputFile + ".dat";
                inputReader = new DataReader(inputDatFile);
                inputReader.open();

                // retrieve the header
                inputHeader = inputReader.getHeader();

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
                if (started)     return;
                
                if (inputBuffer == null) {
                    
                    // initialize the input buffer and already fill it with the rows
                    initInputBuffer();

                }




                while (numberOfRowsInBuffer > 0) {

                    updateInputBuffer();

                    getNextInputRow();
                }
                

                // start playback
                started = true;

                // interrupt the loop wait, making the loop to continue (in case it was waiting the sample interval)
                // causing an immediate start, this makes it feel more responsive
                loopManualResetEvent.Set();

            }
		
        }


        private void initInputBuffer() {

            // thread safety
            lock (lockInputReader) {
                lock (lockInputBuffer) {

                    // set the data pointer of the input reader to the start
                    inputReader.resetDataPointer();

                    // read the input buffer (full)
                    long rowsRead = inputReader.readNextRows(inputBufferSize, out inputBuffer);
                    logger.Error("               -  - " + rowsRead);

                    // set the input ringbuffer variables
                    inputBufferAddIndex = rowsRead * inputHeader.rowSize;
                    numberOfRowsInBuffer = rowsRead;

                    // set the input buffer read index at the start (since the buffer was filled from 0)
                    inputBufferReadIndex = 0;

                }
            }

        }

        private void getNextInputRow() {

            // thread safety
            lock (lockInputBuffer) {

                // if there are no rows in the buffer, return empty
                if (numberOfRowsInBuffer == 0)      return;

                uint sampleCounter = BitConverter.ToUInt32(inputBuffer, (int)(inputBufferReadIndex));
                logger.Error("- " + sampleCounter);

                // read the values
                double[] values = new double[inputHeader.numColumns - 1];
                Buffer.BlockCopy(inputBuffer, (int)(inputBufferReadIndex + sizeof(uint)), values, 0, inputHeader.rowSize - (sizeof(uint)));

                // set the read index to the next row
                inputBufferReadIndex += inputHeader.rowSize;
                if (inputBufferReadIndex == inputBuffer.Length) inputBufferReadIndex = 0;

                // decrease the amount of rows in the buffer as this row will be processed (to make space for another row in the input buffer)
                numberOfRowsInBuffer--;

            }

        }

        private void updateInputBuffer() {

            bool doUpdate = false;

            // thread safety
            lock (lockInputReader) {
                lock (lockInputBuffer) {

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

                // read new data from file
                byte[] rowData = null;
                long rowsRead = inputReader.readNextRows(inputBufferRowsPerRead, out rowData);
                if (rowsRead == -1) {
                    // error while reading (end of file is also possible, but is checked before using 'reachedEnd')

                    // message
                    logger.Error("Error while updating buffer, reading inputrows from file failed");

                } else {
                    // successfully retrieved new input rows

                    // thread safety
                    lock (lockInputBuffer) {


                        uint sampleCounter = BitConverter.ToUInt32(rowData, 0);
                        logger.Error("               - " + sampleCounter + " - " + rowsRead);


                        // check if the data wraps around
                        if (inputBufferAddIndex + rowData.Length > inputBuffer.Length) {
                            // wraps around

                            logger.Error("wraps around");



                            //Buffer.BlockCopy(rowData, 0, inputBuffer, (int)inputBufferAddIndex, (int)(inputBuffer.Length - inputBufferAddIndex));
                            //Buffer.BlockCopy(rowData, (int)(inputBuffer.Length - inputBufferAddIndex), inputBuffer, 0, (int)(rowData.Length - (inputBuffer.Length - inputBufferAddIndex)));

                            //Array.Copy(mData, mCursor, retArr, 0, mData.Count() - mCursor);
                            //Array.Copy(mData, 0, retArr, mData.Count() - mCursor, mCursor);

                        } else {
                            // does not wrap around

                            logger.Error("no wraps around");

                            // copy new data into the input buffer
                            //Buffer.BlockCopy(rowData, 0, inputBuffer, 0, rowData.Length);

                        }


                        
                        // update input buffer variables
                        inputBufferAddIndex += rowsRead * inputHeader.rowSize;
                        if (inputBufferAddIndex > inputBuffer.Length) inputBufferAddIndex = inputBufferAddIndex - inputBuffer.Length;
                        numberOfRowsInBuffer += rowsRead;
                        

                    }

                }

            }

        }

        /**
	     * Stop
	     */
        public void stop() {

            // lock for thread safety
            lock(lockStarted) {

                // check if the source is generating signals
                if (started) {

                    // stop playback
                    started = false;

                    // thread safety
                    lock (lockInputBuffer) {

                        // clear the input buffer and input buffer variables
                        inputBuffer = null;
                        numberOfRowsInBuffer = 0;
                        inputBufferReadIndex = 0;
                        inputBufferAddIndex = 0;

                    }

                }

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
            loopManualResetEvent.Set();

            // wait until the thread stopped
            // try to stop the main loop using running
            int waitCounter = 500;
            while (signalThread.IsAlive && waitCounter > 0) {
                Thread.Sleep(10);
                waitCounter--;
            }

            // clear the thread reference
            signalThread = null;

            // clear the reference to the mainthread
            main = null;

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
	     * Source running thread
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

                        /*
                        // set values for the generated sample
                        double[] sample = new double[outputChannels];
                        for (int i = 0; i < outputChannels; i++) {
                            //sample[i] = rand.NextDouble();
                            sample[i] = rand.Next(0,10) + 100;
                        }
                        
                        // pass the sample
                        main.eventNewSample(sample);
                        */

                        
                        
			        }

                }
                /*
			    // if still running then wait to allow other processes
			    if (running && sampleInterval != -1) {

                    // use the exact time that has passed since the last run to calculate the time to wait to get the exact sample interval
                    swTimePassed.Stop();
                    threadLoopDelay = sampleInterval - (int)swTimePassed.ElapsedMilliseconds;
                    
                    // wait for the remainder of the sample interval to get as close to the sample rate as possible (if there is a remainder)
                    if (threadLoopDelay >= 0) {

                        // reset the manual reset event, so it is sure to block on the next call to WaitOne
                        // 
                        // Note: not using AutoResetEvent because it could happen that .Set is called while not in WaitOne yet, when
                        // using AutoResetEvent this will cause it to skip the next WaitOne call
                        loopManualResetEvent.Reset();

                        // Sleep wait
                        loopManualResetEvent.WaitOne(threadLoopDelay);      // using WaitOne because this wait is interruptable (in contrast to sleep)

                    }

                    // start the timer to measure the loop time
                    swTimePassed.Reset();
                    swTimePassed.Start();

			    }
                */

                // reset the manual reset event, so it is sure to block on the next call to WaitOne
                // 
                // Note: not using AutoResetEvent because it could happen that .Set is called while not in WaitOne yet, when
                // using AutoResetEvent this will cause it to skip the next WaitOne call
                loopManualResetEvent.Reset();

                // Sleep wait
                loopManualResetEvent.WaitOne(2000);      // using WaitOne because this wait is interruptable (in contrast to sleep)

                //logger.Error("tonext");

            }

            // log message
            logger.Debug("Thread stopped");

        }



    }

}
