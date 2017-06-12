using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UNP.Core;
using UNP.Core.Helpers;
using UNP.Core.Params;

namespace UNP.Filters {

    public class NormalizerFilter : FilterBase, IFilter {

        private double[] mOffsets = null;                           // array to hold the offset for each channel
        private double[] mGains = null;                             // array to hold the gain for each channel

        public NormalizerFilter(string filterName) {

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
                "LogSampleStreams",
                "Log the filter's intermediate and output sample streams. See 'Data' tab for more settings on sample stream logging.",
                "0");

            parameters.addParameter <double[]>  (
                "NormalizerOffsets",
                "Normalizer offsets",
                "", "", "0");

            parameters.addParameter <double[]>  (
                "NormalizerGains",
                "Normalizer gain values",
                "", "", "1");


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
            output = new SampleFormat(outputChannels);

            // check the values and application logic of the parameters
            if (!checkParameters(parameters))   return false;

            // transfer the parameters to local variables
            transferParameters(parameters);

            // check the logging of sample streams
            mLogSampleStreams = parameters.getValue<bool>("LogSampleStreams");
            mLogSampleStreamsRuntime = mLogSampleStreams;
            if (mLogSampleStreams) {

                // register the streams
                for (int i = 0; i < outputChannels; i++)
                    Data.RegisterSampleStream(("Normalize_Output_Ch" + (i + 1)), typeof(int));

            }

            // debug output
            logger.Debug("--- Filter configuration: " + filterName + " ---");
            logger.Debug("Input channels: " + inputChannels);
            logger.Debug("Enabled: " + mEnableFilter);
            logger.Debug("Output channels: " + outputChannels);

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

            //
            // no pre-check on the number of output channels is needed here, the number of output
            // channels will remain the some regardsless to the filter being enabled or disabled
            // 

            // check the values and application logic of the parameters
            if (!checkParameters(newParameters)) return false;

            // retrieve and check the LogSampleStream parameter
            bool newLogSampleStreams = newParameters.getValue<bool>("LogSampleStreams");
            if (!mLogSampleStreams && newLogSampleStreams) {
                // logging was (in the initial configuration) switched off and is trying to be switched on
                // (refuse, it cannot be switched on, because sample streams have to be registered during the first configuration)

                // message
                logger.Error("Cannot switch the logging of samples stream on because it was initially switched off (and streams need to be registered during the first configuration, logging is refused");

                // return failure
                return false;

            }

            // transfer the parameters to local variables
            transferParameters(newParameters);

            // apply change in the logging of sample streams
            if (mLogSampleStreams && mLogSampleStreamsRuntime && !newLogSampleStreams) {
                // logging was (in the initial configuration) switched on and is currently on but wants to be switched off (resulting in 0's being output)

                // message
                logger.Debug("Logging of sample streams was switched on but is now switched off, only zeros will be logged");

                // switch logging off (to zeros)
                mLogSampleStreamsRuntime = false;

            } else if (mLogSampleStreams && !mLogSampleStreamsRuntime && newLogSampleStreams) {
                // logging was (in the initial configuration) switched on and is currently off but wants to be switched on (resume logging)

                // message
                logger.Debug("Logging of sample streams was switched off but is now switched on, logging is resumed");

                // switch logging on
                mLogSampleStreamsRuntime = true;

            }

            // TODO: take resetFilter into account (currently always resets the buffers on initialize

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

            }

        }

        public void initialize() {

            // check if the filter is enabled
            if (mEnableFilter) {

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
                for (int channel = 0; channel < inputChannels; ++channel) {

                    // normalize
                    output[channel] = input[channel] - mOffsets[channel] * mGains[channel];

                }


            } else {
                // filter disabled

                // pass the input straight through
                for (uint channel = 0; channel < inputChannels; ++channel)  output[channel] = input[channel];

            }

            // check if the sample streams should be logged (initial setting)
            if (mLogSampleStreams) {

                // check if the logging of sample streams is needed/allowed during runtime
                if (mLogSampleStreamsRuntime) {
                    // enabled initially and at runtime

                    // output values
                    for (uint channel = 0; channel < inputChannels; ++channel)
                        Data.LogSample(output[channel]);

                } else {
                    // enabled initially but not at runtime

                    // output zeros
                    for (uint channel = 0; channel < inputChannels; ++channel)
                        Data.LogSample(0.0);

                }

            }

        }

        public void destroy() {

        }

    }


}
