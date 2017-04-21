using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UNP.Core.Helpers;
using UNP.Core.Params;

namespace UNP.Filters {

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
            parameters.addParameter <bool>  (
                "EnableFilter",
                "Enable TimeSmoothing Filter",
                "1");

            parameters.addParameter<bool>(
                "WriteIntermediateFile",
                "Write filter input and output to file",
                "0");

            parameters.addParameter <double[][]>  (
                "BufferWeights",
                "Weights corresponding to data buffers (columns correspond to output channels, multiple rows correspond to samples)",
                "", "", "0");

        }
        
        public Parameters getParameters() {
            return parameters;
        }

        public bool configure(ref SampleFormat input, out SampleFormat output) {

            // store the number of input channels, set the number of output channels as the same
            // (same regardless if enabled or disabled)
            inputChannels = input.getNumberOfChannels();
            outputChannels = inputChannels;

            // create an output sampleformat
            output = new SampleFormat(outputChannels);

            // 
            // TODO: parameters.checkminimum, checkmaximum

            // filter is enabled/disabled
            mEnableFilter = parameters.getValue<bool>("EnableFilter");

            // check if the filter is enabled
            if (mEnableFilter) {
                // filter enabled

                // check if the number of input channels is at least 1
                if (inputChannels < 1) {
                    logger.Error("Number of input channels cannot be less than 1");
                    return false;
                }

                // retrieve bufferweights
                mBufferWeights = parameters.getValue<double[][]>("BufferWeights");

                // check if there are weights for each input channel
                if (mBufferWeights.Length < inputChannels) {
                    logger.Error("The number of columns in the BufferWeights parameter cannot be less than the number of input channels (as each column gives the weights for each input channel)");
                    return false;
                }
                if (mBufferWeights[0].Length < 1) {
                    logger.Error("The number of rows in the BufferWeights parameter must be at least 1");
                    return false;
                }

            }
           
            return true;

        }

        public void initialize() {

            // check if the filter is enabled
            if (mEnableFilter) {

                // determine the buffersize
                uint bufferSize = 0;
                if (mBufferWeights.Count() > 0)   bufferSize = (uint)mBufferWeights[0].Count();

                // create the data buffers
                mDataBuffers = new RingBuffer[inputChannels];
                for (int i = 0; i < inputChannels; i++)     mDataBuffers[i] = new RingBuffer(bufferSize);
            
            }

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
