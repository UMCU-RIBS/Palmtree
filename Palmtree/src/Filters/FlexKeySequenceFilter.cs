/**
 * FlexKeySequenceFilter class
 * 
 * ...
 * 
 * 
 * Copyright (C) 2020:  RIBS group (Nick Ramsey Lab), University Medical Center Utrecht (The Netherlands) & external contributors
 * Concept:             UNP Team                    (neuroprothese@umcutrecht.nl)
 * Author(s):           Benny van der Vijgh         (benny@vdvijgh.nl)
 * 
 * This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software
 * Foundation, either version 3 of the License, or (at your option) any later version. This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for
 * more details. You should have received a copy of the GNU General Public License along with this program. If not, see <https://www.gnu.org/licenses/>.
 */
using System;
using NLog;
using Palmtree.Core;
using Palmtree.Core.DataIO;
using Palmtree.Core.Helpers;
using Palmtree.Core.Params;

namespace Palmtree.Filters {

    /// <summary>
    /// FlexKeySequenceFilter class
    /// 
    /// ...
    /// </summary>
    public class FlexKeySequenceFilter : FilterBase, IFilter {
        private new const int CLASS_VERSION = 3;

        private int clickInputChannel = 0;                      // channel that will be monitored for clicks required to make the flex-keysequence
        private int amountOfClicks = 0;                         // amount of clicks required to create flex-keysequence
        private int minInterval = 0;                            // minimum interval between required clicks
        private int maxInterval = 0;                            // maximum interval between required clicks
        private double clickRate = 0;                           // rate of clicks that are actually required
        private bool checkMeanStd = false;                       // whether mean and sd are taken into account in creating flex-keysequence
        private int meanStdChannel = 0;                          // channel that will be monitored for mean and sd required to make flex-keysequence
        private double meanThreshold = 0;                       // threshold applied for mean
        private int meanDirection = 0;                          // whether mean should be above or below threshold
        private double stdThreshold = 0;                         // threshold applied for sd
        private int stdDirection = 0;                            // whether mean sd should be above or below threshold

        private bool keySequencePreviousState = false;          // holds most recent state of the flexible keysequence
        private RingBuffer[] mDataBuffers = null;               // an array of ringbuffers, a ringbuffer for every channel

        public FlexKeySequenceFilter(string filterName) {

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
                "Enable TimeSmoothing Filter",
                "1");

            parameters.addParameter<bool>(
                "LogDataStreams",
                "Log the filter's intermediate and output data streams. See 'Data' tab for more settings on sample stream logging.",
                "0");

            parameters.addParameter<int>(
                "ClickInputChannel",
                "Channel of which clicks are used as input.",
                "1", "", "1");

            parameters.addParameter<int>(
                "AmountOfClicks",
                "Amount of clicks required to create wake-up-call.",
                "1", "", "11");

            parameters.addParameter<int>(
                "MinimumClickInterval",
                "Minimal interval between clicks (in samples or seconds) for clicks to be counted towards the amount required to create wake-up call.",
                "0", "", "6s");

            parameters.addParameter<int>(
                "MaximumClickInterval",
                "Maximal interval between clicks (in samples or seconds) for clicks to be counted towards the amount required to create wake-up call.",
                "0", "", "10s");

            parameters.addParameter<double>(
                "ClickRate",
                "Rate (between 0 and 1) of amount of well-timed clicks required to create wake-up call.",
                "0", "", "0.9");

            parameters.addParameter<bool>(
                "CheckMeanAndStd",
                "In addition to requiring clicks to fulfill above requirements, also require mean and sd of seperate channel to fulfill requirements in order to create wake-up call.",
                "1");

            parameters.addParameter<int>(
                "MeanAndStdInputChannel",
                "Channel of which mean and sd are used as input (only if CheckMeanAndStd checkbox is ticked).",
                "1", "", "1");

            parameters.addParameter<double>(
                "MeanThreshold",
                "Mean threshold used in determining if wake-up call is created (only if CheckMeanAndStd checkbox is ticked).",
                "", "", "0");

            parameters.addParameter<int>(
                "MeanDirection",
                "Direction in which the mean is compared to the MeanThreshold. If negative, mean should be below MeanThreshold to count towards create wake-up call, otherwise should be above threshold (only if CheckMeanAndStd checkbox is ticked).",
                "", "", "1");

            parameters.addParameter<double>(
                "StdThreshold",
                "Sd threshold used in determining if wake-up call is created (only if CheckMeanAndStd checkbox is ticked).",
                "", "", "1");

            parameters.addParameter<int>(
                "StdDirection",
                "Direction in which the sd is compared to the StdThreshold. If negative, sd should be below StdThreshold to count towards create wake-up call, otherwise should be above threshold (only if CheckMeanAndStd checkbox is ticked).",
                "", "", "-1");

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
                // check parameters
                int newClickInputChannel = newParameters.getValue<int>("ClickInputChannel");
                if (newClickInputChannel < 1 || newClickInputChannel > inputFormat.numChannels) {
                    logger.Error("ClickInputChannel should be larger than 1 and smaller than amount of input channels.");
                    return false;
                }

                int newAmountOfClicks = newParameters.getValue<int>("AmountOfClicks");
                if (newAmountOfClicks < 1) {
                    logger.Error("AmountOfClicks should be larger than one, to prevent creating wake-up calls in absence of clicks.");
                    return false;
                }

                int newMinimumClickInterval = newParameters.getValueInSamples<int>("MinimumClickInterval");
                if (newMinimumClickInterval < 0) {
                    logger.Error("MinimumClickInterval should be a positive integer, to prevent requiring clicks to overlap.");
                    return false;
                }

                int newMaximumClickInterval = newParameters.getValueInSamples<int>("MaximumClickInterval");
                if (newMaximumClickInterval < 0 || newMaximumClickInterval < newMinimumClickInterval) {
                    logger.Error("MaximumClickInterval should be a positive integer and be equal or larger than MinimumClickInterval, to prevent requiring clicks to overlap.");
                    return false;
                }

                double newClickRate = newParameters.getValue<double>("ClickRate");
                if (newClickRate < 0 || newClickRate > 1) {
                    logger.Error("ClickRate should be between 0 and 1.");
                    return false;
                }

                int newMeanAndStdInputChannel = newParameters.getValue<int>("MeanAndStdInputChannel");
                if (newMeanAndStdInputChannel < 1 || newMeanAndStdInputChannel > inputFormat.numChannels) {
                    logger.Error("MeanAndStdInputChannel should be larger than 1 and smaller than amount of input channels.");
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
                // transfer parameters to local variables
                clickInputChannel = newParameters.getValue<int>("ClickInputChannel");
                amountOfClicks = newParameters.getValue<int>("AmountOfClicks");
                minInterval = newParameters.getValueInSamples<int>("MinimumClickInterval");
                maxInterval = newParameters.getValueInSamples<int>("MaximumClickInterval");
                clickRate = newParameters.getValue<double>("ClickRate");
                checkMeanStd = newParameters.getValue<bool>("CheckMeanAndStd"); 
                meanStdChannel = newParameters.getValue<int>("MeanAndStdInputChannel");
                meanThreshold = newParameters.getValue<double>("MeanThreshold");
                meanDirection = newParameters.getValue<int>("MeanDirection");
                stdThreshold = newParameters.getValue<double>("StdThreshold");
                stdDirection = newParameters.getValue<int>("StdDirection");
            }

        }

        private void printLocalConfiguration() {

            // debug output
            logger.Debug("--- Filter configuration: " + filterName + " ---");
            logger.Debug("Input channels: " + inputFormat.numChannels);
            logger.Debug("Enabled: " + mEnableFilter);
            logger.Debug("Output channels: " + outputFormat.numChannels);
            if (mEnableFilter) {
                logger.Debug("clickInputChannel: " + clickInputChannel);
                logger.Debug("amountOfClicks: " + amountOfClicks);
                logger.Debug("minInterval: " + minInterval);
                logger.Debug("maxInterval: " + maxInterval);
                logger.Debug("clickRate: " + clickRate);
                logger.Debug("checkMeanStd: " + checkMeanStd);
                logger.Debug("meanStdChannel: " + meanStdChannel);
                logger.Debug("meanThreshold: " + meanThreshold);
                logger.Debug("meanDirection: " + meanDirection);
                logger.Debug("stdThreshold: " + stdThreshold);
                logger.Debug("stdDirection: " + stdDirection);
            }
        }

        public void initialize() {

            // check if the filter is enabled
            if (mEnableFilter) {
                // create the data buffers. if mean and sd are not taken into account, create only buffer for clicks, otherwise also one for mean and sd
                mDataBuffers = checkMeanStd ? new RingBuffer[2] : new RingBuffer[1];

                // set size of buffers equal to amount of clicks that need to be detected, times the maximal allowed interval between clicks
                for (uint i = 0; i < mDataBuffers.Length; i++) mDataBuffers[i] = new RingBuffer((uint)(amountOfClicks * maxInterval));
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
            
            // set the initial key-sequence state
            bool keySequenceState = false;

            // add data to click buffer
            mDataBuffers[0].Put(input[clickInputChannel - 1]);

            // if also checking sd and mean, add data to this buffer
            if(checkMeanStd)
				mDataBuffers[1].Put(input[meanStdChannel - 1]);

            // retrieve click data from buffer
            double[] clickData = mDataBuffers[0].Data();

            // copy array to allow using original array later 
            // TODO use BlockCopy?
            double[] countClickData = new double[clickData.Length];
            Array.Copy(clickData, countClickData, clickData.Length);

            // check if copied data contains at least the required amount of clicks (amount of clicks times clickrate). Only if so proceed to test for intervals. This to increase performance: checking amount of clicks (using quicksort) is on average O(n log n), wheras checking intervals (and if needed mean and sd) is estimated around O(3n) to O(6n)
            // check amount of clicks by first sorting and then checking the amount of clicks from the end of the array (Max is precaution to prevent outofBounds in case amountOfClicks times clickRate resolves to zero)
            Array.Sort(countClickData);
            if(countClickData[Math.Max(countClickData.Length - (int)Math.Floor((double)amountOfClicks * clickRate), 1)] == 1) {

                // init vars, to cycle through click data 
                uint interval = 0;
                uint correctClicks = 0;

                // cycle through click data
                for (uint i=0; i < clickData.Length; i++) {

                    // if a click is detected 
                    if (clickData[i] == 1) {

                        // first click always fulfills interval requirements, if not first, check requirements and increase correct clicks if fullfilled 
                        if (correctClicks == 0 || (interval >= minInterval && interval <= maxInterval))                                     
							correctClicks++;
                            
                        // reset interval counter    
                        interval = 0;
                        
                    } else {
						// if no click is detected, increase interval
						interval++;
							
					}
						
                }

                // check if sufficient correct clicks have been made, and set flag if so
                if (correctClicks >= Math.Floor(amountOfClicks * clickRate)) keySequenceState = true;
                    
                // if also mean and sd need to be checked
                if (checkMeanStd) {

                    // retrieve data from buffer
                    double[] meanStdData = mDataBuffers[1].Data();

                    // init vars
                    int dataLength = meanStdData.Length;
                    double bufferSum = 0.0;
                    double SSQ = 0.0;
                    double mean = 0.0;
                    double sd = 0.0;

                    // calculate the mean
                    for (uint i = 0; i < dataLength; ++i) bufferSum += meanStdData[i];
                    mean = bufferSum / dataLength;

                    // calculate the sd                  
                    for (uint i = 0; i < dataLength; ++i) SSQ += (meanStdData[i] - mean) * (meanStdData[i] - mean);
                    sd = Math.Sqrt(SSQ / dataLength);

                    // check if mean and sd are above or below given mean and sd, as indicated by respective direction parameters, and set flag accordingly
                    if (meanDirection < 0 && stdDirection < 0 && mean < meanThreshold && sd < stdThreshold)           keySequenceState = keySequenceState && true;
                    else if (meanDirection >= 0 && stdDirection < 0 && mean >= meanThreshold && sd < stdThreshold)    keySequenceState = keySequenceState && true;
                    else if (meanDirection < 0 && stdDirection >= 0 && mean < meanThreshold && sd >= stdThreshold)    keySequenceState = keySequenceState && true;
                    else if (meanDirection >= 0 && stdDirection >= 0 && mean >= meanThreshold && sd >= stdThreshold)  keySequenceState = keySequenceState && true;
                    else                                                                                            keySequenceState = false;
                }

            }

            // check if the flex-keysequence state changed
            if (keySequenceState != keySequencePreviousState) {
					
                // set the global
                Globals.setValue<bool>("FlexKeySequenceActive", (keySequenceState ? "1" : "0"));

                // log that the state has changed
                Data.logEvent(1, "FlexKeySequenceChange", (keySequenceState) ? "1" : "0");

                // update the current (to be previous) keysequence state
                keySequencePreviousState = keySequenceState;
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
