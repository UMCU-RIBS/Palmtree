/**
 * The TimeSmoothingFilter class
 * 
 * ...
 * 
 * 
 * Copyright (C) 2017:  RIBS group (Nick Ramsey Lab), University Medical Center Utrecht (The Netherlands) & external contributors
 * Concept:             UNP Team                    (neuroprothese@umcutrecht.nl)
 * Author(s):           Max van den Boom            (info@maxvandenboom.nl)
 * 
 * This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software
 * Foundation, either version 3 of the License, or (at your option) any later version. This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for
 * more details. You should have received a copy of the GNU General Public License along with this program. If not, see <https://www.gnu.org/licenses/>.
 */
using NLog;
using Palmtree.Core;
using Palmtree.Core.Helpers;
using Palmtree.Core.Params;
using System;

namespace Palmtree.Filters {

    /// <summary>
    /// TimeSmoothingFilter class.
    /// 
    /// ...
    /// </summary>
    public class TimeSmoothingFilter : FilterBase, IFilter {

        private new const int CLASS_VERSION = 2;

        private RingBuffer[] mDataBuffers = null;                   // an array of ringbuffers, a ringbuffer for every channel
        private double[][] mBufferWeights = null;                   // matrix with the buffer weights for each channel (1st dimention are the channels; 2nd dimension are the sample weights per channel)

        public TimeSmoothingFilter(string filterName) {

            // set class version
            base.CLASS_VERSION = CLASS_VERSION;

            // store the filter name
            this.filterName = filterName;

            // initialize the logger and parameters with the filter name
            logger = LogManager.GetLogger(filterName);
            parameters = ParameterManager.GetParameters(filterName, Parameters.ParamSetTypes.Filter);

            // define the parameters
            parameters.addParameter <bool>  (
                "EnableFilter",
                "Enable TimeSmoothing Filter",
                "1");

            parameters.addParameter<bool>(
                "LogDataStreams",
                "Log the filter's intermediate and output data streams. See 'Data' tab for more settings on sample stream logging.",
                "0");

            parameters.addParameter <double[][]>  (
                "BufferWeights",
                "Weights corresponding to data buffers (columns correspond to output channels, multiple rows correspond to samples)",
                "", "", "0.7,0.5,0.2,0;0.7,0.5,0.2,0");

            // message
            logger.Info("Filter created (version " + CLASS_VERSION + ")");

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

            if (resetFilter) {

                // message
                logger.Debug("Filter reset");

            }

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

                // retrieve bufferweights
                double[][] newBufferWeights = newParameters.getValue<double[][]>("BufferWeights");

                // check if there are weights for each input channel
                if (newBufferWeights.Length < inputFormat.numChannels) {
                    logger.Error("The number of columns in the BufferWeights parameter (" + newBufferWeights.Length + ") cannot be less than the number of input channels (" + inputFormat.numChannels + "). Each column defines the weights for a single input channel.");
                    return false;
                }
                if (newBufferWeights.Length > inputFormat.numChannels)
                    logger.Warn("The number of columns in the BufferWeights parameter (" + newBufferWeights.Length + ") is higher than the number of incoming channels (" + inputFormat.numChannels + "). Each column defines the weights for a single input channel.");

                if (newBufferWeights[0].Length < 1) {
                    logger.Error("The number of rows in the BufferWeights parameter must be at least 1");
                    return false;
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

                // store the bufferweights
                mBufferWeights = newParameters.getValue<double[][]>("BufferWeights");

            }

        }

        private void printLocalConfiguration() {

            // debug output
            logger.Debug("--- Filter configuration: " + filterName + " ---");
            logger.Debug("Input channels: " + inputFormat.numChannels);
            logger.Debug("Enabled: " + mEnableFilter);
            logger.Debug("Output channels: " + outputFormat.numChannels);
            if (mEnableFilter) {
                string strWeights = "Weights: ";
                if (mBufferWeights != null) {
                    for (int i = 0; i < mBufferWeights.Length; i++) {
                        strWeights += "[" + string.Join(", ", mBufferWeights[i]) + "]";
                    }
                } else {
                    strWeights += "-";
                }
                logger.Debug(strWeights);
            }

        }

        public void initialize() {

            // check if the filter is enabled
            if (mEnableFilter) {

                // determine the buffersize
                uint bufferSize = 0;
                if (mBufferWeights.Length > 0)   bufferSize = (uint)mBufferWeights[0].Length;

                // create the data buffers
                mDataBuffers = new RingBuffer[inputFormat.numChannels];
                for (int i = 0; i < inputFormat.numChannels; i++)     mDataBuffers[i] = new RingBuffer(bufferSize);
            
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
            
            // check if the filter is enabled
            if (mEnableFilter) {
                // filter enabled
            
                // create the output package
                output = new double[input.Length];
            
                // loop over samples (by sets of channels)
                int totalSamples = inputFormat.numSamples * inputFormat.numChannels;
                for (int sample = 0; sample < totalSamples; sample += inputFormat.numChannels) {
                
		            // loop through every channel
                    for (int channel = 0; channel < inputFormat.numChannels; ++channel) {
                    
                        // add to the buffer
                        mDataBuffers[channel].Put(input[sample + channel]);

                        // for every sample generate a smoothed value based on the last ones (and the given weights)
                        double[] data = mDataBuffers[channel].Data();
                        double outputValue = 0;
                        uint ringpos = 0;
                        for (uint i = 0; i < mDataBuffers[channel].Fill(); ++i) {

	                        // calculate the correct position in the buffer weights (corrected for mcursor position)
					        ringpos = (mDataBuffers[channel].CursorPos() - i + (uint)mBufferWeights[channel].Length - 1) % (uint)mBufferWeights[channel].Length;
					        outputValue += data[i] * mBufferWeights[channel][ringpos];
					        //logger.Info("\n\\n channel " + channel + " data[i] " + data[i] + " b " + mBufferWeights[channel][ringpos] + "\n");
				
                        }

                        // store the output value
                        output[sample + channel] = outputValue;
                        
			        }

		        }

            } else {
                // filter disabled
                
                // pass reference
                output = input;

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
