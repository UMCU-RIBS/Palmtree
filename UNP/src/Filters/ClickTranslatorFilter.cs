using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UNP.Core;
using UNP.Core.Helpers;
using UNP.Core.Params;

namespace UNP.Filters {

    public class ClickTranslatorFilter : FilterBase, IFilter {

        private new const int CLASS_VERSION = 0;

        private int activePeriod = 0;                               // time window of buffer used for determining clicks
        private int mBufferSize = 0;                                // now equals the activeperiod variable, can be used to enlarge the buffer but only use the last part (activeperiod)
        private int startActiveBlock = 0;                           // now is always 0 since the activeperiod and buffersize are equal, is used when the buffer is larger than the activeperiod
        private double activeRateThreshold = 0;                     // 
        private int refractoryPeriod = 0;                           // the refractory period that should be waited before a new click can be triggered
        private int refractoryCounter = 0;                          // counter to count down the samples for refractory

        private RingBuffer[] mDataBuffers = null;                   // an array of ringbuffers, a ringbuffer for every channel
        private bool active_state = true;

        public ClickTranslatorFilter(string filterName) {

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
                "Enable click translator filter",
                "1");

            parameters.addParameter <bool>      (
                "LogDataStreams",
                "Log the filter's intermediate and output data streams. See 'Data' tab for more settings on sample stream logging.",
                "0");

            parameters.addParameter <double>       (
                "ActivePeriod",
                "Time window of buffer used for determining clicks (in samples or seconds)",
                "1s", "", "0.4s");

            parameters.addParameter <double>    (
                "ActiveRateClickThreshold",
                "The threshold above which the average value (of ActivePeriod) in active state should get to send a 'click' and put the filter into inactive state.",
                "0", "1", ".5");

            parameters.addParameter<double>        (
                "RefractoryPeriod",
                "Time window after click in which no click will be translated (in samples or seconds)",
                "1s", "", "3.6s");

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

            // 
            // TODO: parameters.checkminimum, checkmaximum

            // filter is enabled/disabled
            bool newEnableFilter = newParameters.getValue<bool>("EnableFilter");

            // check if the filter is enabled
            if (newEnableFilter) {

                // check the activeperiod
                int newActivePeriod = newParameters.getValueInSamples("ActivePeriod");
                if (newActivePeriod < 1) {
                    logger.Error("The ActivePeriod parameter specifies a zero-sized buffer");
                    return false;
                }

                // check the active rate threshold
                double newActiveRateThreshold = newParameters.getValue<double>("ActiveRateClickThreshold");
			    if (newActiveRateThreshold > 1 || newActiveRateThreshold < 0) {
                    logger.Error("The ActiveRateClickThreshold is outside [0 1]");
                    return false;
                }

                // check the refractory period
                int newRefractoryPeriod = newParameters.getValueInSamples("RefractoryPeriod");
                if (newRefractoryPeriod < 1) {
                    logger.Error("The InactivePeriod parameter must be at least 1 sampleblock");
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

                // store the activeperiod
                activePeriod = newParameters.getValueInSamples("ActivePeriod");
                mBufferSize = activePeriod;
                startActiveBlock = mBufferSize - activePeriod;

                // store the active rate threshold
                activeRateThreshold = newParameters.getValue<double>("ActiveRateClickThreshold");

                // store the refractory period
                refractoryPeriod = newParameters.getValueInSamples("RefractoryPeriod");

            }



        }

        private void printLocalConfiguration() {

            // debug output
            logger.Debug("--- Filter configuration: " + filterName + " ---");
            logger.Debug("Input channels: " + inputChannels);
            logger.Debug("Enabled: " + mEnableFilter);
            logger.Debug("Output channels: " + outputChannels);
            if (mEnableFilter) {
                logger.Debug("ActivePeriod: " + activePeriod);
                logger.Debug("ActiveRateClickThreshold: " + activeRateThreshold);
                logger.Debug("RefractoryPeriod: " + refractoryPeriod);
            }

        }

        public void initialize() {

            // check if the filter is enabled
            if (mEnableFilter) {

                // create the data buffers
                mDataBuffers = new RingBuffer[inputChannels];
                for (uint i = 0; i < inputChannels; i++) mDataBuffers[i] = new RingBuffer((uint)mBufferSize);

                // set the state initially to active (not refractory)
                active_state = true;

                // reset the refractory period
                refractoryCounter = 0;

            }

        }

        public void start() {

            // set the state initially to active (not refractory)
            active_state = true;

            // reset the refractory period
            refractoryCounter = 0;

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

		        //loop over channels and samples
		        for( int channel = 0; channel < inputChannels; ++channel ) {	

			        //add new sample to buffer
			        mDataBuffers[channel].Put(input[channel]);

                    //extract buffer
                    double[] data = mDataBuffers[channel].Data();

			        //if ready for click (active state)
			        if (active_state) {
				        // active state

				        //compute average over active time-window length
				        double activeRate = 0;
				        for(int j = startActiveBlock; j < data.Count(); ++j ) {        // deliberately using Count here, we want to take the entire size of the buffer, not just the (ringbuffer) filled ones
					        activeRate += data[j];
				        }
				        activeRate /= (mBufferSize - startActiveBlock);

				        //compare average to active threshold 
				        // the first should always be 1
				        if ((activeRate >= activeRateThreshold) && (data[0] == 1)) {
					
					        output[channel] = 1;
					        active_state = false;
                            refractoryCounter = refractoryPeriod;
					        //State( "ReadyForClick" ) = 0;
					        //State( "Clicked" ) = 1;

				        } else {
					
					        output[channel] = 0;
					        //State( "Clicked" ) = 0;

				        }
				
			        } else { 
				        // recovery mode (inactive state)

				        // inactive_state stops after set refractory period
				        output[channel] = 0;
				        //State( "Clicked" ) = 0;
                        refractoryCounter--;

				        if (refractoryCounter == 0) {
					        active_state = true;
					        //State( "ReadyForClick" ) = 1;
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
