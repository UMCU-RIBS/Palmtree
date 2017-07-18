using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Timers;
using UNP.Core.Params;

namespace UNP.Plugins {

    public class WindowsSensorsPlugin : IPlugin {
        
        private const int CLASS_VERSION = 0;

        private static Logger logger = null;
        private static Parameters parameters = null;

        protected string pluginName = "";
        private int pluginId = -1;                                          // id used to identify plugin at data class

        private bool sensorEnabled = false;                                 // flag to hold whether the Windows sensors functions can be used
        private bool logData = false;                                       // flag to hold whether data should be sent to the data class, ie be logged

        private Windows.Devices.Sensors.Accelerometer accelerometer;        // hold Accelerometer (only do anything with this variable inside a try block (outside will cause an InvalidTypeException at startup of the project)
        private double[] logAcceleration = new double[3];

        Timer debugTimer = null;                                            // debug purposes: allows use of timer to test in absence of sensor input

        public WindowsSensorsPlugin(string pluginName) {

            // store the plugin name
            this.pluginName = pluginName;

            // initialize the logger and parameters with the filter name
            logger = LogManager.GetLogger(pluginName);
            parameters = ParameterManager.GetParameters(pluginName, Parameters.ParamSetTypes.Plugin);

            try {
                init_safe();
            } catch (Exception) {
                logger.Warn("Could not load Windows Driver Kit dependency, no sensory input");
            }

        }

        public string getName() {
            return this.pluginName;
        }

        public Parameters getParameters() {
            return parameters;
        }

        public int getClassVersion() {
            return CLASS_VERSION;
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
                uint minReportInterval = accelerometer.MinimumReportInterval;
                uint reportInterval = minReportInterval > requestedReportInterval ? minReportInterval : requestedReportInterval;
                accelerometer.ReportInterval = reportInterval;
                
                logger.Warn("ReportInterval: " + accelerometer.ReportInterval);

                // new meter values anonymous callback
                accelerometer.ReadingChanged += new Windows.Foundation.TypedEventHandler<Windows.Devices.Sensors.Accelerometer, Windows.Devices.Sensors.AccelerometerReadingChangedEventArgs>(delegate (Windows.Devices.Sensors.Accelerometer sender, Windows.Devices.Sensors.AccelerometerReadingChangedEventArgs e) {

                    logAcceleration[0] = e.Reading.AccelerationX;
                    logAcceleration[1] = e.Reading.AccelerationY;
                    logAcceleration[2] = e.Reading.AccelerationZ;

                    // send to data class, if flag is set
                    if(this.logData) Core.Data.LogPluginDataValue(logAcceleration, pluginId);
                });
                
                // flag sensor as enabled
                sensorEnabled = true;

            } else {

                // message
                logger.Warn("No acceletometer was found, no sensory input");
                
            }
            
        }

        public bool configure() {

            // register streams
            string[] streamNames = new string[3] { "accelerationX", "accelerationY", "accelerationZ" };
            this.pluginId = Core.Data.RegisterPluginInputStream(pluginName, streamNames, null);

            return true;
        }

        public void initialize() {

        }

        public void start() {

            // debug, create timer to generate data 
            debugTimer = new Timer(1000);
            debugTimer.Elapsed += OnTimedEvent;
            debugTimer.AutoReset = true;
            debugTimer.Enabled = true;

            // set flag to start sending data
            logData = true;
        }

        // debug, send data based on timer
        private void OnTimedEvent(Object source, ElapsedEventArgs e) {

            double[] logAcceleration = new double[3];
            Random rnd = new Random();

            logAcceleration[0] = rnd.Next(1, 200);
            logAcceleration[1] = rnd.Next(1, 200);
            logAcceleration[2] = rnd.Next(1, 200);

            logger.Info("Data [x,y,z]: " + logAcceleration[0] + "," + logAcceleration[1] + "," + logAcceleration[2]);
            Core.Data.LogPluginDataValue(logAcceleration, this.pluginId);        
        }

        public void stop() {

            // debug
            debugTimer.Enabled = false;

            // stop sending data
            logData = false;
        }

        public bool isStarted() {
            return true;
        }

        // execute before the filters
        public void preFiltersProcess() {
            //logger.Warn("accelerationX " + accelerationX);
        }

        // execute after the filters
        public void postFiltersProcess() {

        }

        public void destroy() {

        }

    }

}
