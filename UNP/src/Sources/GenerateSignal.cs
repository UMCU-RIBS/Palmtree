using NLog;
using System;
using System.Diagnostics;
using System.Threading;
using UNP.Core;
using UNP.Core.DataIO;
using UNP.Core.Helpers;
using UNP.Core.Params;

namespace UNP.Sources {

    public class GenerateSignal : ISource {

        private const string CLASS_NAME = "GenerateSignal";
        private const int CLASS_VERSION = 1;

        private const int threadLoopDelayNoProc = 200;                                  // thread loop delay when not processing (1000ms / 5 run times per second = rest 200ms)

        private static Logger logger = LogManager.GetLogger(CLASS_NAME);
        private static Parameters parameters = ParameterManager.GetParameters(CLASS_NAME, Parameters.ParamSetTypes.Source);


        private Thread signalThread = null;                                             // the source thread
        private bool running = true;					                                // flag to define if the source thread should be running (setting to false will stop the source thread)
        private ManualResetEvent loopManualResetEvent = new ManualResetEvent(false);    // Manual reset event to call the WaitOne event on (this allows - in contrast to the sleep wait - to cancel the wait period at any point when closing the source) 
        private Stopwatch swTimePassed = new Stopwatch();                               // stopwatch object to give an exact amount to time passed inbetween loops
        private int sampleIntervalMs = 0;                                               // interval between the samples in milliseconds
        private long sampleIntervalTicks = 0;                                           // interval between the samples in ticks (for high precision timing)
        private int threadLoopDelay = 0;
        private long highPrecisionWaitTillTime = 0;                                     // stores the time until which the high precision timing waits before continueing

        private bool configured = false;
        private bool initialized = false;
        private bool started = false;				                                    // flag to define if the source is started or stopped
        private Object lockStarted = new Object();

        private Random rand = new Random(Guid.NewGuid().GetHashCode());
        private int outputChannels = 0;
        private double sampleRate = 0;                                                  // hold the amount of samples per second that the source outputs (used by the mainthead to convert seconds to number of samples)
        private bool highPrecision = false;                                             // hold whether the generator should have high precision intervals
        

        public GenerateSignal() {

            parameters.addParameter<int> (
                "Channels",
                "Number of source channels to generate",
                "1", "", "1");

            parameters.addParameter<double> (
                "SampleRate",
                "Rate with which samples are generated, in samples per second (hz).\nNote: High precision will be enabled automatically when a sample rate is set to more than 1000 hz.",
                "0", "", "5");

            parameters.addParameter<bool>(
                "HighPrecision",
                "Use high precision intervals when generating sample.\nNote 1: Enabling this option will claim one processor core entirely, possibly causing your system to slow down or hang.\nNote 2: High precision will be enabled automatically when a sample rate is set to more than 1000 hz.",
                "", "", "0");

            // message
            logger.Info("Source created (version " + CLASS_VERSION + ")");

            // start a new thread
            signalThread = new Thread(this.run);
            signalThread.Name = "GenerateSignal Run Thread";
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

        public bool configure(out PackageFormat output) {

            // retrieve the number of output channels
            outputChannels = parameters.getValue<int>("Channels");
            if (outputChannels <= 0) {
                logger.Error("Number of output channels cannot be 0");
                output = null;
                return false;
            }
            
            // retrieve the sample rate
            sampleRate = parameters.getValue<double>("SampleRate");
            if (sampleRate <= 0) {
                logger.Error("The sample rate cannot be 0 or lower");
                output = null;
                return false;
            }
            
            // retrieve the high precision setting
            highPrecision = parameters.getValue<bool>("HighPrecision");

            // create a sampleformat
            output = new PackageFormat(outputChannels, 1, sampleRate);      // since the number of samples is 1 per package, the given samplerate is the packagerate)

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

            // TODO: debug, even sourceinput dingen; tijdelijk, dit hoort niet in generateSignal klasse
            PackageFormat generateSignalSampleFormat = new PackageFormat(outputChannels, 1, sampleRate);
            for (int i = 0; i < outputChannels; i++) {
                Data.registerSourceInputStream(("Ch" + i), generateSignalSampleFormat);
            }

            // flag as configured
            configured = true;

            // return success
            return true;

        }

        public void initialize() {
            
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
                
                // start generating
                started = true;

            }

            // interrupt the loop wait, allowing the loop to continue (in case it was waiting the noproc interval)
            // causing an immediate start and switching to the processing waittime
            loopManualResetEvent.Set();

        }

	    /**
	     * Stop
	     */
	    public void stop() {

            // if not initialized than nothing needs to be stopped
            if (!initialized) return;

            // lock for thread safety
            lock (lockStarted) {

                // check if the source is generating signals
                if (started) {
                    
                    // stop generating
                    started = false;

                }

            }

            // interrupt the loop wait, allowing the loop to continue (in case it was waiting the proc interval)
            // switching to the no-processing waittime
            loopManualResetEvent.Set();

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
            while (running) {

                // lock for thread safety
                lock (lockStarted) {

			        // check if we are generating
			        if (started) {

                        // set values for the generated sample
                        double[] sample = new double[outputChannels];
                        for (int i = 0; i < outputChannels; i++) {
                            //sample[i] = rand.NextDouble();
                            sample[i] = rand.Next(0, 10) + 100;
                        }

                        // TODO: debug, even sourceinput dingen
                        Data.logSourceInputValues(sample);

                        // pass the sample
                        MainThread.eventNewSample(sample);
                        
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
                                loopManualResetEvent.Reset();

                                // Sleep wait
                                loopManualResetEvent.WaitOne(threadLoopDelay);      // using WaitOne because this wait is interruptable (in contrast to sleep)
                                
                            }

                        }

                    } else {
                            
                        // reset the manual reset event, so it is sure to block on the next call to WaitOne
                        // 
                        // Note: not using AutoResetEvent because it could happen that .Set is called while not in WaitOne yet, when
                        // using AutoResetEvent this will cause it to skip the next WaitOne call
                        loopManualResetEvent.Reset();

                        // Sleep wait
                        loopManualResetEvent.WaitOne(threadLoopDelayNoProc);      // using WaitOne because this wait is interruptable (in contrast to sleep)

                    }

                    // restart the timer to measure the loop time
                    swTimePassed.Restart();

                }

            }

            // log message
            logger.Debug("Thread stopped");

        }


    }

}
