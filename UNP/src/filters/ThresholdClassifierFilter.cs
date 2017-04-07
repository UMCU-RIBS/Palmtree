using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UNP.helpers;

namespace UNP.filters {

    class ThresholdClassifierFilter : IFilter {

        private static Logger logger = LogManager.GetLogger("ThresholdClassifier");
        private static Parameters parameters = ParameterManager.GetParameters("ThresholdClassifier", Parameters.ParamSetTypes.Filter);

        private bool mEnableFilter = false;
        private uint inputChannels = 0;
        private uint outputChannels = 0;

        private uint[] mConfigInputChannels = null;
        private uint[] mConfigOutputChannels = null;
        private double[] mConfigThresholds = null;
        private int[] mConfigDirections = null;

        public ThresholdClassifierFilter() {

            // define the parameters
            



        }

        public Parameters getParameters() {
            return parameters;
        }

        // 
        public bool configure(ref SampleFormat input, out SampleFormat output) {

            // TODO: preflight checks (use sample size, channels and parameters)


            // set if the filter is enabled
            mEnableFilter = true;

            // store the number of input channels
            inputChannels = input.getNumberOfChannels();

            // 
            mConfigInputChannels = new uint[1] { 0 };        // 0 = channel 1
            mConfigOutputChannels = new uint[1] { 0 };        // 0 = channel 1
            mConfigThresholds = new double[1] { 0.45 };
            mConfigDirections = new int[1] { 1 };

            // determine the highest output channel from the configuration
            uint maxOutputChannel = 0;
            for (int row = 0; row < mConfigOutputChannels.Count(); ++row)
            {
                if (mConfigOutputChannels[row] > maxOutputChannel)
                    maxOutputChannel = mConfigOutputChannels[row];
            }

            // set the number of output channels to the highest possible output
            outputChannels = maxOutputChannel + 1;

            // create an output sampleformat
            output = new SampleFormat(outputChannels);

            return true;

        }

        public void initialize() {

        }

        public void start() {
            return;
        }

        public void stop() {

        }

        public bool isStarted() {
            return false;
        }

        public void process(double[] input, out double[] output) {

            // create an output sample
            output = new double[outputChannels];

            // set output initially to 0
            for (uint channel = 0; channel < outputChannels; ++channel)     output[channel] = 0.0;

            // loop through the config rows and accumilate the input channels into the output channels
            for (uint row = 0; row < mConfigThresholds.Count(); ++row)      output[mConfigOutputChannels[row]] += input[mConfigInputChannels[row]];

		    // Thresholding
		    for(uint channel = 0; channel < outputChannels; ++channel ) {

                // check if the direction is positive and the output value is higher than the threshold, or
                // if the direction is negative and the output value is lower than the threshold
                if ((mConfigDirections[channel] <  0 && output[channel] < mConfigThresholds[channel]) || 
                    (mConfigDirections[channel] >= 0 && output[channel] > mConfigThresholds[channel])       ) {

				    output[channel]= 1;

			    } else {
	
				    output[channel] = 0;

                }

            }
        }

        public void destroy() {

        }

    }

}
