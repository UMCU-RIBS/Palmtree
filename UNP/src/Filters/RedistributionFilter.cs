/**
 * The RedistributionFilter class
 * 
 * ...
 * 
 * 
 * Copyright (C) 2017:  RIBS group (Nick Ramsey Lab), University Medical Center Utrecht (The Netherlands) & external contributors
 * Author(s):           Max van den Boom            (info@maxvandenboom.nl)
 * 
 * Adapted from:        BCI2000 (Schalk Lab, www.schalklab.org)
 * 
 * This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software
 * Foundation, either version 3 of the License, or (at your option) any later version. This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for
 * more details. You should have received a copy of the GNU General Public License along with this program. If not, see <https://www.gnu.org/licenses/>.
 */
using NLog;
using UNP.Core.Helpers;
using UNP.Core.Params;

namespace UNP.Filters {

    /// <summary>
    /// The <c>RedistributionFilter</c> class.
    /// 
    /// ...
    /// </summary>
    public class RedistributionFilter : FilterBase, IFilter {

        private new const int CLASS_VERSION = 1;

        private int[] mConfigInputChannels = null;
        private int[] mConfigOutputChannels = null;
        private double[] mConfigWeights = null;

        public RedistributionFilter(string filterName) {

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
                "Enable filter",
                "1");

            parameters.addParameter<bool>(
                "LogDataStreams",
                "Log the filter's intermediate and output data streams. See 'Data' tab for more settings on sample stream logging.",
                "0");

            parameters.addParameter <double[][]>  (
                "Redistribution",
                "Specifies which input channels are added together to one or more output channels.\nAlso specifies which weights are applied to the input values before addition\n\nInput: Input channel (1...n)\nOutput: output channel (1...n)\nWeight: Weight applied to input channel value",
                "", "", "1;1;1", new string[] { "Input", "Output", "Weight" });

            // message
            logger.Info("Filter created (version " + CLASS_VERSION + ")");

        }

        /**
         * Configure the filter. Checks the values and application logic of the
         * parameters and, if valid, transfers the configuration parameters to local variables
         * (initialization of the filter is done later by the initialize function)
         **/
        public bool configure(ref PackageFormat input, out PackageFormat output) {

            // retrieve and check the number of input channels
            inputChannels = input.getNumberOfChannels();
            if (inputChannels <= 0) {
                logger.Error("Number of input channels cannot be 0");
                output = null;
                return false;
            }

            // check the values and application logic of the parameters
            if (!checkParameters(parameters)) {
                output = null;
                return false;
            }

            // transfer the parameters to local variables
            transferParameters(parameters);
            
            // check if the filter is enabled
            if (mEnableFilter) {
                // filter enabled

                // determine the highest output channel from the
                // configuration and set this as the number of output channels
                int highestOutputChannel = 0;
                for (int row = 0; row < mConfigOutputChannels.Length; ++row) {
                    if (mConfigOutputChannels[row] > highestOutputChannel)
                        highestOutputChannel = mConfigOutputChannels[row];
                }
                outputChannels = highestOutputChannel;

            } else {
                // filter disabled

                // filter will pass the input straight through, so same number of channels as output
                outputChannels = inputChannels;

            }

            // create an output sampleformat
            output = new PackageFormat(outputChannels, input.getSamples(), input.getRate());

            // configure output logging for this filter
            configureOutputLogging(filterName + "_", output);

            // print configuration
            printLocalConfiguration();

            // return success
            return true;

        }

        /**
         *  Re-configure the filter settings on the fly (during runtime) using the given parameterset. 
         *  Checks if the new settings have adjustments that cannot be applied to a running filter
         *  (most likely because they would adjust the number of expected output channels, which would have unforseen consequences for the next filter)
         *  
         *  The local parameter is left untouched so it is easy to revert back to the original configuration parameters
         *  The functions handles both the configuration and initialization of filter related variables.
         **/
        public bool configureRunningFilter(Parameters newParameters, bool resetFilter) {
            
            // check if new parameters are given (only a reset is also an option)
            if (newParameters != null) {

                // retrieve whether the filter should be enabled
                bool newEnabled = newParameters.getValue<bool>("EnableFilter");
                if (!mEnableFilter && newEnabled) {
                    // filter was off, and should be switched on

                    // determine the highest output channel from the configuration
                    double[][] newRedistribution = parameters.getValue<double[][]>("Redistribution");
                    int highestOutputChannel = 0;
                    for (int row = 0; row < newRedistribution[0].Length; ++row) {
                        if (newRedistribution[1][row] > highestOutputChannel)
                            highestOutputChannel = (int)newRedistribution[1][row];
                    }

                    // check if the number of output channels would remain the same (if the change would be applied)
                    if (outputChannels != highestOutputChannel) {

                        // message
                        logger.Error("Error while trying to enable the filter. Enabling the filter would adjust the number of output channels from " + outputChannels + " to " + highestOutputChannel + ", this might break the next filter(s) and is disallowed, not applying filter re-configuration");

                        // return failure
                        return false;

                    }

                } else if (mEnableFilter && !newEnabled) {
                    // filter was on, and should be switched off

                    // Check if the number of output channels would remain or change the same (if the change would be applied)
                    // When the filter is to be switched off, then the current number of output
                    // channels (now, with the filter on) should be the same as the number of input channels. 
                    if (outputChannels != inputChannels) {
                        // number of channels would change, disallow adjustment

                        // message
                        logger.Error("Error while trying to disable the filter. Disabling the filter would adjust the number of output channels from " + outputChannels + " to " + inputChannels + ", this might break the next filter(s) and is disallowed, not applying filter re-configuration");

                        // return failure
                        return false;

                    }

                }

                // check the values and application logic of the parameters
                if (!checkParameters(newParameters))    return false;

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

            if (resetFilter) {

                // message
                logger.Debug("Filter reset");

            }

            // initialize the variables (if needed)
            initialize();

            // return success
            return true;

        }

        /**
         * check the values and application logic of the given parameter set
         **/
        private bool checkParameters(Parameters newParameters) {

            // 
            // TODO: parameters.checkminimum, checkmaximum

            // filter is enabled/disabled
            bool newEnableFilter = newParameters.getValue<bool>("EnableFilter");

            // check if the filter is enabled
            if (newEnableFilter) {

                // check redistribution parameters
                double[][] newRedistribution = newParameters.getValue<double[][]>("Redistribution");
		        if (newRedistribution.Length != 3 || newRedistribution[0].Length <= 0) {
                    logger.Error("Redistribution parameter must have 3 columns (Input channel, Output channel, Weight) and at least one row");
                    return false;
                }

                // loop through the rows
                for (int row = 0; row < newRedistribution[0].Length; ++row ) {

                    if (newRedistribution[0][row] < 1 || newRedistribution[0][row] % 1 != 0) {
                        logger.Error("Input channels must be positive integers (note that the channel numbering is 1-based)");
                        return false;
                    }
                    if (newRedistribution[0][row] > inputChannels) {
                        logger.Error("One of the input channel values exceeds the number of channels coming into the filter (#inputChannels: " + inputChannels + ")");
                        return false;
                    }
                    if (newRedistribution[1][row] < 1 || newRedistribution[1][row] % 1 != 0) {
                        logger.Error("Output channels must be positive integers (note that the channel numbering is 1-based)");
                        return false;
                    }

                }

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

                // retrieve newRedistribution
                double[][] newRedistribution = newParameters.getValue<double[][]>("Redistribution");

                // transfer the settings
                mConfigInputChannels = new int[newRedistribution[0].Length];        // 0 = channel 1
                mConfigOutputChannels = new int[newRedistribution[0].Length];        // 0 = channel 1
                mConfigWeights = new double[newRedistribution[0].Length];
                for (int row = 0; row < newRedistribution[0].Length; ++row ) {
                    mConfigInputChannels[row] = (int)newRedistribution[0][row];
                    mConfigOutputChannels[row] = (int)newRedistribution[1][row];
                    mConfigWeights[row] = newRedistribution[2][row];
                }

            }
            
        }

        private void printLocalConfiguration() {

            // debug output
            logger.Debug("--- Filter configuration: " + filterName + " ---");
            logger.Debug("Input channels: " + inputChannels);
            logger.Debug("Enabled: " + mEnableFilter);
            logger.Debug("Output channels: " + outputChannels);
            if (mEnableFilter) {
                string strRedistribution = "Redistribution: ";
                if (mConfigInputChannels != null) {
                    for (int i = 0; i < mConfigInputChannels.Length; i++) {
                        strRedistribution += "[" + mConfigInputChannels[i] + ", " + mConfigOutputChannels[i] + ", " + mConfigWeights[i] + "]";
                    }
                } else {
                    strRedistribution += "-";
                }
                logger.Debug(strRedistribution);
            }

        }

        public void initialize() {

        }


        public void start() {
            
        }

        public void stop() {

        }

        public bool isStarted() {
            return false;
        }

        public void process(double[] input, out double[] output) {

            // create an output sample
            output = new double[outputChannels];

            // check if the filter is enabled
            if (mEnableFilter) {
                // filter enabled

                // set output initially to 0
                for (uint channel = 0; channel < outputChannels; ++channel)     output[channel] = 0.0;

                // loop through the config rows and accumilate the input channels ( * the weight) into the output channels
                for (uint row = 0; row < mConfigWeights.Length; ++row)          output[mConfigOutputChannels[row] - 1] += input[mConfigInputChannels[row] - 1] * mConfigWeights[row];

            } else {
                // filter disabled

                // pass the input straight through
                for (uint channel = 0; channel < inputChannels; ++channel)  output[channel] = input[channel];

            }

            // handle the data logging of the output (both to file and for visualization)
            processOutputLogging(output);

        }

        public void destroy() {

            // stop the filter
            // Note: At this point stop will probably have been called from the mainthread before destroy, however there is a slight
            // chance that in the future someone accidentally will put something in the configure/initialize that should have
            // actually been put in the start. If start is not called in the mainthread, then stop will also not be called at the
            // modules. For these accidents we do an extra stop here.
            stop();

        }

    }

}
