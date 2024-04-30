/**
 * ClickTranslatorFilter class
 * 
 * Filter to translate channel input to (binary) clicks given a specific active period, a threshold and a refractory period.
 * Other channels can pass through untouched.
 * 
 * 
 * Copyright (C) 2024:  RIBS group (Nick Ramsey Lab), University Medical Center Utrecht (The Netherlands) & external contributors
 * Concept:             UNP Team                    (neuroprothese@umcutrecht.nl)
 * Author(s):           Max van den Boom            (info@maxvandenboom.nl)
 *                      Benny van der Vijgh         (benny@vdvijgh.nl)
 * 
 * Adapted from:        Patrik Andersson (andersson.j.p@gmail.com)
 *                      Erik Aarnoutse (E.J.Aarnoutse@umcutrecht.nl)
 * 
 * This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software
 * Foundation, either version 3 of the License, or (at your option) any later version. This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for
 * more details. You should have received a copy of the GNU General Public License along with this program. If not, see <https://www.gnu.org/licenses/>.
 */
using System;
using NLog;
using Palmtree.Core;
using Palmtree.Core.Helpers;
using Palmtree.Core.Params;

namespace Palmtree.Filters {

    /// <summary>
    /// ClickTranslatorFilter class
    /// 
    /// Translate a signal above a threshold for a specific time to a binary signal (click). Allows for a refractoryPeriod
    /// </summary>
    public class ClickTranslatorFilter : FilterBase, IFilter {

        private new const int CLASS_VERSION         = 5;

		public new enum ResetOptions:int {
			Minimal = 0,                                        // reset the minimum, trying to retain as much of the information in the filter as possible
			Complete = 1,                                       // perform a complete reset of filter information
            StartFullRefractoryPeriod = 100,                    // starts the full refractory period on all translated channels
            StopRefractoryPeriod = 110,                         // immediately stops the refractory period on all translated channels
		};

        // configuration variables
        private int[] mChannels                     = null;     // the channels that need to be thresholded (0-based)
        private int[] mActivePeriod                 = null;     // time window of buffer used for determining clicks
        private int[] mRefractoryPeriod             = null;     // the refractory period that should be waited before a new click can be triggered
        private double[] mActiveRateThreshold       = null;     // threshold above which the values must be (in activePeriod) to send click

        private int[] mStartActiveBlock             = null;     // now is always 0 since the activeperiod and buffersize are equal, is used when the buffer is larger than the activeperiod
        private int[] mBufferSize                   = null;     // now equals the activeperiod variable, can be used to enlarge the buffer but only use the last part (activeperiod)
        
        // worker variables (every element in these arrays represents one input channel; however whether that element is used is determined by the configuration variables)
        private int[] refractoryCounter             = null;     // counter to count down the samples for refractory (after a normal click); 
        private RingBuffer[] dataBuffers            = null;     // buffer to hold the input values that clicks are determined upon
        private bool[] activeState                  = null;     // whether a specific channel is open to clicks (true/active = open to click, false = in refractory period)



        public ClickTranslatorFilter(string filterName) {

            // set class version
            base.CLASS_VERSION = CLASS_VERSION;

            // store the filter name
            this.filterName = filterName;

            // initialize the logger and parameters with the filter name
            logger = LogManager.GetLogger(filterName);
            parameters = ParameterManager.GetParameters(filterName, Parameters.ParamSetTypes.Filter);

            // define the parameters
            parameters.addParameter<bool>(
               "EnableFilter",
               "Enable click translator filter",
               "1");

            parameters.addParameter<bool>(
                "LogDataStreams",
                "Log the filter's intermediate and output data streams. See 'Data' tab for more settings on sample stream logging.",
                "0");

            parameters.addParameter<double[][]>(
                "Translators",
                "Specifies which channels are click-translated, the other channels pass through untouched.\n\nChannel: the channel (1...n) to which click-translation will be applied.\nActivePeriod: Time window of buffer used for determining clicks (in samples or seconds).\nActiveRateThreshold: The threshold above which the average value (of ActivePeriod) in active state should get to send a 'click' and put the channel into inactive state.\nRefractoryPeriod: Time window after click in which no click will be translated (in samples or seconds).",
                "", "", "1,2;0.4s,0.4s;0.5,0.5;3.6s,3.6s", new string[] { "Channel", "ActivePeriod", "ActiveRateThreshold", "RefractoryPeriod" });

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



                // 
                // re-init worker variables if needed (take resetFilter into account
                // 

                // TODO: currently just resetting buffers.
                //       activestate and refractory are managed below seperately (to take into account resetFilter

                
                for (uint i = 0; i < inputFormat.numChannels; i++) {
                    
                    // here, the resetoption should be taken into account to determine whether or not reset the activestate (of the newly added channels) 
                    // set the state initially to active
                    //activeState[i] = true;
                    
                    // if the channel should be translated, then either resize or clear out the buffer
                    uint newSize = 0;
                    for (uint j = 0; j < mChannels.Length; j++) {
                        if (mChannels[j] == i) {
                            newSize = (uint)mBufferSize[i];
                            break;
                        }
                    }
                    if (dataBuffers[i].Size() != newSize)   dataBuffers[i] = new RingBuffer(newSize);
                    else                                    dataBuffers[i].Clear();
                
                }

            }

            //
            // reset the refractory-period and active-state depending on the reset-option
            // 

            if (resetOption == (int)ResetOptions.StartFullRefractoryPeriod) {

                // start the refractory period and set the state to not open to clicks on all translating channels
		        for (uint i = 0; i < mChannels.Length; ++i) {
                    refractoryCounter[mChannels[i]] = mRefractoryPeriod[mChannels[i]];
                    activeState[mChannels[i]] = false;
                }

            } else if (resetOption == (int)ResetOptions.Minimal) {

                // leave the activestate and refractoryperiods alone


            } else {
                // either the reset option is set to:
                //   - stop the refractory period on all translating channels, or
                //   - complete reset

                // set the refractor period to 0 and state as open to clicks on all translating channels
		        for (uint i = 0; i < mChannels.Length; ++i) {
                    int channel = mChannels[i];
                    refractoryCounter[channel] = 0;
                    activeState[mChannels[i]] = true;

                }
                
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

                // retrieve the translators parameter values
                // ActivePeriod and RefractoryPeriod in samples; ignore the columns 0='Channel' and 2='ActiveRateThreshold', which are not specified in samples
                double[][] newTranslators = newParameters.getValueInSamples<double[][]>("Translators", new int[] { 0, 2 });
                if (newTranslators.Length != 0 && newTranslators.Length != 4) {
                    logger.Error("Translating parameter must have 4 columns (Channel, ActivePeriod, ActiveRateThreshold, RefractoryPeriod)");
                    return false;
                }
                if (newTranslators.Length > 0) {
                    for (int row = 0; row < newTranslators[0].Length; ++row) {
                    
                        // check the channel indices (1...#chan and not double)
                        if (newTranslators[0][row] < 1 || newTranslators[0][row] % 1 != 0) {
                            logger.Error("Channels indices must be positive integers (note that the channel numbering is 1-based)");
                            return false;
                        }
                        if (newTranslators[0][row] > inputFormat.numChannels) {
                            logger.Error("One of the channel indices (value " + newTranslators[0][row] + ") exceeds the number of channels coming into the filter (" + inputFormat.numChannels + ")");
                            return false;
                        }
                        for (int j = 0; j < newTranslators[0].Length; ++j ) {
                            if (row != j && newTranslators[0][row] == newTranslators[0][j]) {
                                logger.Error("One of the channel indices (value " + newTranslators[0][row] + ") occurs twice. A channel can only be thresholded once.");
                                return false;
                            }
                        }

                        // check active-period, refractory-periode (in samples)
                        if (newTranslators[1][row] < 1) {
                            logger.Error("The ActivePeriod parameter for channel " + newTranslators[0][row] + " specifies a zero-sized buffer");
                            return false;
                        }
                        if (newTranslators[3][row] < 1) {
                            logger.Error("The RefractoryPeriod parameter must be (convert to) at least 1 sample, this is not the case for channel " + newTranslators[0][row]);
                            return false;
                        }
                        
                        // check ActiveRateThreshold (as double)
                        if (newTranslators[2][row] > 1 || newTranslators[2][row] < 0) {
                            logger.Error("The ActiveRateThreshold for channel " + newTranslators[0][row] + " is outside [0 1]");
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

            // if the filter is enabled
            mEnableFilter = newParameters.getValue<bool>("EnableFilter");
            if (mEnableFilter) {

                // retrieve the translators parameter values
                // ActivePeriod and RefractoryPeriod in samples; ignore the columns 0='Channel' and 2='ActiveRateThreshold', which are not specified in samples
                double[][] newTranslators = newParameters.getValueInSamples<double[][]>("Translators", new int[] { 0, 2 });
                if (newTranslators == null || newTranslators.Length == 0) {
                    mChannels = new int[0];
                    mActivePeriod = new int[0];
                    mRefractoryPeriod = new int[0];
                    mActiveRateThreshold = new double[0];

                    mBufferSize = new int[0];
                    mStartActiveBlock = new int[0];

                } else {
                    mChannels = new int[newTranslators[0].Length];
                    mActivePeriod = new int[newTranslators[0].Length];
                    mRefractoryPeriod = new int[newTranslators[0].Length];
                    mActiveRateThreshold = new double[newTranslators[0].Length];

                    mBufferSize = new int[newTranslators[0].Length];
                    mStartActiveBlock = new int[newTranslators[0].Length];

                    for (int row = 0; row < newTranslators[0].Length; ++row ) {
                        mChannels[row] = (int)newTranslators[0][row] - 1;
                        mActivePeriod[row] = (int)newTranslators[1][row];
                        mActiveRateThreshold[row] = newTranslators[2][row];
                        mRefractoryPeriod[row] = (int)newTranslators[3][row];

                        mBufferSize[row] = mActivePeriod[row];    
                        mStartActiveBlock[row] = mBufferSize[row] - mActivePeriod[row];

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
                string strTranslating = "Thresholding: ";
                if (mChannels != null) {
                    for (int i = 0; i < mChannels.Length; i++) {
                        strTranslating += "[" + (mChannels[i] + 1) + ", AP (smpls): " + mActivePeriod[i] + ", ART: " + mActiveRateThreshold[i] + ", RefrPer (smpls): " + mRefractoryPeriod[i] + "]";
                    }
                } else
                    strTranslating += "-";
                logger.Debug(strTranslating);
            }

        }

        public bool initialize() {

            // check if the filter is enabled
            if (mEnableFilter) {
                
                // initialize worker variables
                dataBuffers = new RingBuffer[inputFormat.numChannels];
                refractoryCounter = new int[inputFormat.numChannels];
                activeState = new bool[inputFormat.numChannels];

                // loop over the input channels
                for (uint i = 0; i < inputFormat.numChannels; i++) {
                    
                    // set the state initially to active
                    activeState[i] = true;

                    // initialize the channel buffer
                    //
                    // if the channel should be translated, make sure a buffer is initialized
                    // to the buffer size, elsewise initialize an empty buffer
                    uint channelFound = 0;
                    for (channelFound = 0; channelFound < mChannels.Length; channelFound++)
                        if (mChannels[channelFound] == i)
                            break;
                    if (channelFound < mChannels.Length)    dataBuffers[i] = new RingBuffer((uint)mBufferSize[channelFound]);
                    else                                    dataBuffers[i] = new RingBuffer(0);
                    
                }
                
            }
            
            // return success
            return true;
            
        }

        public void start() {

            // set the state to active for all channels
            for (uint i = 0; i < inputFormat.numChannels; i++)
                activeState[i] = true;

            // reset the refractory periods
            Array.Clear(refractoryCounter, 0, refractoryCounter.Length);

        }

        public void stop() {

        }

        public bool isStarted() {
            return false;
        }
        
        public void process(double[] input, out double[] output) {

            // check if the filter is enabled
            if (mEnableFilter && mChannels.Length > 0) {
                // filter enabled and channels to translate
                
                // create an output package (no changes to #channels in this filter, so #output-samples is same as actual #input-samples)
                output = new double[input.Length];

                // if there are channels that only need to pass through untouched, then make a copy of the input matrix and only translate specific values
                // if all channels need to be translated, then this copy can be skipped because all values will be overwritten anyway.
                if (mChannels.Length != inputFormat.numChannels)
                    Buffer.BlockCopy(input, 0, output, 0, input.Length * sizeof(double));
                
                // loop over the samples (in steps of the number of channels)
                for (int sample = 0; sample < input.Length; sample += inputFormat.numChannels) {

		            // translate only the channels that are configured as such
		            for (uint i = 0; i < mChannels.Length; ++i) {
                        int channel = mChannels[i];
                        
                        // add new sample to buffer
                        dataBuffers[channel].Put(input[sample + channel]);

                        // reference buffer
                        double[] data = dataBuffers[channel].Data();

                        // if ready for click (active state)
                        if (activeState[channel]) {

                            //compute average over active time-window length
                            double activeRate = 0;
                            for (int j = mStartActiveBlock[channel]; j < data.Length; ++j)        // deliberately using Length/Count here, we want to take the entire size of the buffer, not just the (ringbuffer) filled ones
                                activeRate += data[j];
                            activeRate /= (mBufferSize[channel] - mStartActiveBlock[channel]);

                            // compare average to active threshold 
                            // the first should always be 1
                            if (activeRate >= mActiveRateThreshold[channel]) {

                                // output a click
                                output[sample + channel] = 1;

                                // refractory from the click
                                activeState[channel]        = false;
                                refractoryCounter[channel]  = mRefractoryPeriod[channel];

                            } else
                                output[sample + channel]    = 0;

                        } else {
                            // not ready for click (inactive state)

                            // inactive_state stops after set refractory period
                            output[sample + channel] = 0;
                            
                            // count down the refractory counter and check if we can allow clicks again
                            if (--refractoryCounter[channel] <= 0)
                                activeState[channel] = true;

                        }

                    }
                }
                
            } else {
                // filter disabled or no channels that require translating

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
