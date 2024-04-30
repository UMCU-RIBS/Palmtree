/**
 * GenerateSignal class
 * 
 * ...
 * 
 * 
 * Copyright (C) 2022:  RIBS group (Nick Ramsey Lab), University Medical Center Utrecht (The Netherlands) & external contributors
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
using System.Threading;
using Palmtree.Core;
using Palmtree.Core.Params;
using Palmtree.Core.DataIO;

namespace Palmtree.Sources {
    using ValueOrder = SamplePackageFormat.ValueOrder;

    /// <summary>
    /// The <c>GenerateSignal</c> class.
    /// 
    /// ...
    /// </summary>
    public class GenerateSignal : ISource {

        private const string CLASS_NAME = "GenerateSignal";
        private const int CLASS_VERSION = 2;

        private const int threadLoopDelayNoProc = 200;                                  // thread loop delay when not processing (1000ms / 5 run times per second = rest 200ms)

        private static Logger logger = LogManager.GetLogger(CLASS_NAME);
        private static Parameters parameters = ParameterManager.GetParameters(CLASS_NAME, Parameters.ParamSetTypes.Source);


        private Thread signalThread = null;                                             // the source thread
        private bool running = true;					                                // flag to define if the source thread should be running (setting to false will stop the source thread)
        private ManualResetEvent loopManualResetEvent = new ManualResetEvent(false);    // Manual reset event to call the WaitOne event on (this allows - in contrast to the sleep wait - to cancel the wait period at any point when closing the source) 
        private Stopwatch swTimePassed = new Stopwatch();                               // stopwatch object to give an exact amount to time passed inbetween loops
        private int samplePackageIntervalMs = 0;                                        // interval between the samples in milliseconds
        private long samplePackageIntervalTicks = 0;                                    // interval between the samples in ticks (for high precision timing)
        private int threadLoopDelay = 0;
        private long highPrecisionWaitTillTime = 0;                                     // stores the time until which the high precision timing waits before continueing
        private long sampleValueCounter = 0;                                            // counter that is used when generating samples that increase linearly

        private bool configured = false;
        private bool initialized = false;
        private bool started = false;				                                    // flag to define if the source is started or stopped
        private Object lockStarted = new Object();

        private Random rand = new Random(Guid.NewGuid().GetHashCode());
        private int outputChannels = 0;
        private double samplePackageRate = 0;                                           // the amount of packages per second that the source outputs
        private bool highPrecision = false;                                             // whether the generator should have high precision intervals
        private int samplesPerPackage = 0;                                              // the amount of samples per package that the source outputs
        private ValueOrder sampleValueOrder = ValueOrder.SampleMajor;                   // the value order/layout

        public GenerateSignal() {

            parameters.addParameter<int> (
                "Channels",
                "Number of source channels to generate",
                "1", "", "2");

            parameters.addParameter<double> (
                "PackageRate",
                "Rate with which sample-packages are generated, in packages per second (hz).\nNote: High precision will be enabled automatically when a sample-package rate is set to more than 1000 hz.",
                "0", "", "5");

            parameters.addParameter<bool>(
                "HighPrecision",
                "Use high precision intervals when generating sample-packages.\nNote 1: Running with this option enabled will put a large claim on the computing resources underlying the Palmtree OS-process, and\ntherefore - depending on the OS - could entirely claim a single one processor core. As a result, the pipeline signal\nprocessing might be slower or other processes on your system could slow down.\nNote 2: High precision will be enabled automatically when a sample-package rate is set to more than 1000 hz.",
                "", "", "0");
            
            parameters.addParameter<int> (
                "SamplesPerPackage",
                "Number of samples per package.",
                "1", "65535", "1");
            
            /*
            parameters.addParameter<int>(
                "ValueOrder",
                "The linear ordering (the layout) of the values in the package. Sample-major is recommended and orders the values first by samples (stores the sample-elements contiguously in memory) and second\nby channel (i.e. <smpl0 - ch0> - <smpl0 - ch1> ...). Channel-major orders the values first by channels (stores the channel-elements contiguously in memory) and second by sample (i.e. <smpl0 - ch0> - <smpl1 - ch0> ...)",
                "0", "1", "0", new string[] { "Channel-major", "Sample-major" });
            */

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
            return samplePackageRate * samplesPerPackage;

        }

        public bool configure(out SamplePackageFormat output) {

            // retrieve the number of output channels
            outputChannels = parameters.getValue<int>("Channels");
            if (outputChannels <= 0) {
                logger.Error("Number of output channels cannot be 0");
                output = null;
                return false;
            }
            
            // retrieve the sample-package rate
            samplePackageRate = parameters.getValue<double>("PackageRate");
            if (samplePackageRate <= 0) {
                logger.Error("The sample-package rate cannot be 0 or lower");
                output = null;
                return false;
            }
            
            // retrieve the high precision setting
            highPrecision = parameters.getValue<bool>("HighPrecision");

            // retrieve the number of output channels
            samplesPerPackage = parameters.getValue<int>("SamplesPerPackage");
            if (samplesPerPackage <= 0) {
                logger.Error("Number of samples per package cannot be 0");
                output = null;
                return false;
            }
            if (samplesPerPackage > 65535) {
                logger.Error("Number of samples per package cannot be higher than 65535");
                output = null;
                return false;
            }

            // retrieve the sample value order
            //sampleValueOrder = (parameters.getValue<int>("ValueOrder") == 0 ? ValueOrder.ChannelMajor : ValueOrder.SampleMajor);
            sampleValueOrder = ValueOrder.SampleMajor;

            // create a sampleformat
            output = new SamplePackageFormat(outputChannels, samplesPerPackage, samplePackageRate, sampleValueOrder);

            // calculate the sample-package interval
            samplePackageIntervalMs = (int)Math.Floor(1000.0 / samplePackageRate);

            // check if the sample-package rate is above 1000hz
            if (samplePackageRate > 1000) {

                // enable the high precision timing
                highPrecision = true;

                // message
                logger.Warn("Because the sample-package rate is larger than 1000hz, the high precision timer will be used");

            }

            // check if high precision timing is enabled
            if (highPrecision) {

                // calculate the sample-package interval for the high precision timing
                samplePackageIntervalTicks = (long)Math.Round(Stopwatch.Frequency * (1.0 / samplePackageRate));

                // message
                logger.Warn("High precision timer enabled. The majority of the Palmtree OS-process will be claimed by the source-module, this might have consequences for the pipeline performance and/or your system performance");

            }

            // TODO: debug, log as sourceinput
            //for (int i = 0; i < outputChannels; i++)
            //    Data.registerSourceInputStream(("Ch" + i), samplesPerPackage, samplePackageRate);

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
            logger.Debug("Number of output channels: " + outputChannels);
            logger.Debug("Sample package rate: " + samplePackageRate + "Hz");
            logger.Debug("Sample package interval (calculated): " + samplePackageIntervalMs + "ms");
            logger.Debug("High precision timing: " + (highPrecision ? "on" : "off"));
            logger.Debug("Number of samples per package: " + samplesPerPackage);

        }

        public bool initialize() {
            
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
            if (!initialized)	return;

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
            // (this is done because if the sample-package rate is low, we might have to wait for a long time for the thread to end)
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
	     * Note: 'running' just determines whether the source thread is running; start(), stop() and 
         * isStarted() manage whether samples are generated and forwarded into Palmtree
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

                        // debug/test: vary package size
                        //samplesPerPackage = rand.Next(0, 10) + 6;

                        // initialize an array
                        double[] samples = new double[outputChannels * samplesPerPackage];
                        
                        // generate values
                        if (sampleValueOrder == ValueOrder.SampleMajor) {
                            for (int iSmpl = 0; iSmpl < samplesPerPackage; iSmpl++) {
                                for (int iCh = 0; iCh < outputChannels; iCh++)
                                    samples[iSmpl * outputChannels + iCh] = rand.Next(0, 10) + 100;
                                /*
                                for (int iCh = 0; iCh < outputChannels; iCh++)
                                    samples[iSmpl * outputChannels + iCh] = sampleValueCounter;
                                sampleValueCounter++;
                                if (sampleValueCounter == long.MaxValue)  sampleValueCounter = 0;
                                */
                            }
                        } else {
                            for (int iSmpl = 0; iSmpl < samplesPerPackage; iSmpl++) {
                                for (int iCh = 0; iCh < outputChannels; iCh++)
                                    samples[iCh * samplesPerPackage + iSmpl] = rand.Next(0, 10) + 100;
                                /*
                                for (int iCh = 0; iCh < outputChannels; iCh++)
                                    samples[iCh * samplesPerPackage + iSmpl] = sampleValueCounter;
                                sampleValueCounter++;
                                if (sampleValueCounter == long.MaxValue)  sampleValueCounter = 0;
                                */
                            }
                        }
                        

                        // TODO: debug, still log as sourceinput
                        //Data.logSourceInputValues(samples);

                        // pass the sample
                        MainThread.eventNewSample(samples);
                        
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
                            highPrecisionWaitTillTime = Stopwatch.GetTimestamp() + samplePackageIntervalTicks;     // choose not to correct for elapsed ticks. This result in a slightly higher wait time, which at lower Hz comes closer to the expected samplecount per second
                            //highPrecisionWaitTillTime = Stopwatch.GetTimestamp() + samplePackageIntervalTicks - swTimePassed.ElapsedTicks;
                            while (Stopwatch.GetTimestamp() <= highPrecisionWaitTillTime) ;

                        } else {
                            // low precision timing

                            threadLoopDelay = samplePackageIntervalMs;     // choose not to correct for elapsed ms. This result in a slightly higher wait time, which at lower Hz comes closer to the expected samplecount per second
                            //threadLoopDelay = samplePackageIntervalMs - (int)swTimePassed.ElapsedMilliseconds;

                            // wait for the remainder of the sample-package interval to get as close to the sample-package rate as possible (if there is a remainder)
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
