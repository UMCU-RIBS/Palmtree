using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using UNP.Core;
using UNP.Core.Helpers;
using UNP.Core.Params;

namespace UNP.Sources {

    public class PlaybackSignal : ISource {

        private static Logger logger = LogManager.GetLogger("PlaybackSignal");
        private static Parameters parameters = ParameterManager.GetParameters("PlaybackSignal", Parameters.ParamSetTypes.Source);

        private MainThread main = null;

	    public PlaybackSignal(MainThread main) {

            // set the reference to the main
            this.main = main;
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
        public double getSamplesPerSecond() {
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

            // stop source
            // Note: At this point stop will probably have been called from the mainthread before destroy, however there is a slight
            // chance that in the future someone accidentally will put something in the configure/initialize that should have
            // actually been put in the start. If start is not called in the mainthread, then stop will also not be called at the
            // modules. For these accidents we do an extra stop here.
            stop();

            // clear the reference to the mainthread
            main = null;

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
