/**
 * The ClickTranslatorFilter class
 * 
 * ...
 * 
 * 
 * Copyright (C) 2017:  RIBS group (Nick Ramsey Lab), University Medical Center Utrecht (The Netherlands) & external contributors
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
using NLog;
using Palmtree.Core;
using Palmtree.Core.Helpers;
using Palmtree.Core.Params;

namespace Palmtree.Filters {

    /// <summary>
    /// The <c>ClickTranslatorFilter</c> class.
    /// 
    /// ...
    /// </summary>
    public class ClickTranslatorFilter : FilterBase, IFilter {

        private new const int CLASS_VERSION = 2;

        private int[] activePeriod = null;                               // time window of buffer used for determining clicks
        private int[] mBufferSize = null;                                // now equals the activeperiod variable, can be used to enlarge the buffer but only use the last part (activeperiod)
        private int[] startActiveBlock = null;                           // now is always 0 since the activeperiod and buffersize are equal, is used when the buffer is larger than the activeperiod
        private double[] activeRateThreshold = null;                     // threshold above which the values must be (in activePeriod) to send click
        private int[] clickRefractoryPeriod = null;                      // the refractory period that should be waited before a new click can be triggered
        private int[] keySequenceRefractoryPeriod = null;                // the refractory period that should be waited before a new click can be triggered
        private int[] clickRefractoryCounter = null;                     // counter to count down the samples for refractory (after a normal click)
        private int[] keySequenceRefractoryCounter = null;               // counter to count down the samples for refractory (after the keysequence)

        private RingBuffer[] mDataBuffers = null;                        // an array of ringbuffers, a ringbuffer for every channel
        private bool[] activeState = null;

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
                "ChannelParameters",
                "Specifies click parameters per channel. Each row represents an input channel, with the following paramters:\nActivePeriod: Time window of buffer used for determining clicks (in samples or seconds).\nActiveRateClickThreshold: The threshold above which the average value (of ActivePeriod) in active state should get to send a 'click' and put the channel into inactive state.\nClickRefractoryPeriod: Time window after click in which no click will be translated (in samples or seconds).\nKeySequenceRefractoryPeriod: Time window after key sequence in which no click will be translated (in samples or seconds).",
                "", "", "0.4s;0.5;3.6s;3.6s", new string[] { "ActivePeriod", "ActiveRateClickThreshold", "ClickRefractoryPeriod", "KeySequenceRefractoryPeriod" });

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

            // check if we are resetting the filter
            if (resetFilter) {

                // message
                logger.Debug("Filter reset");

                // clear the data buffers
                for (int i = 0; i < inputChannels; i++)
                    mDataBuffers[i].Clear();

                // reset the refractory periods
                System.Array.Clear(clickRefractoryCounter, 0, clickRefractoryCounter.Length);
                System.Array.Clear(keySequenceRefractoryCounter, 0, keySequenceRefractoryCounter.Length);

                // allow for clicks
                for (uint i = 0; i < inputChannels; i++) activeState[i] = true;

            } else {

                // todo: resize the mDataBuffer according to the configuration, now just recreating instead
                mDataBuffers = new RingBuffer[inputChannels];
                for (uint i = 0; i < inputChannels; i++) mDataBuffers[i] = new RingBuffer((uint)mBufferSize[i]);
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

            // filter is enabled/disabled
            bool newEnableFilter = newParameters.getValue<bool>("EnableFilter");

            // check if the filter is enabled
            if (newEnableFilter) {
                // check channel parameters. First check the sample-based paramters (all except ActiveRateClickThreshold), then check ActiveRateClickThreshold seperately
                int[][] newChannelParams = newParameters.getValueInSamples<int[][]>("ChannelParameters");
                if (newChannelParams.Length != 4 || newChannelParams[0].Length != inputChannels) {
                    logger.Error("Channel parameters must have 4 columns (ActivePeriod, ActiveRateClickThreshold, ClickRefractoryPeriod, KeySequenceRefractoryPeriod), and exactly one row for each input channel");
                    return false;
                }

                // loop through the rows to check parameters except ActiveRateClickThreshold
                for (int row = 0; row < newChannelParams[0].Length; ++row) {

                    if (newChannelParams[0][row] < 1) {
                        logger.Error("The ActivePeriod parameter for channel " + (row + 1) + " specifies a zero-sized buffer");
                        return false;
                    }
                    if (newChannelParams[2][row] < 1) {
                        logger.Error("The ClickRefractoryPeriod parameter must be at least 1 sample, this is not the case for channel " + (row + 1));
                        return false;
                    }
                    if (newChannelParams[3][row] < 1) {
                        logger.Error("The KeySequenceRefractoryPeriod parameter must be at least 1 sample, this is not the case for channel " + (row + 1));
                        return false;
                    }

                }

                // check ActiveRateClickThreshold. First retrieve parameter again, now without calculating to samples, then loop through the rows
                double[][] ActiveRateClickThreshold = newParameters.getValue<double[][]>("ChannelParameters");

                // loop through the rows
                for (int row = 0; row < ActiveRateClickThreshold[0].Length; ++row) {
                    if (ActiveRateClickThreshold[1][row] > 1 || ActiveRateClickThreshold[1][row] < 0) {
                        logger.Error("The ActiveRateClickThreshold for channel " + (row + 1) + " is outside [0 1]");
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

                // retrieve parameters in samples to transfer activePeriod, clickRefractoryPeriod and keysequenceRefractoryPeriod
                int[][] newChannelParams = newParameters.getValueInSamples<int[][]>("ChannelParameters");

                // initialize the local variable arrays on the correct size
                activePeriod = new int[newChannelParams[0].Length];
                mBufferSize = new int[newChannelParams[0].Length];
                startActiveBlock = new int[newChannelParams[0].Length];
                clickRefractoryPeriod = new int[newChannelParams[0].Length];
                keySequenceRefractoryPeriod = new int[newChannelParams[0].Length];
                activeRateThreshold = new double[newChannelParams[0].Length];
                activeState = new bool[newChannelParams[0].Length];
                clickRefractoryCounter = new int[newChannelParams[0].Length];
                keySequenceRefractoryCounter = new int[newChannelParams[0].Length];

                // loop through values of parameters and store in local variables
                for (int row = 0; row < newChannelParams[0].Length; ++row) {

                    // store the activePeriod
                    activePeriod[row] = newChannelParams[0][row];
                    mBufferSize[row] = activePeriod[row];
                    startActiveBlock[row] = mBufferSize[row] - activePeriod[row];

                    // store the clickRefractoryPeriod
                    clickRefractoryPeriod[row] = newChannelParams[2][row];

                    // store the KeysequenceRefractoryPeriod
                    keySequenceRefractoryPeriod[row] = newChannelParams[3][row];

                }

                // retrieve parameter again, now without calculating to samples, then loop through the rows to transfer activeRateThreshold
                double[][] ActiveRateClickThreshold = newParameters.getValue<double[][]>("ChannelParameters");

                // loop through the rows and transfer to local parameter
                for (int row = 0; row < ActiveRateClickThreshold[0].Length; ++row) {
                    activeRateThreshold[row] = ActiveRateClickThreshold[1][row];
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
                string strChannelParams = "Channel parameters per channel: ";
                for (uint i = 0; i < inputChannels; i++) {
                    strChannelParams += " channel " + (i + 1) + ": ActivePeriod: " + activePeriod[i] + ", ActiveRateClickThreshold: " + activeRateThreshold[i] + ", clickRefractoryPeriod: " + clickRefractoryPeriod[i] + ", keySequenceRefractoryPeriod: " + keySequenceRefractoryPeriod[i] + " .";
                }
                logger.Debug(strChannelParams);
            }

        }

        public void initialize() {

            // check if the filter is enabled
            if (mEnableFilter) {

                // create the data buffers
                mDataBuffers = new RingBuffer[inputChannels];

                // set databuffer size to specified buffersize per channel and set the states for each channel initially to active
                for (uint i = 0; i < inputChannels; i++) {
                    mDataBuffers[i] = new RingBuffer((uint)mBufferSize[i]);
                    activeState[i] = true;
                }

                // reset the refractory periods
                System.Array.Clear(clickRefractoryCounter, 0, clickRefractoryCounter.Length);
                System.Array.Clear(keySequenceRefractoryCounter, 0, keySequenceRefractoryCounter.Length);

            }

        }

        public void start() {

            // set the state to active for all channels
            for (uint i = 0; i < inputChannels; i++) activeState[i] = true;

            // reset the refractory periods
            System.Array.Clear(clickRefractoryCounter, 0, clickRefractoryCounter.Length);
            System.Array.Clear(keySequenceRefractoryCounter, 0, keySequenceRefractoryCounter.Length);

        }

        public void stop() {

        }

        public bool isStarted() {
            return false;
        }

        // set or unset refractory period
        public void setRefractoryPeriod(bool on) {

            if (on) {
                
                // set refractory period on by copying respective refractory periods to the counters for each channel
                for (uint i = 0; i < inputChannels; i++) activeState[i] = false;
                System.Array.Copy(clickRefractoryPeriod, clickRefractoryCounter, clickRefractoryPeriod.Length);
                System.Array.Copy(keySequenceRefractoryPeriod, keySequenceRefractoryCounter, keySequenceRefractoryPeriod.Length);

            } else {                                    
                
                // set refractory period off by clearing respective refractory counters for each channel
                for (uint i = 0; i < inputChannels; i++) activeState[i] = true;
                System.Array.Clear(clickRefractoryCounter, 0, clickRefractoryCounter.Length);
                System.Array.Clear(keySequenceRefractoryCounter, 0, keySequenceRefractoryCounter.Length);

            }

            logger.Error("Set refractory period " + on);
            printLocalConfiguration();

        }

        public void process(double[] input, out double[] output) {

            // create an output sample
            output = new double[outputChannels];

            // check if the filter is enabled
            if (mEnableFilter) {

                // check if a keysequence was made
                if (Globals.getValue<bool>("KeySequenceActive")) {

                    // reset the click refractory periods (in case this one is longer than the escape refractory, we should be able to listen for clicks after the keysequence)
                    System.Array.Clear(clickRefractoryCounter, 0, clickRefractoryCounter.Length);

                    // set the escape refractory period
                    for (uint i = 0; i < keySequenceRefractoryPeriod.Length; i++) {
                        keySequenceRefractoryCounter[i] = keySequenceRefractoryPeriod[i] + 1;   // +1 one because in this same loop, the counter will be lowered with 1
                    }

                    // do not accept new keypresses
                    for (uint i = 0; i < inputChannels; i++) activeState[i] = false;

                }

                // loop over channels and samples
                for (int channel = 0; channel < inputChannels; ++channel) {

                    // add new sample to buffer
                    mDataBuffers[channel].Put(input[channel]);

                    // extract buffer
                    double[] data = mDataBuffers[channel].Data();

                    // if ready for click (active state)
                    if (activeState[channel]) {

                        //compute average over active time-window length
                        double activeRate = 0;
                        for (int j = startActiveBlock[channel]; j < data.Length; ++j) {        // deliberately using Length/Count here, we want to take the entire size of the buffer, not just the (ringbuffer) filled ones
                            activeRate += data[j];
                        }
                        activeRate /= (mBufferSize[channel] - startActiveBlock[channel]);

                        // compare average to active threshold 
                        // the first should always be 1
                        if ((activeRate >= activeRateThreshold[channel]) && (data[0] == 1)) {

                            // output a click
                            output[channel] = 1;

                            // refractory from the click
                            activeState[channel] = false;
                            clickRefractoryCounter[channel] = clickRefractoryPeriod[channel];

                        } else
                            output[channel] = 0;

                    } else {
                        // not ready for click (inactive state)

                        // inactive_state stops after set refractory period
                        output[channel] = 0;

                        // count down the refractory counters
                        if (clickRefractoryCounter[channel] > 0) clickRefractoryCounter[channel]--;
                        if (keySequenceRefractoryCounter[channel] > 0) keySequenceRefractoryCounter[channel]--;

                        // check if the counters reached 0, then allow for clicks again
                        if (clickRefractoryCounter[channel] == 0 && keySequenceRefractoryCounter[channel] == 0) {
                            activeState[channel] = true;
                        }

                    }

                }

            } else {
                // filter disabled

                // pass the input straight through
                for (uint channel = 0; channel < inputChannels; ++channel)  
                    output[channel] = input[channel];

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
