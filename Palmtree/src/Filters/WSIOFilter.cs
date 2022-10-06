/**
 * WSIOFilter class
 * 
 * ...
 * 
 * 
 * Copyright (C) 2022:  Crone Lab, John Hopkins (Baltimore, USA) & external contributors
 * Author(s):           Christopher Coogan          (ccoogan2@jhmi.edu)
 * 
 * This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software
 * Foundation, either version 3 of the License, or (at your option) any later version. This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for
 * more details. You should have received a copy of the GNU General Public License along with this program. If not, see <https://www.gnu.org/licenses/>.
 */
using NLog;
using Palmtree.Core;
using Palmtree.Core.Params;
using Palmtree.Core.DataIO;
using WebSocketSharp.Server;
using System;
using System.Linq;

namespace Palmtree.Filters {

    /// <summary>
    /// WSIOFilter class
    /// 
    /// ...
    /// </summary>
    public class WSIOFilter : FilterBase, IFilter {
        public static WebSocketServer wssv_data;
        public static WebSocketServer wssv_event;

        private new const int CLASS_VERSION = 1;
        
        public WSIOFilter(string filterName) {

            // set class version
            base.CLASS_VERSION = CLASS_VERSION;

            // store the filter name
            this.filterName = filterName;

            // initialize the logger and parameters with the filter name
            logger = LogManager.GetLogger(filterName);
            parameters = ParameterManager.GetParameters(filterName, Parameters.ParamSetTypes.Filter);

            // define the parameters
            parameters.addParameter<bool>(
                "EnableFilter",
                "Enable WSIO Filter",
                "1");

            parameters.addParameter<bool>(
                "LogDataStreams",
                "Log the filter's intermediate and output data streams. See 'Data' tab for more settings on sample stream logging.",
                "0");

            parameters.addParameter<double>(
                "Data_WebsocketPort",
                "Port to send data out onto",
                "21111");
            parameters.addParameter<double>(
                "Event_WebsocketPort",
                "Port to send data out onto",
                "21112");

            Data.eventLogged += Data_onEventLogged;

        }

        /**
         * Configure the filter. Checks the values and application logic of the
         * parameters and, if valid, transfers the configuration parameters to local variables
         * (initialization of the filter is done later by the initialize function)
         **/
        public bool configure(ref SamplePackageFormat input, out SamplePackageFormat output) {

            // check sample-major ordered input
            if (input.valueOrder != SamplePackageFormat.ValueOrder.SampleMajor) {
                logger.Error("This filter is designed to work only with sample-major ordered input");
                output = null;
                return false;
            }

            // retrieve the number of input channels
            if (input.numChannels <= 0) {
                logger.Error("Number of input channels cannot be 0");
                output = null;
                return false;
            }

            // the output package will be in the same format as the input package
            output = new SamplePackageFormat(input.numChannels, input.numSamples, input.packageRate, input.valueOrder);

            // store a references to the input and output format
            inputFormat = input;
            outputFormat = output;

            // check the values and application logic of the parameters
            if (!checkParameters(parameters))   return false;

            // transfer the parameters to local variables
            transferParameters(parameters);

            // configure output logging for this filter
            configureOutputLogging(filterName + "_", output);

            // print configuration
            printLocalConfiguration();

            // return success
            return true;

        }

        /// <summary>Re-configure and/or reset the configration parameters of the filter (defined in the newParameters argument) on-the-fly.</summary>
        /// <param name="newParameters">Parameter object that defines the configuration parameters to be set. Set to NULL to leave the configuration parameters untouched.</param>
        /// <param name="resetOption">Filter reset options. 0 will reset the minimum; 1 will perform a complete reset of filter information. > 1 for custom resets.</param>
        /// <returns>A boolean, either true for a succesfull re-configuration, or false upon failure</returns>
        public bool configureRunningFilter(Parameters newParameters, int resetOption) {

            // check if new parameters are given (only a reset is also an option)
            if (newParameters != null) {

                //
                // no pre-check on the number of output channels is needed here, the number of output
                // channels will remain the some regardless to the filter being enabled or disabled
                // 

                // check the values and application logic of the parameters
                if (!checkParameters(newParameters)) return false;

                // retrieve and check the LogDataStreams parameter
                bool newLogDataStreams = newParameters.getValue<bool>("LogDataStreams");
                if (!mLogDataStreams && newLogDataStreams) {
                    // logging was (in the initial configuration) switched off and is trying to be switched on
                    // (refuse, it cannot be switched on, because sample streams have to be registered during the first configuration)

                    // message
                    logger.Error("Cannot switch the logging of data streams on because it was initially switched off (and streams need to be registered during the first configuration, logging is refused");

                    // return failure
                    return false;

                }

                // transfer the parameters to local variables
                transferParameters(newParameters);

                // apply change in the logging of sample streams
                if (mLogDataStreams && mLogDataStreamsRuntime && !newLogDataStreams) {
                    // logging was (in the initial configuration) switched on and is currently on but wants to be switched off (resulting in 0's being output)

                    // message
                    logger.Debug("Logging of data streams was switched on but is now switched off, only zeros will be logged");

                    // switch logging off (to zeros)
                    mLogDataStreamsRuntime = false;

                } else if (mLogDataStreams && !mLogDataStreamsRuntime && newLogDataStreams) {
                    // logging was (in the initial configuration) switched on and is currently off but wants to be switched on (resume logging)

                    // message
                    logger.Debug("Logging of data streams was switched off but is now switched on, logging is resumed");

                    // switch logging on
                    mLogDataStreamsRuntime = true;

                }

                // print configuration
                printLocalConfiguration();

            }

            // TODO: take resetFilter into account (currently always resets the buffers on initialize
            //          but when set not to reset, the buffers should be resized while retaining their values!)


            // initialize the variables
            initialize();

            // return success
            return true;

        }


        /**
         * check the values and application logic of the given parameter set
         **/
        private bool checkParameters(Parameters newParameters) {


            // TODO: parameters.checkminimum, checkmaximum

            // filter is enabled/disabled
            bool newEnableFilter = newParameters.getValue<bool>("EnableFilter");

            // check if the filter is enabled
            if (newEnableFilter) {


            }

            // return success
            return true;

        }

        /**
         * transfer the given parameter set to local variables
         **/
        private void transferParameters(Parameters newParameters) {

            // filter is enabled/disabled
            mEnableFilter = newParameters.getValue<bool>("EnableFilter");

            // check if the filter is enabled
            if (mEnableFilter) {

            }

        }

        private void printLocalConfiguration() {

            // debug output
            logger.Debug("--- Filter configuration: " + filterName + " ---");
            logger.Debug("Input channels: " + inputFormat.numChannels);
            logger.Debug("Enabled: " + mEnableFilter);
            logger.Debug("Output channels: " + outputFormat.numChannels);
            if (mEnableFilter) {

            }

        }

        public bool initialize() {

            // check if the filter is enabled
            if (mEnableFilter) {
                double wsPort_data = parameters.getValue<double>("Data_WebsocketPort");
                double wsPort_event = parameters.getValue<double>("Event_WebsocketPort");

                wssv_data = new WebSocketServer("ws://localhost:" + wsPort_data);
                wssv_event = new WebSocketServer("ws://localhost:" + wsPort_event);

                wssv_data.AddWebSocketService<WSIO>("/");
                wssv_event.AddWebSocketService<WSIO>("/");
            }
            return true;

        }

        public void start() {
            wssv_data.Start();
            wssv_event.Start();
        }

        public void stop() {
            wssv_data.Stop();
            wssv_event.Stop();

        }

        public bool isStarted() {
            return false;
        }

        public void process(double[] input, out double[] output) {

            // create the output package
            output = new double[input.Length];

            // check if the filter is enabled
            output = input;
            if (mEnableFilter) {
                byte[] result = new byte[0];
                output.Select(n =>
                {
                    BitConverter.GetBytes(n).ToArray().Select(m =>
                    {
                        result = result.Append(m).ToArray();

                        return m;
                    }).ToArray();

                    return n;
                }).ToArray();

                result = result.Prepend(Convert.ToByte(outputFormat.packageRate)).ToArray();
                result = result.Prepend(Convert.ToByte(outputFormat.numSamples)).ToArray();
                result = result.Prepend(Convert.ToByte(outputFormat.numChannels)).ToArray();

                if (wssv_data.IsListening)
                {
                    wssv_data.WebSocketServices["/"].Sessions.Broadcast(result);
                }
            }

            // pass reference

            // handle the data logging of the output (both to file and for visualization)
            processOutputLogging(output);

        }

        private void Data_onEventLogged(string message)
        {
            if (wssv_event.IsListening)
            {
                wssv_event.WebSocketServices["/"].Sessions.Broadcast(message);
            }
        }

        public void destroy() {
            wssv_data.Stop();
            wssv_event.Stop();


            // stop the filter
            // Note: At this point stop will probably have been called from the mainthread before destroy, however there is a slight
            // chance that in the future someone accidentally will put something in the configure/initialize that should have
            // actually been put in the start. If start is not called in the mainthread, then stop will also not be called at the
            // modules. For these accidents we do an extra stop here.
            stop();

        }

    }


}
