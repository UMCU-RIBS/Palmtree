using NLog;
using System;
using System.Collections.Generic;
using UNP.Core;
using UNP.Core.Helpers;
using UNP.Core.Params;

namespace UNP.Filters {

    public class NormalizerFilter : FilterBase, IFilter {

        private enum AdaptationTypes : int {
            None = 0,
            ZeroMean = 1,
            ZeroMeanUnitVar = 2,
        };

        private new const int CLASS_VERSION = 1;

        private double[] mOffsets = null;                           // array to hold the offset for each channel
        private double[] mGains = null;                             // array to hold the gain for each channel
        private int[] mAdaptation = null;
        private bool mDoAdapt = false;
        private int mBufferSize = 0;                        // time window of past data per buffer that enters into statistic
        private string[][] mBuffers = null;
        private string mUpdateTrigger = null;
        private RingBuffer[][] mDataBuffers = null;           // holds the data for each channel (for the size of buffer)

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

            parameters.addParameter <double[]>  (
                "NormalizerOffsets",
                "Normalizer offsets",
                "", "", "0");

            parameters.addParameter <double[]>  (
                "NormalizerGains",
                "Normalizer gain values",
                "", "", "1");

            parameters.addParameter<int[]>(
                "Adaptation",
                "Adaptation setting per channel: 0 no adaptation, 1 zero mean, 2 zero mean, unit variance",
                "", "", "0");

            parameters.addParameter<string[][]>(
                "Buffers",
                "Defines the buffers.\nThe columns correspond to the output channels. The rows allow for one or more buffers per channel.\nInside the cells (for each buffer) it is possible to define expressions which are evaluated during runtime, global variables can be\nused and evaluated (e.g. [feedback] == 1). if an expression is evaluated as true then the sample will be added accordingly to that buffer.\n\nNote the variablenames in the expressions are case-sensitive",
                "", "", "[Feedback] == 1 && [Target] == 1,[Feedback] == 1 && [Target] == 0");

            parameters.addParameter<double>(
                "BufferLength",
                "Time window of past data per buffer that enters into statistic",
                "", "", "9s");

            parameters.addParameter<string>(
                "UpdateTrigger",
                "Expression (global variables can be used, e.g. [feedback] == 1) on which the offset and gain will be updated, the trigger will occur when the expression changes from 0 to 1.\nUse empty string for continues update.\n\nNote the variablenames in the expressions are case-sensitive",
                "", "", "[Feedback]==0");

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

                // check if there are offsets for each input channel
                double[] newOffsets = newParameters.getValue<double[]>("NormalizerOffsets");
                if (newOffsets.Length < inputChannels) {
                    logger.Error("The number of values in the NormalizerOffsets parameter cannot be less than the number of input channels (as each value gives the offset for each input channel)");
                    return false;
                }

                // check if there are gains for each input channel
                double[] newGains = newParameters.getValue<double[]>("NormalizerGains");
                if (newGains.Length < inputChannels) {
                    logger.Error("The number of values in the NormalizerGains parameter cannot be less than the number of input channels (as each value gives the gain for each input channel)");
                    return false;
                }

                // check if there are gains for each input channel
                int[] newAdaptation = newParameters.getValue<int[]>("Adaptation");
                if (newAdaptation.Length < inputChannels) {
                    logger.Error("The number of values in the Adaptation parameter cannot be less than the number of input channels (as each value gives the adaptation setting for each input channel)");
                    return false;
                }

                // check if adaptation is needed
                bool newDoAdaptation = false;
                for(int channel = 0; channel < inputChannels; channel++)    newDoAdaptation |= (newAdaptation[channel] != (int)AdaptationTypes.None);
                if (newDoAdaptation) {
                    // adaptation is needed

                    // check the buffers parameter
                    string[][] newBuffers = newParameters.getValue<string[][]>("Buffers");
                    if (newBuffers.Length > inputChannels) {
                        logger.Error("The number of columns in the Buffers parameter may not exceed the number of input channels");
                        return false;
                    }
                    // Evaluate all buffer expressions to test for validity.
                    for (int col = 0; col < newBuffers.Length; col++) {
                        for (int row = 0; row < newBuffers[col].Length; row++) {
                            if (!Globals.testExpression(newBuffers[col][row])) {
                                logger.Error("The buffers parameter contains an invalid experession '" + newBuffers[col][row] + "', this expression should be corrected");
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
                for (int channel = 0; channel < inputChannels; channel++) {
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
            logger.Debug("Input channels: " + inputChannels);
            logger.Debug("Enabled: " + mEnableFilter);
            logger.Debug("Output channels: " + outputChannels);
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

        public void initialize() {

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

        }

        public void start() {
            
        }

        public void stop() {
            //double[] newGains = newParameters.getValue<double[]>("NormalizerGains");
            //double[] test = new double[] { 1112, 3334 };
            //parameters.setValue("NormalizerGains", test);

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

                if (mDoAdapt) {
                    logger.Error("-----");
                    for (int channel = 0; channel < mBuffers.Length; ++channel) {
                        for (int buffer = 0; buffer < mBuffers[channel].Length; ++buffer) {
                            bool testResult = Globals.evaluateConditionExpression(mBuffers[channel][buffer]);
                            logger.Debug("channel: " + channel + "   buffer: " + buffer + "    testResult = " + testResult);
                            if (testResult) {
                                mDataBuffers[channel][buffer].Put(input[channel]);
                            }
                        }
                    }
                    /*
                    if (mpUpdateTrigger != NULL) {
                        bool currentTrigger = mpUpdateTrigger->Evaluate(&Input);
                        //bciout << "mPreviousTrigger: " << mPreviousTrigger << endl;
                        //bciout << "currentTrigger: " << currentTrigger << endl;
                        if (currentTrigger && !mPreviousTrigger) {
                            bciout << "update" << endl;
                            Update();

                        }
                        mPreviousTrigger = currentTrigger;
                    } else
                        Update();
                        */


                }


                // loop through every channel
                for (int channel = 0; channel < inputChannels; ++channel) {

                    // normalize
                    output[channel] = (input[channel] - mOffsets[channel]) * mGains[channel];

                }

            } else {
                // filter disabled

                // pass the input straight through
                for (uint channel = 0; channel < inputChannels; ++channel)  output[channel] = input[channel];

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

            /*
            for (size_t channel = 0; channel < mDataBuffers.size(); ++channel)
                if (mAdaptation[channel] != none) { // Compute raw moments for all buffers.
                    size_t numValues = 0;
                    vector<double> bufferMeans;
                    vector<double> bufferSqMeans;
                    for (size_t i = 0; i < mDataBuffers[channel].size(); ++i) {
                        const RingBuffer&buffer = mDataBuffers[channel][i];
                        const RingBuffer::DataVector&data = buffer.Data();
                        double bufferSum = 0,
                               bufferSqSum = 0;
                        for (size_t j = 0; j < buffer.Fill(); ++j) {
                            bufferSum += data[j];
                            bufferSqSum += data[j] * data[j];
                        }
                        numValues += buffer.Fill();
                        if (buffer.Fill() > 0) {
                            bufferMeans.push_back(bufferSum / buffer.Fill());
                            bufferSqMeans.push_back(bufferSqSum / buffer.Fill());
                        }
                    }
                    // Compute total mean and variance from the raw moments.
                    double dataMean = 0;
                    if (!bufferMeans.empty()) { // Use the mean of means to avoid bias.
                        dataMean
                          = accumulate(bufferMeans.begin(), bufferMeans.end(), 0.0)
                            / bufferMeans.size();

                        mOffsets[channel] = static_cast<float>(dataMean);
                        bcidbg << "Channel " << channel
                               << ": Set offset to " << mOffsets[channel] << " using information"
                               << " from " << bufferMeans.size() << " buffers"
                               << endl;
                    }

                    if (mAdaptation[channel] == zeroMeanUnitVariance) { // Normalize to unit variance.
                        double dataSqMean = 0;
                        if (!bufferSqMeans.empty()) { // Again, use the mean of means to avoid bias.
                            dataSqMean
                              = accumulate(bufferSqMeans.begin(), bufferSqMeans.end(), 0.0)
                                / bufferSqMeans.size();
                        }
                        double dataVar = dataSqMean - dataMean * dataMean;
                        const double eps = 1e-10;
                        if (dataVar > eps) {
                            mGains[channel] = 1.0f / ::sqrt(dataVar);
                            bcidbg << "Set gain to " << mGains[channel]
                                   << ", using data variance"
                                   << endl;
                        }
                        bcidbg(2) << "\n"
                                    << "\tmean:    \t" << dataMean << "\n"
                                    << "\tvariance:\t" << dataVar << "\n"
                                    << "\tsamples: \t" << numValues << "\n"
                                    << flush;
                    }
                }
                */
        }

    }

}
