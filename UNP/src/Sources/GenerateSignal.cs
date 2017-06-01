using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using UNP.Core;
using UNP.Core.Helpers;
using UNP.Core.Params;

namespace UNP.Sources {

    public class GenerateSignal : ISource {

        private static Logger logger = LogManager.GetLogger("GenerateSignal");
        private static Parameters parameters = ParameterManager.GetParameters("GenerateSignal", Parameters.ParamSetTypes.Source);

        private MainThread pipeline = null;

        Stopwatch swTimePassed = new Stopwatch();                           // stopwatch object to give an exact amount to time passed inbetween loops
        private int sampleInterval = 200;                                   // interval between the samples in milliseconds
        int threadLoopDelay = 0;

        private bool running = true;					                    // flag to define if the source thread is still running (setting to false will stop the source thread)
        private bool configured = false;
        private bool initialized = false;

        private bool started = false;				                        // flag to define if the source is started or stopped
        private Object lockStarted = new Object();

        Random rand = new Random(Guid.NewGuid().GetHashCode());
        private int outputChannels = 0;
        private double sampleRate = 0;                                      // hold the amount of samples per second that the source outputs (used by the mainthead to convert seconds to number of samples)

	    public GenerateSignal(MainThread pipeline) {

            // set the reference to the pipeline
            this.pipeline = pipeline;

            parameters.addParameter<int> (
                "Channels",
                "Number of source channels to generate",
                "1", "", "1");

            parameters.addParameter<double> (
                "SampleRate",
                "Rate with which samples are generated, in samples per second (hz)",
                "0", "", "5");

            // start a new thread
            Thread thread = new Thread(this.run);
            thread.Start();

        }

        public Parameters getParameters() {
            return parameters;
        }

        public bool configure(out SampleFormat output) {
            configured = true;

            // retrieve the number of output channels
            outputChannels = parameters.getValue<int>("Channels");
            
            // create a sampleformat
            output = new SampleFormat((uint)outputChannels);

            // check if the number of output channels is higher than 0
            if (outputChannels <= 0) {
                logger.Error("Number of output channels cannot be 0");
                return false;
            }

            // retrieve the sample rate
            sampleRate = parameters.getValue<double>("SampleRate");
            if (sampleRate <= 0) {
                logger.Error("The sample rate cannot be 0 or lower");
                return false;
            }

            // calculate the sample interval
            sampleInterval = (int)Math.Floor(1000.0 / sampleRate);
            
            // return success
            return true;

        }

        public void initialize() {
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
		
            // stop generating (stop will check if it was running in the first place)
		    stop();
		
		    // stop the thread from running
		    running = false;
		
		    // allow the source thread to stop
            Thread.Sleep(100);
		
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

            // set an initial start for the stopwatche
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
                        pipeline.eventNewSample(sample);

			        }

                }


                // 
			    // if still running then sleep to allow other processes
			    if (running && sampleInterval != -1) {

                    // calculate the exact time that has passed since the last run
                    swTimePassed.Stop();
                    int timePassed = (int)swTimePassed.ElapsedMilliseconds;

                    // calculate the time to wait to get the exact sample interval
                    threadLoopDelay = sampleInterval - timePassed;

                    // sleep for the remainder of the sample interval to get as close to the sample rate as possible (if there is a remainder)
                    if (threadLoopDelay >= 0) Thread.Sleep(threadLoopDelay);

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
