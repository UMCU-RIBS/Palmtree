using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Timers;
using UNP.Core;
using UNP.Core.DataIO;
using UNP.Core.Params;

namespace UNP.Plugins {

    public class WindowsSensorsPlugin : IPlugin {
        
        private const int CLASS_VERSION = 1;
       
        private static Logger logger = null;
        private static Parameters parameters = null;

        private string pluginName = "";
        private string pluginExt = "";
        private static int pluginId = -1;                                               // unique id used to identify plugin at data class (this number is generated and returned by the Data class upon registration of the plugin)

        private bool mLogDataStreams = false;                                           // stores whether the initial configuration has the logging of plugin streams enabled or disabled
        private string[] pluginStreamNames = new string[3]
            { "accelerationX", "accelerationY", "accelerationZ" };                      // 

        private bool sensorAvailable = false;                                           // flag to hold whether the Windows sensors functions can be used
        private Windows.Devices.Sensors.Accelerometer accelerometer;                    // hold Accelerometer (only do anything with this variable inside a try block (outside will cause an InvalidTypeException at startup of the project)
        private double[] acceleration = new double[3];
        private uint minReportInterval = 0;                                             // the minimum report interval of the device
        private uint reportInterval = 0;                                                // the report interval

        private Thread pluginThread = null;                                             // the thread
        private bool running = true;					                                // flag to define if the source thread should be running (setting to false will stop the source thread)
        private ManualResetEvent loopManualResetEvent = new ManualResetEvent(false);    // Manual reset event to call the WaitOne event on (this allows - in contrast to the sleep wait - to cancel the wait period at any point when closing the source) 

        private bool configured = false;
        private bool initialized = false;
        private bool started = false;				                                    // flag to define if the source is started or stopped
        private Object lockStarted = new Object();
        private double sampleRate = 0;                                                  // hold the amount of samples per second that the source outputs (used by the mainthead to convert seconds to number of samples)
        private int sampleIntervalMs = 0;                                               // interval between the samples in milliseconds

        public WindowsSensorsPlugin(string pluginName, string pluginExt) {

            // store the plugin name and extension
            this.pluginName = pluginName;
            this.pluginExt = pluginExt;

            // initialize the logger and parameters with the filter name
            logger = LogManager.GetLogger(pluginName);
            parameters = ParameterManager.GetParameters(pluginName, Parameters.ParamSetTypes.Plugin);

            // parameters
            parameters.addParameter<bool>(
                "LogDataStreams",
                "Log the plugin's data streams. See 'Data' tab for more settings on data stream logging.",
                "0");

            parameters.addParameter<double>(
                "LogSampleRate",
                "Rate with which plugin samples are logged, in samples per second (hz).\nNote, logging higher than 1000Hz not possible",
                "0", "", "10");

            // initialize Windows Driver Kit (or try to)
            try {
                init_safe();
            } catch (Exception) {

                // flag sensor as unavailabe
                sensorAvailable = false;

                //logger.Warn("Could not load Windows Driver Kit dependency, no sensory input");

            }

            // message
            logger.Info("Plugin created (version " + CLASS_VERSION + ")");

            // start a new thread
            pluginThread = new Thread(this.run);
            pluginThread.Name = "WindowsSensorsPlugin Run Thread";
            pluginThread.Start();

        
        }

        public int getClassVersion() {
            return CLASS_VERSION;
        }

        public string getName() {
            return pluginName;
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

        public bool configure() {           

            // check if the sensor is available
            if (sensorAvailable) {

                // message
                logger.Info("Windows Sensor available");
                logger.Info("Sensor minimum report interval: " + minReportInterval);
                logger.Info("Sensor report interval: " + reportInterval);

                // retrieve the sample rate
                sampleRate = parameters.getValue<double>("LogSampleRate");
                if (sampleRate <= 0) {
                    logger.Error("The sample rate cannot be 0 or lower");
                    return false;
                }

                // check if the samplerate is above 1000hz
                if (sampleRate > 1000) {

                    // message
                    logger.Warn("The sample rate is higher than 1000hz, capped at 1000Hz");

                    // set the sample rate to 1000
                    sampleRate = 1000;

                }

                // calculate the sample interval
                sampleIntervalMs = (int)Math.Floor(1000.0 / sampleRate);

                // retrieve and prepare the logging of plugin streams
                mLogDataStreams = parameters.getValue<bool>("LogDataStreams");
                if (mLogDataStreams) {

                    // register the streams
                    pluginId = Data.registerPluginInputStream(pluginName, pluginExt, pluginStreamNames, null);

                }

            } else {

                // message
                logger.Warn("No Windows Sensor was available, no sensory input");

            }

            // init global
            Globals.setValue<double>("AccelerationX", "0");
            Globals.setValue<double>("AccelerationY", "0");
            Globals.setValue<double>("AccelerationZ", "0");

            // flag as configured
            configured = true;

            // return success
            return true;

        }

        public void initialize() {

            // flag the initialization as complete
            initialized = true;

        }

        public void start() {

            // check if configured and the source was initialized
            if (!configured || !initialized) {
                return;
            }

            // check if the sensor is available
            if (sensorAvailable) {

                // lock for thread safety
                lock (lockStarted) {

                    // check if the plugin was not already started
                    if (started) return;

                    // start plugin
                    started = true;

                }

            }

            // interrupt the loop wait, allowing the loop to continue (in case it was waiting), causing an immediate start
            loopManualResetEvent.Set();

        }

        public void stop() {

            // lock for thread safety
            lock (lockStarted) {

                // check if the plugin is started
                if (started) {

                    // stop plugin
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
            lock (lockStarted) {

                return started;

            }

        }

        // execute before the filters
        public void preFiltersProcess() {

            // update the global values with the latest
            //Globals.setValue<double>("AccelerationX", acceleration[0].ToString());
            //Globals.setValue<double>("AccelerationY", acceleration[1].ToString());
            //Globals.setValue<double>("AccelerationZ", acceleration[2].ToString());

        }

        // execute after the filters
        public void postFiltersProcess() {

        }

        public void destroy() {

            // stop plugin
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
            while (pluginThread.IsAlive && waitCounter > 0) {
                Thread.Sleep(10);
                waitCounter--;
            }

            // clear the thread reference
            pluginThread = null;

        }


        [MethodImpl(MethodImplOptions.NoInlining)]
        public void init_safe() {

            // Init accelerometer 
            accelerometer = Windows.Devices.Sensors.Accelerometer.GetDefault();

            // set sampling frequency and set array to hold incoming data
            uint requestedReportInterval = 10;              // in ms

            // Check if accelerometer is found
            if (accelerometer != null) {

                // Retrieve the minimum interval supported by the device and compares it to a requested interval
                // If the minimum supported interval is greater than the requested interval, the code sets the 
                // value to the minimum. Otherwise, it sets the value to the requested interval.
                minReportInterval = accelerometer.MinimumReportInterval;
                reportInterval = minReportInterval > requestedReportInterval ? minReportInterval : requestedReportInterval;
                accelerometer.ReportInterval = reportInterval;

                // new meter values anonymous callback
                accelerometer.ReadingChanged += new Windows.Foundation.TypedEventHandler<Windows.Devices.Sensors.Accelerometer, Windows.Devices.Sensors.AccelerometerReadingChangedEventArgs>(delegate (Windows.Devices.Sensors.Accelerometer sender, Windows.Devices.Sensors.AccelerometerReadingChangedEventArgs e) {

                    // log the data
                    acceleration[0] = e.Reading.AccelerationX;
                    acceleration[1] = e.Reading.AccelerationY;
                    acceleration[2] = e.Reading.AccelerationZ;

                });

                // flag sensor as enabled
                sensorAvailable = true;

            } else {

                // flas sensor as disabled
                sensorAvailable = false;

            }

        }

        /**
	     * Source running thread
	     */
        private void run() {

            // name this thread
            if (Thread.CurrentThread.Name == null) {
                Thread.CurrentThread.Name = "Windows Sensor Plugin Thread";
            }

            // log message
            logger.Debug("Thread started");

            // loop while running
            while (running) {

                // lock for thread safety
                lock (lockStarted) {

                    // check if we are generating
                    if (started) {

                        // log the current values, if we set this to do so
                        if (mLogDataStreams) {
                            Data.logPluginDataValue(acceleration, pluginId);
                        }
                    }

                }

                // if still running then wait to allow other processes
                if (running) {

                    // reset the manual reset event, so it is sure to block on the next call to WaitOne
                    // 
                    // Note: not using AutoResetEvent because it could happen that .Set is called while not in WaitOne yet, when
                    // using AutoResetEvent this will cause it to skip the next WaitOne call
                    loopManualResetEvent.Reset();

                    // Sleep wait
                    loopManualResetEvent.WaitOne(sampleIntervalMs);      // using WaitOne because this wait is interruptable (in contrast to sleep)

                }

            }

        }   // end function


    }


}
