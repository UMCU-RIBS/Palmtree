/**
 * The AdaptationFilter class
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
using System;
using Palmtree.Core.DataIO;
using Palmtree.Core.Helpers;
using Palmtree.Core.Params;

namespace Palmtree.Filters {

    /// <summary>
    /// The <c>AdaptationFilter</c> class.
    /// 
    /// ...
    /// </summary>
    public class AdaptationFilter : FilterBase, IFilter {

        private new const int CLASS_VERSION = 1;

        private int[] mAdaptation = null;
        private int mBufferSize = 0;                        // time window of past data per buffer that enters into statistic
        private int mAdaptationMinimalLength = 0;           // length of the buffer that has to be filled before adaptation starts
        private int mBufferDiscardFirst = 0;                // the amount of time at the start where samples should not be put in the buffer - 0 is off -

        private double[] mInitialMeans = null;				// the initial mean to adapt the signal to
        private double[] mInitialStds = null;				// the initial std to adapt the signal to
        private double[] mExcludeStdThreshold = null;       // the std threshold (on a standard normal distribution) above which a sample will be excluded from buffering

        private RingBuffer[] dataBuffers = null;           // holds the data for each channel (for the size of buffer)
        private bool[] statSet = null;                      // whether the statistics for this channel have been calculated
        private double[] statMeans = null;                  // the calculated mean to adapt the signal to
        private double[] statStds = null;                   // the calcuted stds to adapt the signal to
        private bool[] statStopUpdate = null;               // whether the statistics per channel should not be updated each incoming sample (used in the adaptTofirst to stop calculating if the buffer is full)
        private bool adaptationOnMessage = false;

        

        public enum AdaptationTypes : int {
            none = 0,
            rawToInitial = 1,
            rawToFirstSamples = 2,
            rawToLatestSamples = 3,
            meerAdaptatieMeerBeter,
        };

        public AdaptationFilter(string filterName) {

            // set class version
            base.CLASS_VERSION = CLASS_VERSION;

            // store the filter name
            this.filterName = filterName;

            // initialize the logger and parameters with the filter name
            logger = LogManager.GetLogger(filterName);
            parameters = ParameterManager.GetParameters(filterName, Parameters.ParamSetTypes.Filter);

            // define the parameters
            parameters.addParameter <bool>      (
                "EnableFilter",
                "Enable AdaptationFilter",
                "1");
            
            parameters.addParameter <bool>      (
                "LogDataStreams",
                "Log the filter's intermediate and output data streams. See 'Data' tab for more settings on data stream logging.",
                "0");

            parameters.addParameter <int[]>     (
                "Adaptation",
                "The adaptation type for each incoming channel, use one of the following options:\n0: no adaptation\n1: to initial params\n2: to first samples\n3: to lastest samples (sliding window)\n\nE.g. if there are three incoming channels, then setting this parameter to '0 3 1' would leave the first incoming channel\nunmodified, adapt the second channel to the lastest samples and adapt the third to a fixed mean and standard deviation\n\nNote: make sure that a value exists in this parameter for each incoming channel.",
                "0", "3", "0",
                new string[]{   "No adaptation",
                                "To initial parameters",
                                "To first samples",
                                "To lastest samples",
                            });

            parameters.addParameter <double[]>  (
                "InitialChannelMeans",
                "The initial channel mean for each incoming channel (only used\nwhen the adaptation parameter for the channel is set to 1).\n\nNote: make sure that a value exists in this parameter for each incoming channel.",
                "", "", "0");

            parameters.addParameter <double[]>  (
                "InitialChannelStds",
				"The initial standard deviation for each incoming channel (only used\nwhen the adaptation parameter for the channel is set to 1).\n\nNote: make sure that a value exists in this parameter for each incoming channel.",
                "", "", "1");

            parameters.addParameter <int>       (
                "BufferLength",
                "Time window of past data that enters into statistic (in samples or seconds)",
                "0", "", "9s");

            parameters.addParameter <double>       (
                "BufferDiscardFirst",
                "The amount of time at the start where samples should not be put in the channel buffer (in samples or seconds). 0 is off.",
                "0", "", "1s");

            parameters.addParameter <int>       (
                "AdaptationMinimalLength",
                "Length of the buffer that has to be filled before adaptation starts (in samples or seconds)",
                "0", "", "5s");

            parameters.addParameter <double[]>  (
                "ExcludeStdThreshold",
                "The threshold for each incoming channel (on a standard normal distribution) above which a sample will be excluded from buffering\n\nNote: make sure that a value exists in this parameter for each incoming channel.",
                "", "", "2.7");

            // message
            logger.Info("Filter created (version " + CLASS_VERSION + ")");

        }

        /**
         * Configure the filter. Checks the values and application logic of the
         * parameters and, if valid, transfers the configuration parameters to local variables
         * (initialization of the filter is done later by the initialize function)
         **/
        public bool configure(ref PackageFormat input, out PackageFormat output) {

            // retrieve the number of input channels
            inputChannels = input.getNumberOfChannels();
            if (inputChannels <= 0) {
                logger.Error("Number of input channels cannot be 0");
                output = null;
                return false;
            }

            // set the number of output channels as the same
            // (same regardless if enabled or disabled)
            outputChannels = inputChannels;

            // create an output sampleformat
            output = new PackageFormat(outputChannels, input.getSamples(), input.getRate());

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

            // initialize the variables
            initialize();

            // return success
            return true;

        }


        /**
         * check the values and application logic of the given parameter set
         **/
        private bool checkParameters(Parameters newParameters) {

            // 
            // TODO: newParameters.checkminimum, checkmaximum

            // TODO: add exceptions to parameters, the error here should make configure return false
            //int unit = newParameters.getValueInSamples("EnableFilter");
    
            // filter is enabled/disabled
            bool newEnableFilter = newParameters.getValue<bool>("EnableFilter");

            // check if the filter is enabled
            if (newEnableFilter) {

                // check adaptation settings
                int[] newAdaptation = newParameters.getValue<int[]>("Adaptation");
                if (newAdaptation.Length < inputChannels) {
                    logger.Error("The number of entries in the Adaptation parameter must match the number of input channels");
                    return false;
                }
                
		        // check if init or adaptation are enabled to check other parameters
		        bool initial = false;
		        bool adaptUsingBuffer = false;
		        for( int channel = 0; channel < inputChannels; ++channel ) {
                    initial |= (newAdaptation[channel] == (int)AdaptationTypes.rawToInitial);
                    adaptUsingBuffer |= (newAdaptation[channel] == (int)AdaptationTypes.rawToFirstSamples || newAdaptation[channel] == (int)AdaptationTypes.rawToLatestSamples);
		        }

		        // check initial parameters if needed
		        if (initial) {

                    // retrieve adaptation to initial settings
                    double[] newInitialMeans = newParameters.getValue<double[]>("InitialChannelMeans");
                    double[] newInitialStds = newParameters.getValue<double[]>("InitialChannelStds");

                    // check if there are enough input parameters on the initial settings
                    if (newInitialMeans.Length < inputChannels) {
                        logger.Error("The number of entries in the InitialChannelMeans parameter must match the number of input channels");
                        return false;
                    }
                    if (newInitialStds.Length < inputChannels) {
                        logger.Error("The number of entries in the InitialChannelStds parameter must match the number of input channels");
                        return false;
                    }
		        }


		        // check adaptation parameters if needed
		        if (adaptUsingBuffer) {

                    // check the buffer size in samples
                    int newBufferSize = newParameters.getValueInSamples("BufferLength");
                    if (newBufferSize < 1) {
				        logger.Error("The BufferLength parameter specifies a zero-sized buffer (while one or more channels are set to dynamic adaptation)");
                        return false;
                    }

                    // check the minimum length of the buffer (before adaptation starts)
                    int newAdaptationMinimalLength = newParameters.getValueInSamples("AdaptationMinimalLength");
			        if (newAdaptationMinimalLength > newBufferSize) {
				        logger.Error("The AdaptationMinimalLength parameter should be shorter than the BufferLength parameter");
                        return false;
                    }
			        if (newAdaptationMinimalLength < 2) {
				        logger.Error("The AdaptationMinimalLength parameter should at least be more than 1");
                        return false;
                    }

                    // check the (std) exclusion threshold for the channels
                    double[] newExcludeStdThreshold = newParameters.getValue<double[]>("ExcludeStdThreshold");
			        if (newExcludeStdThreshold.Length < inputChannels ) {
                        logger.Error("The number of entries in the AF_ExcludeStdThreshold parameter must match the number of input channels");
                        return false;
                    }

                    // check the number of samples to discard
                    int newBufferDiscardFirst = newParameters.getValueInSamples("BufferDiscardFirst");
                    if (newBufferDiscardFirst < 0 ) {
                        logger.Error("The BufferDiscardFirst parameter cannot be a negative number");
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

                // store the adaptation settings
                mAdaptation = newParameters.getValue<int[]>("Adaptation");
                
		        // check if init or adaptation are enabled to check other parameters
		        bool initial = false;
		        bool adaptUsingBuffer = false;
		        for( int channel = 0; channel < inputChannels; ++channel ) {
                    initial |= (mAdaptation[channel] == (int)AdaptationTypes.rawToInitial);
                    adaptUsingBuffer |= (mAdaptation[channel] == (int)AdaptationTypes.rawToFirstSamples || mAdaptation[channel] == (int)AdaptationTypes.rawToLatestSamples);
		        }

		        // check if initial parameters are needed
		        if (initial) {

                    // store adaptation to initial settings
                    mInitialMeans = newParameters.getValue<double[]>("InitialChannelMeans");
                    mInitialStds = newParameters.getValue<double[]>("InitialChannelStds");

		        }

		        // check if adaptation parameters are needed
		        if (adaptUsingBuffer) {

                    // store the buffer size in samples, the minimum length of the buffer (before adaptation starts),
                    // the (std) exclusion threshold for the channels and the number of samples to discard
                    mBufferSize = newParameters.getValueInSamples("BufferLength"); 
                    mAdaptationMinimalLength = newParameters.getValueInSamples("AdaptationMinimalLength");
                    mExcludeStdThreshold = newParameters.getValue<double[]>("ExcludeStdThreshold");
                    mBufferDiscardFirst = newParameters.getValueInSamples("BufferDiscardFirst");

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
                logger.Debug("Adaptation: " + (mAdaptation == null ? "-" : string.Join(",", mAdaptation)));
                logger.Debug("InitialChannelMeans: " + (mInitialMeans == null ? "-" : string.Join(",", mInitialMeans)));
                logger.Debug("InitialChannelStds: " + (mInitialStds == null ? "-" : string.Join(",", mInitialStds)));
                logger.Debug("BufferLength: " + mBufferSize);
                logger.Debug("AdaptationMinimalLength: " + mAdaptationMinimalLength);
                logger.Debug("ExcludeStdThreshold: " + (mExcludeStdThreshold == null ? "-" : string.Join(",", mExcludeStdThreshold)));
                logger.Debug("mBufferDiscardFirst: " + mBufferDiscardFirst);
            }

        }

        public void initialize() {

            // check if the filter is enabled
            if (mEnableFilter) {

                // create the data buffers
                dataBuffers = new RingBuffer[inputChannels];
                for (uint i = 0; i < inputChannels; i++) dataBuffers[i] = new RingBuffer((uint)mBufferSize);

                // initialize statistic variables
                statSet = new bool[inputChannels];
                statMeans = new double[inputChannels];
                statStds = new double[inputChannels];
                statStopUpdate = new bool[inputChannels];
                for (uint i = 0; i < inputChannels; i++) {
                    statSet[i] = false;
                    statMeans[i] = 0;
                    statStds[i] = 0;
                    statStopUpdate[i] = false;
                }

                // no message has been shown
                adaptationOnMessage = false;
                
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
                for (int channel = 0; channel < inputChannels; ++channel ) {
				
				    // check whether the samples should be discarded
				    if (mBufferDiscardFirst == 0) {
					    // do not (or stop) discarding first samples

					    // check
					    // if adaptation is set to 2: to first samples, and the buffer is not yet full
					    // or there should be a continues updating of samples in the buffer (3: to lastest samples)
                        if ((mAdaptation[channel] == (int)AdaptationTypes.rawToFirstSamples && !dataBuffers[channel].IsFull()) ||
						     mAdaptation[channel] == (int)AdaptationTypes.rawToLatestSamples) {

						    // check if statistical values are available
						    if (!statSet[channel]) {
							    // no statistical values are set

							    // add to the buffer
							    dataBuffers[channel].Put(input[channel]);

						    } else {
                                // statistical values are set

                                // calculate z-score for input sample
                                double zscore;
							    if (statStds[channel] == 0)
                                    zscore = (input[channel] - statMeans[channel]);
							    else
                                    zscore = (input[channel] - statMeans[channel]) / statStds[channel];

							    // check the threshold
							    if (zscore <= mExcludeStdThreshold[channel]) {

								    // add to the buffer
                                    dataBuffers[channel].Put(input[channel]);

							    } else {

								    // message
                                    logger.Debug("Sample (ch: " + channel + ", raw: " + input[channel] + ", z: " + zscore + ") rejected, exceeded mExcludeStdThreshold (" + mExcludeStdThreshold[channel] + ")");

							    }

						    }

					    }

				    } else {
					    // discard first samples

					    // message
                        logger.Debug("Sample discarded (ch: " + channel + ", raw: " + input[channel] + "), since it is first input");

					    // make sure counting happens only once (at the last channel)
					    // (nature of the nested loops, it should only be once per sample)
					    if (channel == inputChannels - 1) {

						    // reduce the sample discard count
						    mBufferDiscardFirst--;

					    }

				    }

                    // check if the statistics should be updated
                    if (    (mAdaptation[channel] == (int)AdaptationTypes.rawToFirstSamples && !statStopUpdate[channel]) ||
                             mAdaptation[channel] == (int)AdaptationTypes.rawToLatestSamples) {

                        // Update the channel's calculated values
                        updateStats(channel);
                        
                    }

                    // check if the adaptation is set to first samples and the number of first samples will not change anymore (buffer is full and statistics are calculated)
                    if (mAdaptation[channel] == (int)AdaptationTypes.rawToFirstSamples && statSet[channel] && dataBuffers[channel].IsFull() && !statStopUpdate[channel]) {

                        // flag to stop updating the statistics for this channel
                        statStopUpdate[channel] = true;
                        
                        // message
                        logger.Debug("Calculated mean and sd for calibration of channel " + channel + ", based on first samples: " + statMeans[channel] + " and " + statStds[channel]);

                        // log stop
                        Data.logEvent(2, "stopStatUpdate", (channel + ";" + statMeans[channel] + ";" + statStds[channel]));

                    }

                    // produce the output
                    if (mAdaptation[channel] == (uint)AdaptationTypes.none) {
					    output[channel] = input[channel];

				    } else if (mAdaptation[channel] == (int)AdaptationTypes.rawToInitial) {

					    // set the output
					    output[channel] = (input[channel] - mInitialMeans[channel]) / mInitialStds[channel];

				    } else if (mAdaptation[channel] == (int)AdaptationTypes.rawToFirstSamples || mAdaptation[channel] == (int)AdaptationTypes.rawToLatestSamples) {
				
					    if (!statSet[channel])
						    output[channel] = 0;
					    else {

						    // set the output
						    output[channel] = (input[channel] - statMeans[channel]) / statStds[channel];
                            
					    }

				    }

                }

                

            } else {
                // filter disabled

                // pass the input straight through
                for (uint channel = 0; channel < inputChannels; ++channel)  output[channel] = input[channel];

            }

            // handle the data logging of the output (both to file and for visualization)
            processOutputLogging(output);

        }

        private void updateStats(int channel) {
            
            // retrieve the buffer
            double[] data = dataBuffers[channel].Data();
            uint dataLength = dataBuffers[channel].Fill();
			double bufferSum = 0;
			
			// check if the minimum number of samples is reached before calculating
			if (dataLength >= mAdaptationMinimalLength) {
                
                // message
                if (!adaptationOnMessage) {
                    logger.Debug("Minimum adaptation length (" + mAdaptationMinimalLength + " samples) reached, applying adaptation from now on");
					adaptationOnMessage = true;
				}

				// calculate and store the mean
				for(uint i = 0; i < dataLength; ++i )       bufferSum += data[i];
                statMeans[channel] = bufferSum / dataLength;
				
				// calculate the SSQ and std, store the std
				double SSQ = 0.0;
				for(uint i = 0; i < dataLength; ++i )       SSQ += (data[i] - statMeans[channel]) * (data[i] - statMeans[channel]);
                statStds[channel] = Math.Sqrt(SSQ / dataLength);

                // flag the statistics for this channel as set
                statSet[channel] = true;

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
