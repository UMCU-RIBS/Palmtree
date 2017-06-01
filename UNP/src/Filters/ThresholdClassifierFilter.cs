using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UNP.Core.Helpers;
using UNP.Core.Params;

namespace UNP.Filters {

    public class ThresholdClassifierFilter : IFilter {

        private string filterName = "";
        private static Logger logger = null;
        private static Parameters parameters = null;

        private bool mEnableFilter = false;
        private uint inputChannels = 0;
        private uint outputChannels = 0;

        private int[] mConfigInputChannels = null;
        private int[] mConfigOutputChannels = null;
        private double[] mConfigThresholds = null;
        private int[] mConfigDirections = null;

        public ThresholdClassifierFilter(string filterName) {

            // store the filter name
            this.filterName = filterName;

            // initialize the logger and parameters with the filter name
            logger = LogManager.GetLogger(filterName);
            parameters = ParameterManager.GetParameters(filterName, Parameters.ParamSetTypes.Filter);

            // define the parameters
            parameters.addParameter<bool>(
                "EnableFilter",
                "Enable AdaptationFilter",
                "1");

            parameters.addParameter<bool>(
                "WriteIntermediateFile",
                "Write filter input and output to file",
                "0");

            parameters.addParameter <double[][]>  (
                "Thresholds",
                "Specifies which input channels are added together to one or more output channels.\nAlso specifies what threshold values are applied to the output values, after addition, to binarize the output values\n\nInput: Input channel (1...n)\nOutput: output channel (1...n)\nThreshold: (channel output) threshold above or under which the channel output will become 1 or 0\nDirection: the direction of the thresholding.\nIf direction < 0 (negative) then smaller than the threshold will result in true; if >= 0 (positive) then larger than the threshold will result in true",
                "", "", "0", new string[] {"Input", "Output", "Threshold", "Direction" });

            

        }

        public string getName() {
            return filterName;
        }

        public Parameters getParameters() {
            return parameters;
        }

        /**
         * Configure the filter. Checks the values and application logic of the
         * parameters and, if valid, transfers the configuration parameters to local variables
         * (initialization of the filter is done later by the initialize function)
         **/
        public bool configure(ref SampleFormat input, out SampleFormat output) {

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
                for (int row = 0; row < mConfigOutputChannels.Count(); ++row) {
                    if (mConfigOutputChannels[row] > highestOutputChannel)
                        highestOutputChannel = mConfigOutputChannels[row];
                }
                outputChannels = (uint)highestOutputChannel;

            } else {
                // filter disabled

                // filter will pass the input straight through, so same number of channels as output
                outputChannels = inputChannels;

            }

            // create an output sampleformat
            output = new SampleFormat(outputChannels);

            // debug output
            logger.Debug("--- Filter configuration: " + filterName + " ---");
            logger.Debug("Input channels: " + inputChannels);
            logger.Debug("Enabled: " + mEnableFilter);
            logger.Debug("Output channels: " + outputChannels);

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

            // retrieve whether the filter should be enabled
            bool newEnabled = newParameters.getValue<bool>("EnableFilter");
            if (!mEnableFilter && newEnabled) {
                // filter was off, and should be switched on

                // determine the highest output channel from the configuration
                double[][] newThresholds = parameters.getValue<double[][]>("Thresholds");
                int highestOutputChannel = 0;
                for (int row = 0; row < newThresholds[0].Length; ++row) {
                    if (newThresholds[1][row] > highestOutputChannel)
                        highestOutputChannel = (int)newThresholds[1][row];
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

            // transfer the parameters to local variables
            transferParameters(newParameters);

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

                // check thresholds
                double[][] newThresholds = newParameters.getValue<double[][]>("Thresholds");
		        if (newThresholds.Length != 4 && newThresholds[0].Length > 0) {
                    logger.Error("Threshold parameter must have 4 columns (Input channel, Output channel, Threshold, Direction) and at least one row");
                    return false;
                }

                // loop through the rows
                for (int row = 0; row < newThresholds[0].Length; ++row ) {

                    if (newThresholds[0][row] < 1) {
                        logger.Error("Input channels must be positive integers");
                        return false;
                    }
                    if (newThresholds[0][row] > inputChannels) {
                        logger.Error("One of the input channel values exceeds the number of channels coming into the filter (#inputChannels: " + inputChannels + ")");
                        return false;
                    }
                    if (newThresholds[1][row] < 1) {
                        logger.Error("Output channels must be positive integers");
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

                // retrieve newThresholds
                double[][] newThresholds = newParameters.getValue<double[][]>("Thresholds");

                // transfer the settings
                mConfigInputChannels = new int[newThresholds[0].Length];        // 0 = channel 1
                mConfigOutputChannels = new int[newThresholds[0].Length];        // 0 = channel 1
                mConfigThresholds = new double[newThresholds[0].Length];
                mConfigDirections = new int[newThresholds[0].Length];
                for (int row = 0; row < newThresholds[0].Length; ++row ) {
                    mConfigInputChannels[row] = (int)newThresholds[0][row];
                    mConfigOutputChannels[row] = (int)newThresholds[1][row];
                    mConfigThresholds[row] = newThresholds[2][row];
                    mConfigDirections[row] = (int)newThresholds[3][row];
                }

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

                // loop through the config rows and accumilate the input channels into the output channels
                for (uint row = 0; row < mConfigThresholds.Count(); ++row)      output[mConfigOutputChannels[row] - 1] += input[mConfigInputChannels[row] - 1];

		        // Thresholding
		        for (uint channel = 0; channel < outputChannels; ++channel) {

                    // check if the direction is positive and the output value is higher than the threshold, or
                    // if the direction is negative and the output value is lower than the threshold
                    if ((mConfigDirections[channel] <  0 && output[channel] < mConfigThresholds[channel]) || 
                        (mConfigDirections[channel] >= 0 && output[channel] > mConfigThresholds[channel])       ) {

				        output[channel] = 1;

			        } else {
	
				        output[channel] = 0;

                    }

                }

            } else {
                // filter disabled

                // pass the input straight through
                for (uint channel = 0; channel < inputChannels; ++channel)  output[channel] = input[channel];

            }

        }

        public void destroy() {

        }

    }

}
