/**
 * KeySequenceFilter class
 * 
 * ...
 * 
 * 
 * Copyright (C) 2017:  RIBS group (Nick Ramsey Lab), University Medical Center Utrecht (The Netherlands) & external contributors
 * Concept:             UNP Team                    (neuroprothese@umcutrecht.nl)
 * Author(s):           Max van den Boom            (info@maxvandenboom.nl)
 * 
 * Adapted from:        Meron Vermaas               (m.vermaas-2@umcutrecht.nl)
 * 
 * This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software
 * Foundation, either version 3 of the License, or (at your option) any later version. This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for
 * more details. You should have received a copy of the GNU General Public License along with this program. If not, see <https://www.gnu.org/licenses/>.
 */
using NLog;
using Palmtree.Core;
using Palmtree.Core.DataIO;
using Palmtree.Core.Helpers;
using Palmtree.Core.Params;
using System;

namespace Palmtree.Filters {

    /// <summary>
    /// KeySequenceFilter class.
    /// 
    /// ...
    /// </summary>
    public class KeySequenceFilter : FilterBase, IFilter {

        private new const int CLASS_VERSION = 3;

        private int filterInputChannel = 1;							// input channel
        private double mThreshold = 0;                              // 
        private double mProportionCorrect = 0;                      // 
        private bool[] mSequence = null;                            // 

        private BoolRingBuffer mDataBuffer = null;                  // a boolean ringbuffer to hold the last samples in

        private bool keySequenceState = false;
        private bool keySequencePreviousState = false;

        public KeySequenceFilter(string filterName) {

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
                "Enable Normalizer Filter",
                "1");

            parameters.addParameter<bool>(
                "LogDataStreams",
                "Log the filter's intermediate and output data streams. See 'Data' tab for more settings on sample stream logging.",
                "0");

            parameters.addParameter<int>(
                "FilterInputChannel",
                "Channel to take as input (1...n)",
                "1", "", "1");

            parameters.addParameter <double>  (
                "Threshold",
                "The threshold above which a sample will be classified as a 1 before going into the data buffer",
                "", "", "0.5");

            parameters.addParameter <double>  (
                "Proportion",
                "The proportion of samples in the data buffer that needs to be the same as the pre-defined sequence",
                "0", "1", "0.7");
            
            parameters.addParameter <bool[]>  (
                "Sequence",
                "Sequence activation pattern and amount of samples needed.\nThe pattern is matched chronological order (i.e. the first value in the pattern is matched against the most recent sample)",
                "", "", "1,1,1,1,1,1");

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

            // create an output sampleformat
            output = new SamplePackageFormat(input.numChannels, input.numSamples, input.packageRate, input.valueOrder);

            // store a references to the input and output format
            inputFormat = input;
            outputFormat = output;

            // check the values and application logic of the parameters
            if (!checkParameters(parameters)) return false;

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

                // check the input channel setting
                int newFilterInputChannel = newParameters.getValue<int>("FilterInputChannel");
                if (newFilterInputChannel < 1) {
                    logger.Error("Invalid input channel, should be higher than 0 (1...n)");
                    return false;
                }
                if (newFilterInputChannel > inputFormat.numChannels) {
                    logger.Error("Input should come from channel " + newFilterInputChannel + ", however only " + inputFormat.numChannels + " channels are coming in");
                    return false;
                }

                // check the proportion correct setting
                double newProportion = newParameters.getValue<double>("Proportion");
	            if (newProportion < 0 || newProportion > 1) {
		            logger.Error("Proportion should not be smaller than 0 or higher than 1");
                    return false;
                }

                // check the sequence
                bool[] newSequence = newParameters.getValue<bool[]>("Sequence");
                if (newSequence.Length <= 0) {
                    logger.Error("Sequence array should be at least 1 value long");
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

                // store the input channel setting
                filterInputChannel = newParameters.getValue<int>("FilterInputChannel");
                
                // store the threshold and proportion correct parameters
                mThreshold = newParameters.getValue<double>("Threshold");
                mProportionCorrect = newParameters.getValue<double>("Proportion");

                // store the sequence
                mSequence = newParameters.getValue<bool[]>("Sequence");

            }

        }

        private void printLocalConfiguration() {

            // debug output
            logger.Debug("--- Filter configuration: " + filterName + " ---");
            logger.Debug("Input channels: " + inputFormat.numChannels);
            logger.Debug("Enabled: " + mEnableFilter);
            logger.Debug("Output channels: " + outputFormat.numChannels);
            if (mEnableFilter) {
                logger.Debug("FilterInputChannel: " + filterInputChannel);
                logger.Debug("Threshold: " + mThreshold);
                logger.Debug("Proportion: " + mProportionCorrect);
                logger.Debug("Sequence: " + (mSequence == null ? "-" : string.Join(",", mSequence).Replace("True", "1").Replace("False", "0")));
            }

        }

        public void initialize() {

            // set the key-sequence as not active
            Globals.setValue<bool>("KeySequenceActive", "0");
            keySequenceState = false;
            keySequencePreviousState = false;

            // check if the filter is enabled
            if (mEnableFilter) {

                // init the databuffer
                mDataBuffer = new BoolRingBuffer((uint)mSequence.Length);

            }

        }

        public void start() {

            // set the key-sequence as not active
            Globals.setValue<bool>("KeySequenceActive", "0");
            keySequenceState = false;
            keySequencePreviousState = false;

        }

        public void stop() {

            // clear the databuffer
            if (mDataBuffer != null)    mDataBuffer.Clear();
            
        }

        public bool isStarted() {
            return false;
        }

        public void process(double[] input, out double[] output) {
            
            // if the filter is enabled
            if (mEnableFilter) {
                
                int totalSamples = inputFormat.numSamples * inputFormat.numChannels;
                for (int sample = 0; sample < totalSamples; sample += inputFormat.numChannels) {

                    double[] singleRow = new double[inputFormat.numChannels];
                    Buffer.BlockCopy(input, sample * sizeof(double), singleRow, 0, inputFormat.numChannels * sizeof(double));
                    processSample(singleRow);
                    
                }

            }

            // pass reference
            output = input;

            // handle the data logging of the output (both to file and for visualization)
            processOutputLogging(output);

        }

        public void processSample(double[] input) {
    
            // set boolean based on threshold setting
            bool inValue = (input[filterInputChannel - 1] >= mThreshold);
                
            // add boolean to ringbuffer
            mDataBuffer.Put(inValue);
                
            // check if ringbuffer was filled
            if (mDataBuffer.Fill() == mSequence.Length) {

                // reset compare counter
                int mCompareCounter = 0;
                
                // check if sequence and input are the same
                bool[] clickData = mDataBuffer.Data();
                uint seqLength = (uint)mSequence.Length;
                uint ringpos = 0;
                for (uint i = 0; i < mSequence.Length; ++i) {
                    ringpos = (mDataBuffer.CursorPos() - i + seqLength - 1) % seqLength;
                    if (clickData[i] == mSequence[ringpos])
                        mCompareCounter++;
                }

                // check if proportion of comparison between keysequence and ringbuffer is met
                // set the KeySequenceActive global variable accordingly
                if ((double)mCompareCounter / mSequence.Length >= mProportionCorrect)
                    keySequenceState = true;
                else
                    keySequenceState = false;

                // check if the escapestate has changed
                if (keySequenceState != keySequencePreviousState) {

                    // set the global
                    Globals.setValue<bool>("KeySequenceActive", (keySequenceState ? "1" : "0"));

                    // log the change in escape-state
                    Data.logEvent(1, "KeySequenceChange", (keySequenceState) ? "1" : "0");

                    // update the flag
                    keySequencePreviousState = keySequenceState;

                }

            }

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
