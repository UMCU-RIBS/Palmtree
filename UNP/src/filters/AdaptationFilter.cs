using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UNP.Core.Helpers;
using UNP.Core.Params;

namespace UNP.Filters {

    class AdaptationFilter : IFilter {

        private static Logger logger = LogManager.GetLogger("Adaptation");
        private static Parameters parameters = ParameterManager.GetParameters("Adaptation", Parameters.ParamSetTypes.Filter);

        private bool mEnableFilter = false;
        private uint inputChannels = 0;
        private uint outputChannels = 0;
        
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

        enum AdaptationTypes : int {
            none = 0,
            rawToInitial = 1,
            rawToFirstSamples = 2,
            rawToLatestSamples = 3,
            meerAdaptatieMeerBeter,
        };

        public AdaptationFilter() {

            // define the parameters
            parameters.addParameter <bool>      (
                "EnableFilter",
                "Enable AdaptationFilter",
                "1");

            parameters.addParameter <bool>      (
                "WriteIntermediateFile",
                "Write filter input and output to file",
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

            parameters.addParameter <int>       (
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

            // TODO: add exceptions to parameters, the error here should make configure return false
            //int unit = parameters.getValueInSamples("EnableFilter");

            // filter is enabled/disabled
            mEnableFilter = parameters.getValue<bool>("EnableFilter");

            // check if the filter is enabled
            if (mEnableFilter) {
                // filter enabled

                // check if the number of input channels is higher than 0
                if (inputChannels <= 0) {
                    logger.Error("Number of input channels cannot be 0");
                    return false;
                }

                // retrieve adaptation settings
                mAdaptation = parameters.getValue<int[]>("Adaptation");
                if (mAdaptation.Length < inputChannels) {
                    logger.Error("The number of entries in the Adaptation parameter must match the number of input channels");
                    return false;
                }
                
		        // check if init or adaptation are enabled to check other parameters
		        bool initial = false;
		        bool adaptUsingBuffer = false;
		        for( int channel = 0; channel < inputChannels; ++channel ) {
                    initial |= (mAdaptation[channel] == (int)AdaptationTypes.rawToInitial);
                    adaptUsingBuffer |= (mAdaptation[channel] == (int)AdaptationTypes.rawToFirstSamples || mAdaptation[channel] == (int)AdaptationTypes.rawToLatestSamples);
		        }

		        // check initial parameters if needed
		        if (initial) {

                    // retrieve adaptation to initial settings
                    mInitialMeans = parameters.getValue<double[]>("InitialChannelMeans");
                    mInitialStds = parameters.getValue<double[]>("InitialChannelStds");

                    // check if there are enough input parameters on the initial settings
                    if (mInitialMeans.Length < inputChannels) {
                        logger.Error("The number of entries in the InitialChannelMeans parameter must match the number of input channels");
                        return false;
                    }
                    if (mInitialStds.Length < inputChannels) {
                        logger.Error("The number of entries in the InitialChannelStds parameter must match the number of input channels");
                        return false;
                    }
		        }


		        // check adaptation parameters if needed
		        if (adaptUsingBuffer) {

                    // retrieve the buffer size in samples
                    mBufferSize = parameters.getValueInSamples("BufferLength");
                    if (mBufferSize < 1) {
				        logger.Error("The BufferLength parameter specifies a zero-sized buffer (while one or more channels are set to dynamic adaptation");
                        return false;
                    }

                    // retrieve the minimum length of the buffer (before adaptation starts)
                    mAdaptationMinimalLength = parameters.getValueInSamples("AdaptationMinimalLength");
			        if (mAdaptationMinimalLength > mBufferSize) {
				        logger.Error("The AdaptationMinimalLength parameter should be shorter than the BufferLength parameter");
                        return false;
                    }
			        if (mAdaptationMinimalLength < 2) {
				        logger.Error("The AdaptationMinimalLength parameter should at least be more than 1");
                        return false;
                    }

                    // retrieve the (std) exclusion threshold for the channels
                    mExcludeStdThreshold = parameters.getValue<double[]>("ExcludeStdThreshold");
			        if (mExcludeStdThreshold.Length < inputChannels ) {
                        logger.Error("The number of entries in the AF_ExcludeStdThreshold parameter must match the number of input channels");
                        return false;
                    }

                    // retrieve the number of samples to discard
                    mBufferDiscardFirst = parameters.getValueInSamples("BufferDiscardFirst");
                    if (mBufferDiscardFirst < 0 ) {
                        logger.Error("The BufferDiscardFirst parameter cannot be a negative number");
                        return false;
                    }

                }
                
            
            }

            // return success
            return true;
            
        }

        public void initialize() {

            // check if the filter is enabled
            if (mEnableFilter) {

                // create the data buffers
                mDataBuffers = new RingBuffer[inputChannels];
                for (uint i = 0; i < inputChannels; i++) mDataBuffers[i] = new RingBuffer((uint)mBufferSize);

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

        }

    }

}
