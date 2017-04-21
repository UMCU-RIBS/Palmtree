using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UNP.Core.Helpers;
using UNP.Core.Params;

namespace UNP.Filters {

    class ClickTranslatorFilter : IFilter {

        private static Logger logger = LogManager.GetLogger("ClickTranslator");
        private static Parameters parameters = ParameterManager.GetParameters("ClickTranslator", Parameters.ParamSetTypes.Filter);

        private bool mEnableFilter = false;

        private uint inputChannels = 0;
        private uint outputChannels = 0;

        
        private int activePeriod = 0;                               // time window of buffer used for determining clicks
        private int mBufferSize = 0;                                // now equals the activeperiod variable, can be used to enlarge the buffer but only use the last part (activeperiod)
        private int startActiveBlock = 0;                           // now is always 0 since the activeperiod and buffersize are equal, is used when the buffer is larger than the activeperiod
        private double activeRateThreshold = 0;                     // 
        private int mRefractoryPeriod = 0;                          // the refractory period that should be waited before a new click can be triggered
        private int mRefractoryCounter = 0;                         // counter to count down the samples for refractory

        private RingBuffer[] mDataBuffers = null;                   // an array of ringbuffers, a ringbuffer for every channel
        private bool active_state = true;


        public ClickTranslatorFilter() {

            // define the parameters
             parameters.addParameter <bool>      (
                "EnableFilter",
                "Enable AdaptationFilter",
                "1");

            parameters.addParameter <bool>      (
                "WriteIntermediateFile",
                "Write filter input and output to file",
                "0");

            parameters.addParameter <double>       (
                "ActivePeriod",
                "Time window of buffer used for determining clicks (in samples or seconds)",
                "1s", "", "1s");

            parameters.addParameter <double>    (
                "ActiveRateClickThreshold",
                "The threshold above which the average value (of ActivePeriod) in active state should get to send a 'click' and put the filter into inactive state.",
                "0", "1", ".5");

            parameters.addParameter<double>        (
                "RefractoryPeriod",
                "Time window after click in which no click will be translated (in samples or seconds)",
                "1s", "", "3.6s");

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

            // filter is enabled/disabled
            mEnableFilter = parameters.getValue<bool>("EnableFilter");

            // check if the filter is enabled
            if (mEnableFilter) {

                // retrieve the activeperiod
                activePeriod = parameters.getValueInSamples("ActivePeriod");
                mBufferSize = activePeriod;
                startActiveBlock = mBufferSize - activePeriod;
                if (activePeriod < 1) {
                    logger.Error("The ActivePeriod parameter specifies a zero-sized buffer");
                    return false;
                }

                // retrieve active rate threshold
                activeRateThreshold = parameters.getValue<double>("ActiveRateClickThreshold");
			    if (activeRateThreshold > 1 || activeRateThreshold < 0) {
                    logger.Error("The ActiveRateClickThreshold is outside [0 1]");
                    return false;
                }

                // retrieve the refractory period
                mRefractoryPeriod = parameters.getValueInSamples("RefractoryPeriod");
                if (mRefractoryPeriod < 1) {
                    logger.Error("The InactivePeriod parameter must be at least 1 sampleblock");
                    return false;
                }

		    }

            return true;

        }

        public void initialize() {

            // check if the filter is enabled
            if (mEnableFilter) {

                // create the data buffers
                mDataBuffers = new RingBuffer[inputChannels];
                for (uint i = 0; i < inputChannels; i++) mDataBuffers[i] = new RingBuffer((uint)mBufferSize);

                // set the state initially to active (not refractory)
                active_state = true;

            }

        }

        
        public void start() {
            return;
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
                            mRefractoryCounter = mRefractoryPeriod;
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
                        mRefractoryCounter--;

				        if (mRefractoryCounter == 0) {
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

        }

        public void destroy() {

        }
    }

}
