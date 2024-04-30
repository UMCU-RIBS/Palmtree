/**
 * NormalizerFilter class
 * 
 * Filter to normalize each channel by subtracting an offset from the signal and multiplying the signal with a factor (gain)
 * Normalization parameters can be adapted (conditionally or continously)
 * 
 * 
 * Copyright (C) 2024:  RIBS group (Nick Ramsey Lab), University Medical Center Utrecht (The Netherlands) & external contributors
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
using System;
using System.Collections.Generic;
using Palmtree.Core;
using Palmtree.Core.Helpers;
using Palmtree.Core.Params;
using Palmtree.Core.DataIO;

namespace Palmtree.Filters {

    /// <summary>
    /// NormalizerFilter class
    /// 
    /// Filter to normalize each channel by subtracting an offset from the signal and multiplying the signal with a factor (gain)
    /// </summary>
    public class NormalizerFilter : FilterBase, IFilter {

        private enum AdaptationTypes : int {
            None = 0,
            ZeroMean = 1,
            ZeroMeanUnitVar = 2,
        };

        private new const int CLASS_VERSION = 3;

        private double[] mOffsets = null;                           // array to hold the offset for each channel
        private double[] mGains = null;                             // array to hold the gain for each channel

        private int[] mAdaptation = null;
        private bool mDoAdapt = false;
        private int mBufferSize = 0;                                // time window of past data per buffer that enters into statistic
        private string[][] mBuffers = null;
        private string mUpdateTrigger = null;
        private bool mPreviousTrigger = true;                       // store the previous trigger state 

        private RingBuffer[][] mDataBuffers = null;                 // holds the data for each channel (for the size of buffer)


        public NormalizerFilter(string filterName) {

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

            //
            //
            //
            parameters.addHeader("Initial offsets/gains");
            
            parameters.addParameter <double[]>  (
                "NormalizerOffsets",
                "Normalizer offsets",
                "", "", "0,0");

            parameters.addParameter <double[]>  (
                "NormalizerGains",
                "Normalizer gain values",
                "", "", "1,1");


            //
            //
            //
            parameters.addHeader("Adaptation");
            
            parameters.addParameter<int[]>(
                "Adaptation",
                "Adaptation setting per channel: 0 no adaptation, 1 zero mean, 2 zero mean, unit variance",
                "", "", "0,0");

            parameters.addParameter<string[][]>(
                "Buffers",
                "These buffers are used to calculate the statistics for adaptation.\n\n" +
                "Each column represents an input channel, and each channel has a number of buffers equal to the number of rows.\n" +
                "The statements in the cells are evaluated during runtime, and - if true - the sample of that input channel (column) is added to that specific buffer.\n" +
                "Each of the separate buffers is sized to the BufferSize argument\n\n" +
                "The statistics that are used to normalize the input-package per channel are based on the content of the buffers and are either updated:\n" +
                " - continuously (if UpdateTrigger argument is empty)\n" +
                " - conditionally (if the UpdateTrigger arguments evaluates true)",
                "", "", "[Feedback] == 1 && [Target] == 1,[Feedback] == 1 && [Target] == 0;[Feedback] == 1 && [Target] == 1,[Feedback] == 1 && [Target] == 0");

            parameters.addParameter<double>(
                "BufferLength",
                "Time window of past data per buffer that enters into statistic",
                "", "", "9s");

            parameters.addParameter<string>( 
                "UpdateTrigger",
                "Expression (global variables can be used, e.g. [feedback] == 0) on which the offset and gain will be updated, the trigger will occur when the expression result changes from 0 to 1.\nUse empty string for continues update.\n\nNote the variablenames in the expressions are case-sensitive",
                "", "", "[Feedback]==0");

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

                // check if there are offsets for each input channel
                double[] newOffsets = newParameters.getValue<double[]>("NormalizerOffsets");
                if (newOffsets.Length < inputFormat.numChannels) {
                    logger.Error("The number of entries in the NormalizerOffsets parameter (" + newOffsets.Length + ") cannot be less than the number of input channels (" + inputFormat.numChannels + "). Each entry defines the setting for a single input channel.");
                    return false;
                }
                if (newOffsets.Length > inputFormat.numChannels)
                    logger.Warn("The number of entries in the NormalizerOffsets parameter (" + newOffsets.Length + ") is higher than the number of incoming channels (" + inputFormat.numChannels + "). Each entry defines the setting for a single input channel.");

                // check if there are gains for each input channel
                double[] newGains = newParameters.getValue<double[]>("NormalizerGains");
                if (newGains.Length < inputFormat.numChannels) {
                    logger.Error("The number of entries in the NormalizerGains parameter (" + newGains.Length + ") cannot be less than the number of input channels (" + inputFormat.numChannels + "). Each entry defines the setting for a single input channel.");
                    return false;
                }
                if (newGains.Length > inputFormat.numChannels)
                    logger.Warn("The number of entries in the NormalizerGains parameter (" + newGains.Length + ") is higher than the number of incoming channels (" + inputFormat.numChannels + "). Each entry defines the setting for a single input channel.");

                // check if there are gains for each input channel
                int[] newAdaptation = newParameters.getValue<int[]>("Adaptation");
                if (newAdaptation.Length < inputFormat.numChannels) {
                    logger.Error("The number of entries in the Adaptation parameter (" + newAdaptation.Length + ") cannot be less than the number of input channels (" + inputFormat.numChannels + "). Each entry defines the setting for a single input channel.");
                    return false;
                }
                if (newAdaptation.Length > inputFormat.numChannels)
                    logger.Warn("The number of entries in the Adaptation parameter (" + newAdaptation.Length + ") is higher than the number of incoming channels (" + inputFormat.numChannels + "). Each entry defines the setting for a single input channel.");

                // check if adaptation is needed
                bool newDoAdaptation = false;
                for(int channel = 0; channel < inputFormat.numChannels; channel++)    newDoAdaptation |= (newAdaptation[channel] != (int)AdaptationTypes.None);
                if (newDoAdaptation) {
                    // adaptation is needed

                    // check the buffers parameter
                    string[][] newBuffers = newParameters.getValue<string[][]>("Buffers");
                    if (newBuffers.Length > inputFormat.numChannels) {
                        logger.Error("The number of columns in the Buffers parameter may not exceed the number of input channels");
                        return false;
                    }
                    // Evaluate all buffer expressions to test for validity.
                    for (int col = 0; col < newBuffers.Length; col++) {
                        for (int row = 0; row < newBuffers[col].Length; row++) {
                            if (!Globals.testExpression(newBuffers[col][row])) {
                                logger.Error("The buffers parameter contains an invalid expression '" + newBuffers[col][row] + "', this expression should be corrected");
                                return false;
                            }
                        }
                    }

                    // check the buffer size in samples
                    int newBufferSize = newParameters.getValueInSamples("BufferLength");
                    if (newBufferSize < 1) {
                        logger.Error("The BufferLength parameter specifies a zero-sized buffer (while one or more channels are set to adaptation)");
                        return false;
                    }

                    // check the updatetrigger parameter
                    string newUpdateTrigger = newParameters.getValue<string>("UpdateTrigger");
                    if (!string.IsNullOrEmpty(newUpdateTrigger)) {
                        if (!Globals.testExpression(newUpdateTrigger)) {
                            logger.Error("The update-trigger parameter is does not have a valid expression, either correct or leave this field empty for continues updating");
                            return false;
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

            // filter is enabled/disabled
            mEnableFilter = newParameters.getValue<bool>("EnableFilter");

            // check if the filter is enabled
            if (mEnableFilter) {

                // store the offsets for each input channel
                mOffsets = newParameters.getValue<double[]>("NormalizerOffsets");
                // TODO: check

                // store the gains for each input channel
                mGains = newParameters.getValue<double[]>("NormalizerGains");

                // store the adaptation settings
                mAdaptation = newParameters.getValue<int[]>("Adaptation");
                for (int channel = 0; channel < inputFormat.numChannels; channel++) {
                    mDoAdapt |= (mAdaptation[channel] != (int)AdaptationTypes.None);
                }

                // check if adaptation is needed
                if (mDoAdapt) {

                    // store the buffer expressions
                    mBuffers = newParameters.getValue<string[][]>("Buffers");

                    // store the buffer size in samples
                    mBufferSize = newParameters.getValueInSamples("BufferLength");

                    // store the update-trigger parameter
                    mUpdateTrigger = newParameters.getValue<string>("UpdateTrigger");

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
                logger.Debug("Offsets: " + (mOffsets == null ? "-" : string.Join(",", mOffsets)));
                logger.Debug("Gains: " + (mGains == null ? "-" : string.Join(",", mGains)));
                logger.Debug("Adaptation: " + (mAdaptation == null ? "-" : string.Join(",", mAdaptation)));
                if (mDoAdapt) {
                    if (mBuffers != null) {
                        logger.Debug("Buffers:");
                        for (int i = 0; i < mBuffers.Length; i++) {
                            logger.Debug("     Ch" + i + " = '" + string.Join("  -   ", mBuffers[i]) + "'");
                        }
                    } else { 
                        logger.Debug("Buffers: -");
                    }
                    logger.Debug("BufferLength: " + mBufferSize);
                    logger.Debug("UpdateTrigger: '" + mUpdateTrigger + "'");
                }
            }

        }

        public bool initialize() {

            // check if the filter is enabled
            if (mEnableFilter) {

                // check if adaptation is enabled
                if (mDoAdapt) {

                    // create the databuffers
                    mDataBuffers = new RingBuffer[mBuffers.Length][];
                    for (int i = 0; i < mBuffers.Length; i++) {
                        mDataBuffers[i] = new RingBuffer[mBuffers[i].Length];
                        for (int j = 0; j < mBuffers[i].Length; j++) {
                            mDataBuffers[i][j] = new RingBuffer((uint)mBufferSize);
                        }
                    }

                }
            }
            
            // return success
            return true;

        }

        public void start() {

            // initially set to trigger to true
            mPreviousTrigger = true;

        }

        public void stop() {

            // check if adaptation is switched on
            if (mDoAdapt) {

                // build the offsets and gains strings
                string strOffsets = "";
                string strGains = "";
                for(int i = 0; i < mOffsets.Length; i++) {
					if (i != 0)	strOffsets += " ";
                    strOffsets += mOffsets[i].ToString();
                }
				for(int i = 0; i < mGains.Length; i++) {
					if (i != 0)	strGains += " ";
					strGains += " " + mGains[i].ToString();
				}

                // update parameters
                parameters.setValue("NormalizerOffsets", strOffsets);
                parameters.setValue("NormalizerGains", strGains);

            }

        }

        public bool isStarted() {
            return false;
        }

        public void process(double[] input, out double[] output) {

            // check if the filter is enabled
            if (mEnableFilter) {
                // filter enabled
                
                // create an output package (no changes to #channels in this filter, so #output-samples is same as actual #input-samples)
                output = new double[input.Length];

                // check if adaptation is on for one of the channels
                if (mDoAdapt) {

                    // 
                    // first iteration evaluates the expressions (only once per sample-package, fake precision otherwise)
                    // and adds them to their respective buffers
                    // 

                    // loop over the (input) channels
                    for (int channel = 0; channel < mBuffers.Length; ++channel) {

                        // loop over all the buffers for this input-channel
                        for (int buffer = 0; buffer < mBuffers[channel].Length; ++buffer) {

                            // test cell expression
                            if (Globals.evaluateConditionExpression(mBuffers[channel][buffer])) {

                                // loop over samples (by sets of channels) and add to buffer
                                for (int sample = 0; sample < input.Length; sample += inputFormat.numChannels)
                                    mDataBuffers[channel][buffer].Put(input[sample + channel]);

                            }
                        }
                    }

                    //
                    // check if there adaptation and update the statistics
                    // 
                    if (mUpdateTrigger != null) {
                        // update trigger set

                        // check the expression
                        bool currentTrigger = Globals.evaluateConditionExpression(mUpdateTrigger);

                        // check if the trigger expession result goes from 0 to 1
                        if (currentTrigger && !mPreviousTrigger) {
                            Data.logEvent(1, "NormalizeAdaptiveUpdate", "triggered_by_change");
                            update();
                        }

                        mPreviousTrigger = currentTrigger;
                    } else {
                        // no update trigger, update every time
                        
                        // not logging to spare the events file (that this always happens can be derived from the prm files already)
                        //Data.logEvent(1, "NormalizeAdaptiveUpdate", "continuous");    

                        //
                        update();
                    }

                }  // end mDoAdapt conditional


                //
                // second iteration normalizes every channel
                //

                // loop over samples (by sets of channels) and every channel
                for (int sample = 0; sample < input.Length; sample += inputFormat.numChannels) {
                    for (int channel = 0; channel < inputFormat.numChannels; ++channel) {

                        // normalize each sample
                        output[sample + channel] = (input[sample + channel] - mOffsets[channel]) * mGains[channel];

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

        private void update() {
            // calculate a number of statistics based on the values in each of the buffers

            // loop through the channels
            for (int channel = 0; channel < mDataBuffers.Length; channel++) {

                // check if adaptation needs to be applied to this channel
                if (mAdaptation[channel] != (int)AdaptationTypes.None) {

                    //
                    // computer the mean(s) and variandce) for each buffer
                    //

                    uint numValues = 0;
                    List<double> bufferMeans = new List<double>();
                    List<double> bufferSqMeans = new List<double>();

                    // loop through the buffers within each channel
                    for (int i = 0; i < mDataBuffers[channel].Length; ++i) {

                        // retrieve the samplebuffer and it's data
                        RingBuffer buffer = mDataBuffers[channel][i];
                        double[] data = buffer.Data();

                        // calculate the mean of the buffer and the mean sum-of-squares
                        // and add the mean and mean sum-of-squares to a buffer(list)
                        double bufferSum = 0;
                        double bufferSqSum = 0;
                        for (int j = 0; j < buffer.Fill(); ++j) {
                            bufferSum += data[j];
                            bufferSqSum += data[j] * data[j];
                        }
                        numValues += buffer.Fill();
                        if (buffer.Fill() > 0) {
                            bufferMeans.Add(bufferSum / buffer.Fill());
                            bufferSqMeans.Add(bufferSqSum / buffer.Fill());
                        }

                    }


                    //
                    // Compute the total mean and variance from the raw moments.
                    //

                    double dataMean = 0;
                    
                    // check if there are values in the buffer
                    if (bufferMeans.Count > 0) {
                        
                        // calculate the mean of means
                        for (int i = 0; i < bufferMeans.Count; i++) {
                            dataMean += bufferMeans[i];
                        }
                        dataMean /= bufferMeans.Count;

                        // set the mean of mean as the offset (use the mean of means to avoid bias)
                        mOffsets[channel] = dataMean;

                        // message
                        //logger.Debug("Channel " + channel + ": Set offset to " + mOffsets[channel] + " using information from " + bufferMeans.Count + " buffers");

                    }

                    
                    // check if there should be normalized to the unit variance (the gain should be adjusted as well)
                    if (mAdaptation[channel] == (int)AdaptationTypes.ZeroMeanUnitVar) {

                        double dataSqMean = 0;

                        // calculate the mean of the sum-of-squared means
                        for (int i = 0; i < bufferSqMeans.Count; i++) {
                            dataSqMean += bufferSqMeans[i];
                        }
                        dataSqMean /= bufferSqMeans.Count;

                        // calculate the variance? 
                        double dataVar = dataSqMean - dataMean * dataMean;

                        // check if the value is not 0
                        const double eps = 1e-10;
                        if (dataVar > eps) {

                            // calculate the gain by dividing 1 by the standard deviation?
                            mGains[channel] = 1.0 / Math.Sqrt(dataVar);

                            // message
                            //logger.Debug("Set gain to " + mGains[channel] + ", using data variance");

                        }

                        // message
                        //logger.Debug("mean: " + dataMean + " - variance:" + dataVar + " - samples: " + numValues);

                    }
                    
                }

            }   // end channel loop
            
        }   // end function

    }

}
