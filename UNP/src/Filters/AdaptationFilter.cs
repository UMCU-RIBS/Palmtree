using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UNP.Core;
using UNP.Core.Helpers;
using UNP.Core.Params;

namespace UNP.Filters {

    public class AdaptationFilter : FilterBase, IFilter {

        private new const int CLASS_VERSION = 1;

        private int[] mAdaptation = null;
        private int mBufferSize = 0;                        // time window of past data per buffer that enters into statistic
        private int mAdaptationMinimalLength = 0;           // length of the buffer that has to be filled before adaptation starts
        private int mBufferDiscardFirst = 0;                // the amount of time at the start where samples should not be put in the buffer - 0 is off -

        private double[] mInitialMeans = null;				// the initial mean to adapt the signal to
        private double[] mInitialStds = null;				// the initial std to adapt the signal to
        private double[] mExcludeStdThreshold = null;		// the std threshold (on a standard normal distribution) above which a sample will be excluded from buffering

        private double[] mCalcMeans = null;                 // the calculated mean to adapt the signal to
        private double[] mCalcStds = null;                  // the calcuted stds to adapt the signal to
        private RingBuffer[] mDataBuffers = null;           // holds the data for each channel (for the size of buffer)
        private bool mAdaptationOnMessage = false;

        private const double emptyCalcValue = 9999.9999;

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
                "",
                "0", "3", "0",
                new string[]{   "No adaptation",
                                "To initial parameters",
                                "To first samples",
                                "To lastest samples",
                                //"Meer adaptatie, meer beter...",
                            });

            parameters.addParameter <double[]>  (
                "InitialChannelMeans",
                "Initial channel means",
                "", "", "0");

            parameters.addParameter <double[]>  (
                "InitialChannelStds",
                "Initial channel standard deviations",
                "", "", "1");

            parameters.addParameter <int>       (
                "BufferLength",
                "Time window of past data per buffer that enters into statistic (in samples or seconds)",
                "0", "", "9s");

            parameters.addParameter <double>       (
                "BufferDiscardFirst",
                "The amount of time at the start where samples should not be put in the buffer (in samples or seconds). 0 is off.",
                "0", "", "1s");

            parameters.addParameter <int>       (
                "AdaptationMinimalLength",
                "Length of the buffer that has to be filled before adaptation starts (in samples or seconds)",
                "0", "", "5s");

            parameters.addParameter <double[]>  (
                "ExcludeStdThreshold",
                "The threshold (on a standard normal distribution) above which a sample will be excluded from buffering",
                "", "", "2.7");

            // message
            logger.Info("Filter created (version " + CLASS_VERSION + ")");

        }

        /**
         * Configure the filter. Checks the values and application logic of the
         * parameters and, if valid, transfers the configuration parameters to local variables
         * (initialization of the filter is done later by the initialize function)
         **/
        public bool configure(ref SampleFormat input, out SampleFormat output) {

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
            output = new SampleFormat(outputChannels, input.getRate());

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
				        logger.Error("The BufferLength parameter specifies a zero-sized buffer (while one or more channels are set to dynamic adaptation");
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
                mDataBuffers = new RingBuffer[inputChannels];
                for (uint i = 0; i < inputChannels; i++) mDataBuffers[i] = new RingBuffer((uint)mBufferSize);

                // initialize the means and stds to empty values
                mCalcMeans = new double[inputChannels];
                for (uint i = 0; i < inputChannels; i++) mCalcMeans[i] = emptyCalcValue;
                mCalcStds = new double[inputChannels];
                for (uint i = 0; i < inputChannels; i++) mCalcStds[i] = emptyCalcValue;

                // no message has been shown
                mAdaptationOnMessage = false;

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

                //bool anyChannelDiscardFirst = true;
                //bool anyChannelDoAdapt = false;
                //bool anyChannelAddedToBuffer = false;

			    // loop through every channel
			    for(int channel = 0; channel < inputChannels; ++channel ) {
				
				    // check whether the samples should be discarded
				    if (mBufferDiscardFirst == 0) {
					    // do not (or stop) discarding first samples

					    // flag as not discarding samples (any more, or at the start)
					    //anyChannelDiscardFirst = false;

					    // check
					    // if adaptation is set to 2: to first samples, and the buffer is not yet full
					    // or there should be a continues updating of samples in the buffer (3: to lastest samples)
                        if ((mAdaptation[channel] == (int)AdaptationTypes.rawToFirstSamples && !mDataBuffers[channel].IsFull()) ||
						     mAdaptation[channel] == (int)AdaptationTypes.rawToLatestSamples) {

						    // check if no calc values are available
						    if (mCalcMeans[channel] == emptyCalcValue) {
							    // no calc values available

							    // flag as added to buffer
							    //anyChannelAddedToBuffer = true;

							    // add to the buffer
							    mDataBuffers[channel].Put(input[channel]);

						    } else {
							    // calc values available

							    // calculate z-score for input sample
							    double zscore;
							    if (mCalcStds[channel] == 0)
                                    zscore = (input[channel] - mCalcMeans[channel]);
							    else
                                    zscore = (input[channel] - mCalcMeans[channel]) / mCalcStds[channel];

							    // check the threshold
							    if (zscore <= mExcludeStdThreshold[channel]) {

								    // flag as added to buffer
								    //anyChannelAddedToBuffer = true;

								    // add to the buffer
                                    mDataBuffers[channel].Put(input[channel]);

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

				    // produce the output
				    if (mAdaptation[channel] == (uint)AdaptationTypes.none) {
					    output[channel] = input[channel];

				    } else if (mAdaptation[channel] == (int)AdaptationTypes.rawToInitial) {

					    // mark as applying adaption
					    //anyChannelDoAdapt = true;

					    /*
					    // write the applied mean and std for the sample
					    if (channel == 0) {
						    State("Log_AF_Ch0_AppliedMean_f").AsFloat() = (float)mInitialMeans[channel];
						    State("Log_AF_Ch0_AppliedStd_f").AsFloat() = (float)mInitialStds[channel];
					    } else if (channel == 1) {
						    State("Log_AF_Ch1_AppliedMean_f").AsFloat() = (float)mInitialMeans[channel];
						    State("Log_AF_Ch1_AppliedStd_f").AsFloat() = (float)mInitialStds[channel];
					    }
					    */


					    // set the output
					    output[channel] = (input[channel] - mInitialMeans[channel]) / mInitialStds[channel];

				    } else if (mAdaptation[channel] == (int)AdaptationTypes.rawToFirstSamples || mAdaptation[channel] == (int)AdaptationTypes.rawToLatestSamples) {
				
					    if (mCalcMeans[channel] == emptyCalcValue)
						    output[channel] = 0;
					    else {

						    // mark as applying adaption
						    //anyChannelDoAdapt = true;

						    /*
						    // write the applied mean and std for the sample
						    if (channel == 0) {
							    State("Log_AF_Ch0_AppliedMean_f").AsFloat() = (float)mCalcMeans[channel];
							    State("Log_AF_Ch0_AppliedStd_f").AsFloat() = (float)mCalcStds[channel];
						    } else if (channel == 1) {
							    State("Log_AF_Ch1_AppliedMean_f").AsFloat() = (float)mCalcMeans[channel];
							    State("Log_AF_Ch1_AppliedStd_f").AsFloat() = (float)mCalcStds[channel];
						    }
						    */

						    // set the output
						    output[channel] = (input[channel] - mCalcMeans[channel]) / mCalcStds[channel];
                            
					    }

					    //bciout << "- " << "in " << inputSerial[channel, sample] << "  mCalcMeans[channel] " << mCalcMeans[channel] << "  mCalcStds[channel] " << mCalcStds[channel] << "  out " << outputSerial[channel][sample] << endl;

				    }

			    }

                // Update the calculated values
                update();

            } else {
                // filter disabled

                // pass the input straight through
                for (uint channel = 0; channel < inputChannels; ++channel)  output[channel] = input[channel];

            }

            // handle the data logging of the output (both to file and for visualization)
            processOutputLogging(output);

        }

        private void update() {

	        for(int channel = 0; channel < inputChannels; ++channel ) {
		        if (mAdaptation[channel] == (int)AdaptationTypes.rawToFirstSamples || mAdaptation[channel] == (int)AdaptationTypes.rawToLatestSamples) {

			        // retrieve the buffer
                    double[] data = mDataBuffers[channel].Data();
                    uint dataLength = mDataBuffers[channel].Fill();
			        double bufferSum = 0;
			
			        // check if the minimum number of samples is reached before calculating
			        if (dataLength >= mAdaptationMinimalLength) {
				
				        // message
				        if (!mAdaptationOnMessage) {
                            logger.Debug("Minimum adaptation length (" + mAdaptationMinimalLength + " samples) reached, applying adaptation from now on");
					        mAdaptationOnMessage = true;
				        }

				        // calculate and store the mean
				        for(uint i = 0; i < dataLength; ++i )       bufferSum += data[i];
                        mCalcMeans[channel] = bufferSum / dataLength;
				
				        // calculate the SSQ and std, store the std
				        double SSQ = 0.0;
				        for(uint i = 0; i < dataLength; ++i )       SSQ += (data[i] - mCalcMeans[channel]) * (data[i] - mCalcMeans[channel]);
                        mCalcStds[channel] = Math.Sqrt(SSQ / dataLength);

			        }
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
