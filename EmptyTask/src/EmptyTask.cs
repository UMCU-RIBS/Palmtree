using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UNP.Applications;
using UNP.Core.Helpers;
using UNP.Core.Params;

namespace EmptyTask {

    public class EmptyTask : IApplication {

        private const int CLASS_VERSION = 1;
        private const string CLASS_NAME = "EmptyTask";

        private static Logger logger = LogManager.GetLogger(CLASS_NAME);                        // the logger object for the view
        private static Parameters parameters = ParameterManager.GetParameters(CLASS_NAME, Parameters.ParamSetTypes.Application);

        private EmptyView view = null;
        private Object lockView = new Object();                         // threadsafety lock for all event on the view

        public EmptyTask() {

            // define the parameters
            parameters.addParameter<bool>(
                "Test",
                "test Description",
                "1");


        }

        public Parameters getParameters() {
            return parameters;
        }

        public int getClassVersion() {
            return CLASS_VERSION;
        }

        public string getClassName() {
            return CLASS_NAME;
        }

        public bool configure(ref SampleFormat input) {
            return true;
        }

        public void initialize() {
                    
            // lock for thread safety
            lock (lockView) {

                // check the view (thread) already exists, stop and clear the old one.
                destroyView();

                //
                view = new EmptyView(50, 0, 0, 800, 600, false);


                // start the scene thread
                //if (view != null) view.start();
                view.start();

            }

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

            // stop the application
            // Note: At this point stop will probably have been called from the mainthread before destroy, however there is a slight
            // chance that in the future someone accidentally will put something in the configure/initialize that should have
            // actually been put in the start. If start is not called in the mainthread, then stop will also not be called at the
            // modules. For these accidents we do an extra stop here.
            stop();

            // lock for thread safety
            lock (lockView) {
                destroyView();
            }

            // destroy/empty more task variables

        }

        private void destroyView() {
            
            // check if a scene thread still exists
            if (view != null) {

                // stop the animation thread (stop waits until the thread is finished)
                view.stop();

                // release the thread (For collection)
                view = null;

            }

        }

    }
}
