/**
 * ThresholdClassifierFilter class
 * 
 * This filter allows for the thresholding (binarizing) of specific channels, other channels pass through untouched.
 * 
 * 
 * Copyright (C) 2017:  RIBS group (Nick Ramsey Lab), University Medical Center Utrecht (The Netherlands) & external contributors
 * Author(s):           Max van den Boom            (info@maxvandenboom.nl)
 * 
 * Adapted from:        Patrik Andersson (andersson.j.p@gmail.com)
 *                      Erik Aarnoutse (E.J.Aarnoutse@umcutrecht.nl)
 * 
 * This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software
 * Foundation, either version 3 of the License, or (at your option) any later version. This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for
 * more details. You should have received a copy of the GNU General Public License along with this program. If not, see <https://www.gnu.org/licenses/>.
 */
using NLog;
using Palmtree.Core;
using Palmtree.Core.Params;
using System;

namespace Palmtree.Filters {

    /// <summary>
    /// ThresholdClassifierFilter class
    /// 
    /// ...
    /// </summary>
    public class ThresholdClassifierFilter : FilterBase, IFilter {

        private new const int CLASS_VERSION     = 3;

        private int[] mChannels                 = null;         // the channels that need to be thresholded (0-based)
        private double[] mThresholds            = null;         // the thresholds that will be applied to the incoming values on the specified channel
        private int[] mDirections               = null;         // the direction in which the threshold is applied


        public ThresholdClassifierFilter(string filterName) {

            // set class version
            base.CLASS_VERSION  = CLASS_VERSION;

            // store the filter name
            this.filterName     = filterName;

            // initialize the logger and parameters with the filter name
            logger              = LogManager.GetLogger(filterName);
            parameters          = ParameterManager.GetParameters(filterName, Parameters.ParamSetTypes.Filter);

            // define the parameters
            parameters.addParameter<bool>(
                "EnableFilter",
                "Enable threshold classifier filter",
                "1");

            parameters.addParameter<bool>(
                "LogDataStreams",
                "Log the filter's intermediate and output data streams. See 'Data' tab for more settings on sample stream logging.",
                "0");

            parameters.addParameter <double[][]>  (
                "Thresholding",
                "Specifies which channels are thresholded, the other channels pass through untouched.\n\nChannel: the channel (1...n) to which thresholding will be applied.\nThreshold: the threshold above (>) or under (<) which the channel output will become 1 or 0\nDirection: the direction of the thresholding. If the direction value is negative (<0) then input values smaller than\nthe threshold will result in true; if positive (>= 0) then input values larger than the threshold will result in true.",
                "", "", "1,2;0.45,0.45;1,1", new string[] { "Channel", "Threshold", "Direction" });

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

            // retrieve and check the number of input channels
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
        public bool configureRunningFilter(Parameters newParameters, int resetOption) {
            
            // check if new parameters are given (only a reset is also an option)
            if (newParameters != null) {
                
                //
                // no pre-check on the number of output channels is needed here, the number of output
                // channels will remain the some regardless to the filter being enabled or disabled
                // 

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
            
            // return success
            return true;

        }

        /**
         * check the values and application logic of the given parameter set
         **/
        private bool checkParameters(Parameters newParameters) {

            // 
            // TODO: parameters.checkminimum, checkmaximum

            // if the filter is enabled
            bool newEnableFilter = newParameters.getValue<bool>("EnableFilter");
            if (newEnableFilter) {

                // check thresholding parameters
                double[][] newThresholding = newParameters.getValue<double[][]>("Thresholding");
		        if (newThresholding.Length != 0 && newThresholding.Length != 3) {
                    logger.Error("Thresholding parameter must have 3 columns (Channel, Threshold, Direction)");
                    return false;
                }
                
                // check the channel indices (1...#chan and not double)
                if (newThresholding.Length > 0) {
                    for (int row = 0; row < newThresholding[0].Length; ++row ) {

                        if (newThresholding[0][row] < 1 || newThresholding[0][row] % 1 != 0) {
                            logger.Error("Channels indices must be positive integers (note that the channel numbering is 1-based)");
                            return false;
                        }
                        if (newThresholding[0][row] > inputFormat.numChannels) {
                            logger.Error("One of the channel indices (value " + newThresholding[0][row] + ") exceeds the number of channels coming into the filter (" + inputFormat.numChannels + ")");
                            return false;
                        }
                        for (int j = 0; j < newThresholding[0].Length; ++j ) {
                            if (row != j && newThresholding[0][row] == newThresholding[0][j]) {
                                logger.Error("One of the channel indices (value " + newThresholding[0][row] + ") occurs twice. A channel can only be thresholded once.");
                                return false;
                            }
                        }

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

            // if the filter is enabled
            mEnableFilter = newParameters.getValue<bool>("EnableFilter");
            if (mEnableFilter) {

                // retrieve and transfer the thresholds
                double[][] newThresholds = newParameters.getValue<double[][]>("Thresholding");
                if (newThresholds == null || newThresholds.Length == 0) {
                    mChannels = new int[0];
                    mThresholds = new double[0];
                    mDirections = new int[0];
                } else {
                    mChannels = new int[newThresholds[0].Length];
                    mThresholds = new double[newThresholds[0].Length];
                    mDirections = new int[newThresholds[0].Length];
                    for (int row = 0; row < newThresholds[0].Length; ++row ) {
                        mChannels[row] = (int)newThresholds[0][row] - 1;
                        mThresholds[row] = newThresholds[1][row];
                        mDirections[row] = (int)newThresholds[2][row];
                    }
                }

            }
            
        }

        private void printLocalConfiguration() {

            // debug output
            logger.Debug("--- Filter configuration: " + filterName + " ---");
            logger.Debug("Input channels: " + inputFormat.numChannels);
            logger.Debug("Enabled: " + mEnableFilter);
            logger.Debug("Output channels: " + outputFormat.numChannels);
            if (mEnableFilter) {
                string strThresholds = "Thresholding: ";
                if (mChannels != null) {
                    for (int i = 0; i < mChannels.Length; i++) {
                        strThresholds += "[Ch: " + (mChannels[i] + 1) + ", Thr: " + mThresholds[i] + ", Dir: " + mDirections[i] + "]";
                    }
                } else
                    strThresholds += "-";
                logger.Debug(strThresholds);
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

            // check if the filter is enabled
            if (mEnableFilter && mChannels.Length > 0) {
                // filter enabled and channels to threshold
                
                // create an output package
                output = new double[outputFormat.numChannels * outputFormat.numSamples];

                // if there are channels that only need to pass through untouched, then make a copy of the input matrix and only threshold specific values
                // if all channels need to be thresholded, then this copy can be skipped because all values will be overwritten anyway.
                if (mChannels.Length != inputFormat.numChannels)
                    Buffer.BlockCopy(input, 0, output, 0, input.Length * sizeof(double));

                // loop over the samples (in steps of the number of channels)
                int totalSamples = inputFormat.numSamples * inputFormat.numChannels;
                for (int sample = 0; sample < totalSamples; sample += inputFormat.numChannels) {

		            // threshold only the channels that are configured as such
		            for (uint i = 0; i < mChannels.Length; ++i) {
                        
                        // check if the direction is positive and the output value is higher than the threshold, or
                        // if the direction is negative and the output value is lower than the threshold
                        if ((mDirections[i] <  0 && input[sample + mChannels[i]] < mThresholds[i]) || 
                            (mDirections[i] >= 0 && input[sample + mChannels[i]] > mThresholds[i])) {

				            output[sample + mChannels[i]] = 1;

			            } else {
	
				            output[sample + mChannels[i]] = 0;

                        }

                    }
                    
                }

            } else {
                // filter disabled or no channels that require thresholding

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
