using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UNP;
using UNP.Applications;
using UNP.Core.Helpers;
using UNP.Core.Params;

namespace EmptyTask {

    public class EmptyTask : IApplication {

        private static Logger logger = LogManager.GetLogger("EmptyTask");                        // the logger object for the view
        private static Parameters parameters = ParameterManager.GetParameters("EmptyTask", Parameters.ParamSetTypes.Application);

        private EmptyView mSceneThread = null;


        public EmptyTask() {

            // define the parameters
            parameters.addParameter<bool>(
                "Test",
                "test Description",
                "1");


        }

        public bool configure(ref SampleFormat input) {
            return true;
        }

        public void initialize() {
            
            // check the scene (thread) already exists, stop and clear the old one.
            if (mSceneThread != null)   destroyScene();

            //
            mSceneThread = new EmptyView(60, 0, 0, 800, 600, false);


            // start the scene thread
            //if (mSceneThread != null) mSceneThread.start();
            mSceneThread.start();

        }

        public void start() {
            
        }

        public void stop() {

        }

        public bool isStarted() {
            return true;
        }

        public void process(double[] input) {

        }

        public void destroy() {
            destroyScene();
        }


        private void destroyScene() {
	
	        // check if a scene thread still exists
	        if (mSceneThread != null) {

		        // stop the animation thread (stop waits until the thread is finished)
                mSceneThread.stop();

	        }

	        // delete the thread
	        mSceneThread = null;

        }

    }
}
