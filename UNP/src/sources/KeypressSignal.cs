using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using UNP.helpers;

namespace UNP.sources {

    class KeypressSignal : ISource {

        private static Logger logger = LogManager.GetLogger("KeypressSignal");
        private static Parameters parameters = ParameterManager.GetParameters("KeypressSignal", Parameters.ParamSetTypes.Source);

        private MainThread pipeline = null;

	    public KeypressSignal(MainThread pipeline) {

            // set the reference to the pipeline
            this.pipeline = pipeline;
            /*
            // define the parameters
            Parameters.addParameter("SourceChannels", "0", "%", "0");
            */
            // start a new thread
            Thread thread = new Thread(this.run);
            thread.Start();

        }
        
        public Parameters getParameters() {
            return parameters;
        }

        public bool configure(out SampleFormat output) {
            
            // create a sampleformat
            output = new SampleFormat(2);

            return true;

        }

        public void initialize() {

        }


        /**
         * function to retrieve the number of samples per second
         * 
         * This value could be requested by the main thread and is used to allow parameters
         * to be converted from seconds to samples
         **/
        public int getSamplesPerSecond() {
            return 0;
        }


	    /**
	     * Start
	     */
        public void start() {
            
        }

	    /**
	     * Stop
	     */
	    public void stop() {



	    }

	    /**
	     * Returns whether the signalgenerator is generating signal
	     * 
	     * @return Whether the signal generator is started
	     */
	    public bool isStarted() {

            return false;
	    }


	    /**
	     * 
	     */
	    public void destroy() {
		
		
	    }
	
	    /**
	     * Returns whether the source thread is still running
	     * Note, this is something different than actually generating
	     * 
	     * @return Whether the source thread is running
	     */
	    public bool isRunning() {
		    return false;
	    }


	    /**
	     * Collector running thread
	     */
        private void run() {


        }


    }

}
