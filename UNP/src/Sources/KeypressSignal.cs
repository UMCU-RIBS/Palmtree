using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using UNP.Core;
using UNP.Core.Helpers;
using UNP.Core.Params;

namespace UNP.Sources {

    public class KeypressSignal : ISource {

        private const string CLASS_NAME = "KeypressSignal";
        private const int CLASS_VERSION = 0;

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);

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

        Random rand = new Random(Guid.NewGuid().GetHashCode());
        private int outputChannels = 0;
        private double sampleRate = 0;                                      // hold the amount of samples per second that the source outputs (used by the mainthead to convert seconds to number of samples)

        private Keys[] mConfigKeys = null;
        private int[] mConfigOutputChannels = null;
        private double[] mConfigPressed = null;
        private double[] mConfigNotPressed = null;

	    public KeypressSignal(MainThread main) {

            // set the reference to the main
            this.main = main;

            parameters.addParameter<int> (
                "Channels",
                "Number of source channels to generate",
                "1", "", "1");

            parameters.addParameter<double> (
                "SampleRate",
                "Rate with which samples are generated, in samples per second (hz)",
                "0", "", "5");

            parameters.addParameter<string[][]>(
                "Keys",
                "Specifies which key influence which output channels and what values they give\n\nKey: Key to check for (takes a single character a-z or 0-9)\nOutput: output channel (1...n)\nPressed: value to output on the channel when the given key is pressed\nNot-pressed: value to output on the channel when the given key is not pressed",
                "", "", "F;1;1;-1", new string[] { "Key", "Output", "Pressed", "Not-pressed" });

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

            // retrieve the number of output channels
            outputChannels = parameters.getValue<int>("Channels");

            // create a sampleformat
            output = new SampleFormat((uint)outputChannels);

            // check if the number of output channels is higher than 0
            if (outputChannels <= 0) {
                logger.Error("Number of output channels cannot be 0");
                return false;
            }

            // notify data class on the number of output channels
            Data.SetSourceOutputChannels(outputChannels);

            // retrieve the sample rate
            sampleRate = parameters.getValue<double>("SampleRate");
            if (sampleRate <= 0) {
                logger.Error("The sample rate cannot be 0 or lower");
                return false;
            }

            // calculate the sample interval
            sampleInterval = (int)Math.Floor(1000.0 / sampleRate);

            // retrieve key settings
            string[][] keys = parameters.getValue<string[][]>("Keys");
            if (keys.Length != 0 && keys.Length != 4) {
                logger.Error("Keys parameter must have 4 columns (Key, Output channel, Pressed, Not-pressed)");
                return false;
            }

            // resize the variables
            mConfigKeys = new Keys[keys[0].Length];                    // char converted to virtual key
            mConfigOutputChannels = new int[keys[0].Length];          // for the values stored in this array: value 0 = channel 1
            mConfigPressed = new double[keys[0].Length];
            mConfigNotPressed = new double[keys[0].Length];

            // loop through the rows
            for (int row = 0; row < keys[0].Length; ++row ) {
                
                // try to convert the key character to an int
                string key = keys[0][row].ToUpper();
                if (key.Length != 1 || !(new Regex(@"^[A-Z0-9]*$")).IsMatch(key)) {
                    logger.Error("The key value '" + key + "' is not a valid key (should be a single character, a-z or 0-9)");
                    return false;
                }
                mConfigKeys[row] = (Keys)key[0];     // capital characters A-Z and numbers can be directly saved as int (directly used/emulated as virtual keys)

                // try to parse the channel number
                int channel = 0;
                if (!int.TryParse(keys[1][row], out channel)) {
                    logger.Error("The value '" + keys[1][row] + "' is not a valid output channel value (should be a positive integer)");
                    return false;
                }
                if (channel < 1) {
                    logger.Error("Output channels must be positive integers");
                    return false;
                }
                if (channel > outputChannels) {
                    logger.Error("The output channel value '" + keys[1][row] + "' exceeds the number of channels coming out of the filter (#outputChannels: " + outputChannels + ")");
                    return false;
                }
                mConfigOutputChannels[row] = channel - 1;   // -1 since the user input the channel 1-based and we use a 0-based array

                // try to parse the pressed value
                double doubleValue = 0;
                if (!double.TryParse(keys[2][row], NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, Parameters.NumberCulture, out doubleValue)) {
                    logger.Error("The value '" + keys[2][row] + "' is not a valid double value");
                    return false;
                }
                mConfigPressed[row] = doubleValue;

                // try to parse the not-pressed value
                doubleValue = 0;
                if (!double.TryParse(keys[3][row], NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, Parameters.NumberCulture, out doubleValue)) {
                    logger.Error("The value '" + keys[3][row] + "' is not a valid double value");
                    return false;
                }
                mConfigNotPressed[row] = doubleValue;

            }

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

            // set an initial start for the stopwatche
            swTimePassed.Start();

		    // loop while running
		    while(running) {

                // lock for thread safety
                lock(lockStarted) {

			        // check if we are generating
			        if (started) {

                        // set (0) values for the generated sample
                        double[] sample = new double[outputChannels];

                        // loop through the keys
                        for (int i = 0; i < mConfigKeys.Length; i++) {

                            // check if the key is pressed (without any other special keys like shift, control etc)
                            bool pressed = (0 != (GetAsyncKeyState((int)mConfigKeys[i]) & 0x8000));
                            
                            // set the sample value accordingly
                            sample[mConfigOutputChannels[i]] = (pressed ? mConfigPressed[i] : mConfigNotPressed[i]);

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
