/**
 * EmptyTask class
 * 
 * ...
 * 
 * 
 * Copyright (C) 2022:  RIBS group (Nick Ramsey Lab), University Medical Center Utrecht (The Netherlands) & external contributors
 * Author(s):           Max van den Boom            (info@maxvandenboom.nl)
 * 
 * This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software
 * Foundation, either version 3 of the License, or (at your option) any later version. This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for
 * more details. You should have received a copy of the GNU General Public License along with this program. If not, see <https://www.gnu.org/licenses/>.
 */
using NLog;
using System;

using Palmtree.Applications;
using Palmtree.Core;
using Palmtree.Core.Params;

namespace EmptyTask {

    /// <summary>
    /// EmptyTask class
    /// 
    /// ...
    /// </summary>
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

        public bool configure(ref SamplePackageFormat input) {
            return true;
        }

        public bool initialize() {
                    
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

            // return success
            return true;

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
