using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UNP;
using UNP.Applications;
using UNP.Core.Helpers;

namespace EmptyTask {

    public class EmptyTask : IApplication {

        private static Logger logger = LogManager.GetLogger("EmptyTask");                        // the logger object for the view


        private EmptyView mSceneThread = null;


        public EmptyTask() {

            // define the parameters
            Parameters.addParameter("Test", "0", "1", "1");


        }

        public bool configure(ref SampleFormat input) {
            return true;
        }

        public void initialize() {
            
            // check the scene (thread) already exists, stop and clear the old one.
            if (mSceneThread != null)   destroyScene();

            /*
            // set or calculate the update frequency
            if (mWindowRedrawFreqMax == 0)
                mDisplayRedrawFrequency = 0;
            else
                mDisplayRedrawFrequency = (1000 / mWindowRedrawFreqMax);

	        // check windowed or fullscreen
	        if (mWindowed) {
		        // window

		        // create and start the animation thread
		        mSceneThread = new FollowSceneThread(mDisplayRedrawFrequency, mWindowWidth, mWindowHeight, true, 0);

		        // set the window to it's position (before starting)
		        mSceneThread->moveWindow(mWindowLeft, mWindowTop);

	        } else {
		        // fullscreen
		
		        // create and start the animation thread (fullscreen on <FullscreenMonitor>)
		        mSceneThread = new FollowSceneThread(mDisplayRedrawFrequency, mWindowWidth, mWindowHeight, false, mFullscreenMonitor);
	        }
	        */

            mSceneThread = new EmptyView(60, 0, 0, 800, 600, false);
            

            // set the scene background color
            //RGBColor backgroundColor = RGBColor(Parameter("WindowBackgroundColor"));
            // TODO: set background color

            /*
            // set task specific display attributes 
            if (mSceneThread != NULL)
            {
                mSceneThread->setBlockSpeed(mTargetSpeed);									// target speed
                mSceneThread->setCursorSizePerc(mCursorSize);								// cursor size radius in percentage of the screen height
                mSceneThread->setCursorHitColor((mCursorColorHit.R() / 255.0),				// cursor hit color
                                                (mCursorColorHit.G() / 255.0),
                                                (mCursorColorHit.B() / 255.0));
                mSceneThread->setCursorMissColor((mCursorColorMiss.R() / 255.0),			// cursor out color
                                                (mCursorColorMiss.G() / 255.0),
                                                (mCursorColorMiss.B() / 255.0));
                mSceneThread->initBlockTextures(mTargetTextures);							// initialize target textures (do this before the thread start)
                mSceneThread->centerCursor();												// set the cursor to the middle of the screen
                mSceneThread->setFixation(false);											// hide the fixation
                mSceneThread->setCountDown(0);												// hide the countdown

                // check if the cursor rule is set to hitcolor on hit, if so
                // then make the color automatically determined in the Scenethread by it's variable 'mCursorInCurrentBlock',
                // this makes the color update quickly, since the scenethread is executed at a higher frequency
                if (mCursorColorRule == 0)
                {
                    mSceneThread->setCursorColorSetting(3);
                }

            }
            */

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
