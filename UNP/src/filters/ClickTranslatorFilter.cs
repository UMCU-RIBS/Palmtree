using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UNP.helpers;

namespace UNP.filters {

    class ClickTranslatorFilter : IFilter {

        private static Logger logger = LogManager.GetLogger("ClickTranslator");
        private static Parameters parameters = ParameterManager.GetParameters("ClickTranslator");

        private bool mEnableFilter = false;

        private uint inputChannels = 0;
        private uint outputChannels = 0;

        
        private uint activePeriod = 0;                              // time window of buffer used for determining clicks
        private uint mBufferSize = 0;                               // now equals the activeperiod variable, can be used to enlarge the buffer but only use the last part (activeperiod)
        private uint startActiveBlock = 0;                          // now is always 0 since the activeperiod and buffersize are equal, is used when the buffer is larger than the activeperiod
        private double activeRateThreshold = 0;                     // 
        private uint mRefractoryPeriod = 0;                         // the refractory period that should be waited before a new click can be triggered
        private uint mRefractoryCounter = 0;                        // counter to count down the samples for refractory

        private RingBuffer[] mDataBuffers = null;                   // an array of ringbuffers, a ringbuffer for every channel
        private bool active_state = true;


        public ClickTranslatorFilter() {

            // define the parameters
            



        }
        
        public Parameters getParameters() {
            return parameters;
        }

        public bool configure(ref SampleFormat input, out SampleFormat output) {
            // TODO: preflight checks (use sample size, channels and parameters)

            // set if the filter is enabled
            mEnableFilter = true;

            // store the number of input channels, set the number of output channels as the same
            inputChannels = input.getNumberOfChannels();
            outputChannels = inputChannels;

            // 
            activePeriod = (uint)SampleConversion.timeToSamples(1); // 1s
            mBufferSize = activePeriod;
            startActiveBlock = mBufferSize - activePeriod;
            activeRateThreshold = 1;

            mRefractoryPeriod = (uint)SampleConversion.timeToSamples(3.6); // 3.6s

		




            // create an output sampleformat
            output = new SampleFormat(outputChannels);


            return true;

        }

        public void initialize() {


            // create the data buffers
            mDataBuffers = new RingBuffer[inputChannels];
            for (uint i = 0; i < inputChannels; i++) mDataBuffers[i] = new RingBuffer(mBufferSize);

            // set the state initially to active (not refractory)
            active_state = true;

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
				    for(uint j = startActiveBlock; j < data.Count(); ++j ) {        // deliberately using Count here, we want to take the entire size of the buffer, not just the (ringbuffer) filled ones
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

        }

        public void destroy() {

        }
    }

}
