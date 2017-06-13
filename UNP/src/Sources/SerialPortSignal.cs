using NLog;
using System;
using System.Diagnostics;
using System.Threading;
using UNP.Core;
using UNP.Core.Helpers;
using UNP.Core.Params;


namespace UNP.Sources {

    public partial class SerialPortSignal : ISource {

        private static Logger logger = LogManager.GetLogger("SerialPortSignal");
        private static Parameters parameters = ParameterManager.GetParameters("SerialPortSignal", Parameters.ParamSetTypes.Source);

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

        public SerialPortSignal(MainThread main) {

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

            // start a new thread
            signalThread = new Thread(this.run);
            signalThread.Start();

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

            // retrieve the sample rate
            sampleRate = parameters.getValue<double>("SampleRate");
            if (sampleRate <= 0) {
                logger.Error("The sample rate cannot be 0 or lower");
                return false;
            }

            // calculate the sample interval
            sampleInterval = (int)Math.Floor(1000.0 / sampleRate);
            






    /*        
            char comname[5];

            // store the value of the needed parameters
            samplerate = Parameter( "SamplingRate" );
            comport = Parameter( "ComPort" );
            int protocol = Parameter("Protocol");
            mCount=0;

            strcpy(comname,"COM ");comname[3]=comport+'0';
	*/



            // flag as configured
            configured = true;

            // return success
            return true;

        }

        public void initialize() {
            Console.WriteLine("init dingen");

            // interrupt the loop wait and reset the wait lock (so it will wait again upon the next WaitOne call)
            // this will make sure the newly set sample rate interval is applied in the loop
            loopManualResetEvent.Set();
            loopManualResetEvent.Reset();

            // reset the state of the nexus packet
            packet.readstate = PACKET_START;

            // if a serial port object is still there, close the port first
            if (serialPort != null)     closeSerialPort();

            if (!openSerialPort("COM4", 5)) {
                logger.Error("Could not open Comport");
            }

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

            // close the serial connection
            closeSerialPort();

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

                        int numInChannels = 16;      // 16
                        int numSamples = 1;         // 1
                        read_channels(5);
                        for (int sample = 0; sample < numSamples; sample++) {
                            for (int channel = 0; channel < numInChannels; channel++) {

                                int value = packet.buffer[sample * numInChannels + channel];
                                /*
                                if (value > maxvalue) {
                                    value = maxvalue;
                                    bciout << "Value larger than " << maxvalue << endl;
                                }
                                if (value < minvalue) {
                                    value = minvalue;
                                    bciout << "Value smaller than " << minvalue << endl;
                                }
                                 */
                                /*
                                if ((value == 0) && (((protocol == 4) && (channel < 4)) || (protocol != 4)))
                                {
                                    // to avoid AR crashes 
                                        use value = rand() % 1024 ;
                                }
                                */
                                //signal(channel, sample) = (short)value;
                                logger.Warn("channel: " + channel + " - sample: " + sample + " = " + (short)value);

                            }
                        }



                        // set values for the generated sample
                        double[] retSample = new double[outputChannels];
                        for (int i = 0; i < outputChannels; i++) {
                            //sample[i] = rand.NextDouble();
                            retSample[i] = rand.Next(0, 10) + 100;
                        }

                        // pass the sample
                        main.eventNewSample(retSample);

			        }

                }

                // if still running then wait to allow other processes
			    if (running && sampleInterval != -1) {

                    // use the exact time that has passed since the last run to calculate the time to wait to get the exact sample interval
                    swTimePassed.Stop();
                    threadLoopDelay = sampleInterval - (int)swTimePassed.ElapsedMilliseconds;

                    // wait for the remainder of the sample interval to get as close to the sample rate as possible (if there is a remainder)
                    if (threadLoopDelay >= 0)
                        loopManualResetEvent.WaitOne(threadLoopDelay);      // using WaitOne because this wait is interruptable (in contrast to sleep)

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
