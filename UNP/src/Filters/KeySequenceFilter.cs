using NLog;
using System;
using UNP.Core;
using UNP.Core.DataIO;
using UNP.Core.Helpers;
using UNP.Core.Params;

namespace UNP.Filters {

    public class KeySequenceFilter : FilterBase, IFilter {

        private new const int CLASS_VERSION = 1;

        private int filterInputChannel = 1;							// input channel
        private double mThreshold = 0;                              // 
        private double mProportionCorrect = 0;                      // 
        private bool[] mSequence = null;                            // 

        private BoolRingBuffer mDataBuffer = null;                  // a boolean ringbuffer to hold the last samples in
        private int mCompareCounter = 0;

        private bool keySequenceActive = false;
        private bool keySequenceWasPressed = false;

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
                "Sequence activation pattern and amount of samples needed",
                "", "", "1 1 1 1 1 1");

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

                // check the input channel setting
                int newFilterInputChannel = newParameters.getValue<int>("FilterInputChannel");
                if (newFilterInputChannel < 1) {
                    logger.Error("Invalid input channel, should be higher than 0 (1...n)");
                    return false;
                }
                if (newFilterInputChannel > inputChannels) {
                    logger.Error("Input should come from channel " + newFilterInputChannel + ", however only " + inputChannels + " channels are coming in");
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
            logger.Debug("Input channels: " + inputChannels);
            logger.Debug("Enabled: " + mEnableFilter);
            logger.Debug("Output channels: " + outputChannels);
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
            keySequenceActive = false;
            keySequenceWasPressed = false;

            // check if the filter is enabled
            if (mEnableFilter) {

                // init the databuffer
                mDataBuffer = new BoolRingBuffer((uint)mSequence.Length);

            }

        }

        public void start() {

            // set the key-sequence as not active
            Globals.setValue<bool>("KeySequenceActive", "0");
            keySequenceActive = false;
            keySequenceWasPressed = false;

        }

        public void stop() {

            // clear the databuffer
            if (mDataBuffer != null)    mDataBuffer.Clear();
            
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
                
                // set boolean based on threshold setting
                bool inValue = (input[filterInputChannel - 1] >= mThreshold);
                
                // add boolean to ringbuffer
                mDataBuffer.Put(inValue);
                
                // check if ringbuffer was filled
                if (mDataBuffer.Fill() == mSequence.Length) {

                    // reset compare counter
                    mCompareCounter = 0;

                    // check the ringbuffer against the keysequence
                    for (int i = 0; i < mDataBuffer.Fill(); ++i) {

                        // check if sequence and input are the same
                        if (mDataBuffer.Data()[i] == mSequence[i])
                            mCompareCounter++;

                    }

                    // check if proportion of comparison between keysequence and ringbuffer is met
                    // set the KeySequenceActive global variable accordingly
                    if ((double)mCompareCounter / mSequence.Length >= mProportionCorrect)
                        keySequenceActive = true;
                    else
                        keySequenceActive = false;

                    // check if the escapestate has changed
                    if (keySequenceActive != keySequenceWasPressed) {

                        // set the global
                        Globals.setValue<bool>("KeySequenceActive", (keySequenceActive ? "1" : "0"));

                        // log if the escapestate has changed
                        Data.logEvent(1, "KeySequenceChange", (keySequenceActive) ? "1" : "0");

                        // update the flag
                        keySequenceWasPressed = keySequenceActive;

                    }

                    

                } else {

                    // TODO: setValue is not always necessary, only call setValue if the value (locally stored) changes
                    Globals.setValue<bool>("KeySequenceActive", "0");
                    keySequenceActive = false;
                    keySequenceWasPressed = false;

                }

            }
            
            // pass the input straight through as output
            for (uint channel = 0; channel < inputChannels; ++channel) output[channel] = input[channel];

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
