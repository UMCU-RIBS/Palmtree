using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UNP.Core.Helpers;
using UNP.Core.Params;

namespace UNP.Filters {

    class ThresholdClassifierFilter : IFilter {

        private static Logger logger = LogManager.GetLogger("ThresholdClassifier");
        private static Parameters parameters = ParameterManager.GetParameters("ThresholdClassifier", Parameters.ParamSetTypes.Filter);

        private bool mEnableFilter = false;
        private uint inputChannels = 0;
        private uint outputChannels = 0;

        private int[] mConfigInputChannels = null;
        private int[] mConfigOutputChannels = null;
        private double[] mConfigThresholds = null;
        private int[] mConfigDirections = null;

        public ThresholdClassifierFilter() {


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

        public Parameters getParameters() {
            return parameters;
        }

        // 
        public bool configure(ref SampleFormat input, out SampleFormat output) {

            // store the number of input channels
            inputChannels = input.getNumberOfChannels();

            // 
            // TODO: parameters.checkminimum, checkmaximum

            // filter is enabled/disabled
            mEnableFilter = parameters.getValue<bool>("EnableFilter");

            // check if the filter is enabled
            if (mEnableFilter) {
                // filter enabled

                // check if the number of input channels is higher than 0
                if (inputChannels <= 0) {
                    logger.Error("Number of input channels cannot be 0");
                    output = null;
                    return false;
                }

                // retrieve bufferweights
                double[][] thresholds = parameters.getValue<double[][]>("Thresholds");
		        if (thresholds.Length != 4 && thresholds[0].Length > 0) {
                    logger.Error("Threshold parameter must have 4 columns (Input channel, Output channel, Threshold, Direction) and at least one row");
                    output = null;
                    return false;
                }

                // resize the variables
                mConfigInputChannels = new int[thresholds[0].Length];        // 0 = channel 1
                mConfigOutputChannels = new int[thresholds[0].Length];        // 0 = channel 1
                mConfigThresholds = new double[thresholds[0].Length];
                mConfigDirections = new int[thresholds[0].Length];

                // loop through the rows
                for (int row = 0; row < thresholds[0].Length; ++row ) {

                    if (thresholds[0][row] < 1) {
                        logger.Error("Input channels must be positive integers");
                        output = null;
                        return false;
                    }
                    if (thresholds[0][row] > inputChannels) {
                        logger.Error("One of the input channel values exceeds the number of channels coming into the filter (#inputChannels: " + inputChannels + ")");
                        output = null;
                        return false;
                    }
                    if (thresholds[1][row] < 1) {
                        logger.Error("Output channels must be positive integers");
                        output = null;
                        return false;
                    }

                    // 
                    mConfigInputChannels[row] = (int)thresholds[0][row];
                    mConfigOutputChannels[row] = (int)thresholds[1][row];
                    mConfigThresholds[row] = thresholds[2][row];
                    mConfigDirections[row] = (int)thresholds[3][row];

                }

                // determine the highest output channel from the configuration
                int highestOutputChannel = 0;
                for (int row = 0; row < mConfigOutputChannels.Count(); ++row) {
                    if (mConfigOutputChannels[row] > highestOutputChannel)
                        highestOutputChannel = mConfigOutputChannels[row];
                }

                // set the number of output channels to the highest possible output
                outputChannels = (uint)highestOutputChannel;

                // create an output sampleformat
                output = new SampleFormat(outputChannels);


            } else {
                // filter disabled

                // filter will pass the input straight through, so same number of channels as output
                outputChannels = inputChannels;

                // create an output sampleformat
                output = new SampleFormat(outputChannels);

            }

            return true;

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
