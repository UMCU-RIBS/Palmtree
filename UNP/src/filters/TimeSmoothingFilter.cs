using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UNP.helpers;

namespace UNP.filters {

    public class TimeSmoothingFilter : IFilter {

        private static Logger logger = LogManager.GetLogger("TimeSmoothing");
        private static Parameters parameters = ParameterManager.GetParameters("TimeSmoothing", Parameters.ParamSetTypes.Filter);

        private bool mEnableFilter = false;
        private uint inputChannels = 0;
        private uint outputChannels = 0;

        private RingBuffer[] mDataBuffers = null;                   // an array of ringbuffers, a ringbuffer for every channel
        private double[][] mBufferWeights = null;                   // matrix with the buffer weights for each channel (1ste dimention are the channels; 2nd dimension are the sample weights per channel)


        public TimeSmoothingFilter() {

            // define the parameters
            parameters.addParameter <bool>  ("EnableFilter",
                                             "Enable AdaptationFilter",
                                             "", "", "1");

            /*
            Parameters.addParameter("SF_EnableFilter", "0", "1", "1");
            Parameters.addParameter("SF_WriteIntermediateFile", "0", "1", "0");
            //SF_BufferWeights
            */


        }
        
        public Parameters getParameters() {
            return parameters;
        }

        public bool configure(ref SampleFormat input, out SampleFormat output) {
            // TODO: preflight checks (use sample size, channels and parameters)

            // set if the filter is enabled
            mEnableFilter = true;

            // store the number of input channels, set the number of output channels as the same
            inputChannels = input.getNumberOfChannels();
            outputChannels = inputChannels;

            // create an output sampleformat
            // (will be the same regardless of whether the filter is enabled or not)
            output = new SampleFormat(outputChannels);

            // check if the filter is enabled
            if (mEnableFilter) {

                // check if the number of input channels is higher than 0
                if (inputChannels <= 0) {
                    logger.Error("Number of input channels cannot be 0");
                    return false;
                }

                // set the buffer weights for each channel
                mBufferWeights = new double[inputChannels][];
                for (int i = 0; i < inputChannels; i++) {

                    double[] channelWeights = new double[] { 0.7, 0.5, 0.2, 0.2, 0 };
                    mBufferWeights[i] = channelWeights;

                }
            
            }
           
            return true;

        }

        public void initialize() {

            // determine the buffersize
            uint bufferSize = 0;
            if (mBufferWeights.Count() > 0)   bufferSize = (uint)mBufferWeights[0].Count();

            // create the data buffers
            mDataBuffers = new RingBuffer[inputChannels];
            for (int i = 0; i < inputChannels; i++)     mDataBuffers[i] = new RingBuffer(bufferSize);
            

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

		    // loop through every channel
            for (int channel = 0; channel < inputChannels; ++channel) {

                // add to the buffer
                mDataBuffers[channel].Put(input[channel]);
                
                // for every sample generate a smoothed value based on the last ones (and the given weights)
                double[] data = mDataBuffers[channel].Data();
				double outputValue = 0;
                for (uint i = 0; i < mDataBuffers[channel].Fill(); ++i) {

                    // calculate the correct position in the buffer weights (corrected for mcursor position)
                    uint ringpos = (mDataBuffers[channel].CursorPos() - i + (uint)mBufferWeights[channel].Count() - 1) % (uint)mBufferWeights[channel].Count();
                    outputValue += data[i] * mBufferWeights[channel][ringpos];

                }

                // store the output value
                output[channel] = outputValue;

            }

        }

        public void destroy() {

        }

    }


}
