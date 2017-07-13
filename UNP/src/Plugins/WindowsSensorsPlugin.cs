using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using UNP.Core.Params;

namespace UNP.Plugins {

    public class WindowsSensorsPlugin : IPlugin {
        
        private const int CLASS_VERSION = 0;

        private static Logger logger = null;
        private static Parameters parameters = null;

        protected string pluginName = "";

        private bool sensorEnabled = false;                                 // flag to hold whether the Windows sensors functions can be used
        private Windows.Devices.Sensors.Accelerometer accelerometer;        // hold Accelerometer (only do anything with this variable inside a try block (outside will cause an InvalidTypeException at startup of the project)
        private double accelerationX = 0;
        private double accelerationY = 0;
        private double accelerationZ = 0;

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
            return pluginName;
        }

        public Parameters getParameters() {
            return parameters;
        }

        public int getClassVersion() {
            return CLASS_VERSION;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void init_safe() {

            // Init vars, accelerometer and logging frequency
            accelerometer = Windows.Devices.Sensors.Accelerometer.GetDefault();
            
            uint requestedReportInterval = 10;

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

                    accelerationX = e.Reading.AccelerationX;
                    accelerationY = e.Reading.AccelerationY;
                    accelerationZ = e.Reading.AccelerationZ;

                });
                
                // flag sensor as enabled
                sensorEnabled = true;

            } else {

                // message
                logger.Warn("No acceletometer was found, no sensory input");
                
            }
            
        }


        public bool configure() {
            return true;
        }
        public void initialize() {

        }

        public void start() {

        }
        public void stop() {

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
