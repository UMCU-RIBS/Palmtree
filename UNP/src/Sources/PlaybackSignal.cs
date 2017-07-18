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
using UNP.Core.Helpers;
using UNP.Core.Params;

namespace UNP.Sources {

    public class PlaybackSignal : ISource {

        private const string CLASS_NAME = "PlaybackSignal";
        private const int CLASS_VERSION = 0;

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
        private bool started = false;				                        // flag to define if the source is started or stopped
        private Object lockStarted = new Object();

        private Random rand = new Random(Guid.NewGuid().GetHashCode());
        private int outputChannels = 0;
        private double sampleRate = 0;                                      // hold the amount of samples per second that the source outputs (used by the mainthead to convert seconds to number of samples)

        private string inputFile = "";                                      // filepath to the file(s) to use for playback

        public PlaybackSignal(MainThread main) {

            // set the reference to the main
            this.main = main;
            
            parameters.addParameter<string>(
                "Input",
                "The data input file(s) that should be used for playback.\nWhich file of a set is irrelevant as long as the set has the same filename (the file extension is ignored as multiple files might be used).",
                "", "", "");

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

        public bool configure(out SampleFormat output) {
            
            
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
                output = new SampleFormat((uint)0);
                return false;

            }
            
            
            // read the data header
            DataHeader header = DataCommon.readHeader(inputDatFile);
            if (header == null) {

                // message
                logger.Error("Could not read header data from input .dat file '" + inputDatFile + "'");

                // return
                output = new SampleFormat((uint)0);
                return false;

            }


            // check if the number of pipeline input streams in the .dat is higher than 0
            if (header.pipelineInputStreams <= 0) {

                // message
                logger.Error("The input .dat file has no pipeline input streams, these are required for playback, make sure the LogPipelineInputStream setting (data tab) is switched on while recording data for replay");

                // return
                output = new SampleFormat((uint)0);
                return false;

            }

            // set the number of output channels for this source based on the .dat file
            outputChannels = header.pipelineInputStreams;
            
            // create a sampleformat
            output = new SampleFormat((uint)outputChannels);

            // debug, TODO: set temp
            sampleRate = 5;
            sampleInterval = (int)Math.Floor(1000.0 / sampleRate);

            // write some playback information
            logger.Info("Playback data file: " + inputDatFile);
            logger.Info("Data file version: " + header.version);
            logger.Info("Number of pipeline input streams / output channels: " + outputChannels);


            // flag as configured
            configured = true;

            // return success
            return true;

        }

        public void initialize() {

            // interrupt the loop wait. The loop will reset the wait lock (so it will wait again upon the next WaitOne call)
            // this will make sure the newly set sample rate interval is applied in the loop
            loopManualResetEvent.Set();
            
            // flag the initialization as complete
            initialized = true;

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
                
                // start generating
                started = true;

                // interrupt the loop wait, making the loop to continue (in case it was waiting the sample interval)
                // causing an immediate start, this makes it feel more responsive
                loopManualResetEvent.Set();

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

                    // message
                    //logger.Info("Collection stopped for '" + collectionName + "'");

                    // stop generating
                    started = false;

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

                        // set values for the generated sample
                        double[] sample = new double[outputChannels];
                        for (int i = 0; i < outputChannels; i++) {
                            //sample[i] = rand.NextDouble();
                            sample[i] = rand.Next(0,10) + 100;
                        }
                        
                        // pass the sample
                        main.eventNewSample(sample);

			        }

                }
                
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
                
		    }

            // log message
            logger.Debug("Thread stopped");

        }



    }

}
