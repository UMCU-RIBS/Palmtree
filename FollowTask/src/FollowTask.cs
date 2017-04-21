using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using UNP;
using UNP.Applications;
using UNP.Core;
using UNP.Core.Helpers;

namespace FollowTask {

    public class FollowTask : IApplication {

		private enum TaskStates:int {
			Wait,
			CountDown,
			Task,
			EndText
		};

        private static Logger logger = LogManager.GetLogger("FollowTask");                        // the logger object for the view

        Random rand = new Random(Guid.NewGuid().GetHashCode());

        private FollowView mSceneThread = null;
        private Object lockView = new Object();                         // threadsafety lock for all event on the view
        private bool mTaskPauzed = false;								// flag to hold whether the task is pauzed (view will remain active, e.g. connection lost)

        private bool mUNPMenuTask = false;								// flag whether the task is started by the UNPMenu
        private bool mUNPMenuTaskStop = false;							// flag to hold whether the task should is stop (setting this to false will notify the UNPMenu that the task should stop)
        private bool mUNPMenuTaskSuspended = false;						// flag to hold whether the task is suspended (view will be destroyed/re-initiated)

        private int mConnectionSoundTimer = 0;							// counter to play a sound when the connection is lost
        private bool mConnectionLost = false;							// flag to hold whether the connection is lost
        private bool mConnectionWasLost = false;						// flag to hold whether the connection has been lost (should be reset after being re-connected)


        // input parameters
        private int mWindowRedrawFreqMax = 0;
        private bool mWindowed = false;
        private int mWindowWidth = 0;
        private int mWindowHeight = 0;
        private int mWindowLeft = 0;
        private int mWindowTop = 0;
        private int mFullscreenMonitor = 0;

        private float mCursorSize = 1f;
        private int mCursorColorRule = 0;
        private RGBColorFloat mCursorColorMiss = new RGBColorFloat();
        private RGBColorFloat mCursorColorHit = new RGBColorFloat();
        private int mCursorColorHitTime = 0;
        private RGBColorFloat mCursorColorEscape = new RGBColorFloat();
        private int mCursorColorEscapeTime = 0;
        private int mCursorColorTimer = 0;

		private List<int> fixedTargetSequence = new List<int>(0);				    // the target sequence (input parameter)
		private int mNumberTargets = 0;
		private int mTargetSpeed = 0;
		private int mTargetYMode = 0;
		private int mTargetWidthMode = 0;
		private int mTargetHeightMode = 0;
        private List<List<float>> mTargets = new List<List<float>>() {              // the block/target definitions (1ste dimention are respectively Ys, Heights, Widths; 2nd dimension blocks options) 
            new List<float>(0), 
            new List<float>(0), 
            new List<float>(0)  
        };          
        private List<String> mTargetTextures = new List<String>(0);			        // the block/target texture definitions (each element gives the texture for each block option, corresponds to the 2nd dimension of targets) 
        private List<int> mTargetSequence = new List<int>(0);					    // the target sequence being used in the task (can either be given by input or generated)
		
		private int mTaskInputChannel = 0;											// input channel
        private int mTaskInputSignalType = 0;										// input signal type (0 = 0 to 1, 1 = -1 to 1)
        private int mTaskFirstRunStartDelay = 0;									// the first run start delay in sample blocks (used as a timer during runtime)
        private int mWaitCounter = 0;
        private int mCountdownCounter = 0;											// the countdown timer

        private int mHitScore = 0;												    // the score of the cursor hitting a block (in number of samples)
		private bool mShowScore = false;



        // task specific variables
        private TaskStates taskState = TaskStates.Wait;
        private TaskStates previousTaskState = TaskStates.Wait;

        private int mCurrentBlock = FollowView.noBlock;	                            // the current block which is in line with X of the cursor (so the middle)
        private float mBlockSpeed = 120;									        // the block movement speed (in pixels per second)






        public FollowTask() {

            // define the parameters
            //Parameters.addParameter("Test", "0", "1", "1");


        }

        public bool configure(ref SampleFormat input) {
            
            
            
            // debug, now using UNPMenu settings


	        // set the task as being standalone
	        mUNPMenuTask = false;

	        // transfer the window settings
	        mWindowRedrawFreqMax = 60;
	        mWindowed = true;
	        mWindowWidth = 800;
	        mWindowHeight = 600;
	        mWindowLeft = 0;
	        mWindowTop = 0;
	        mFullscreenMonitor = 0;

	        // set the UNP task standard settings
	        mShowScore = true;
	        mTaskInputSignalType = 1;
	        mTaskInputChannel = 1;
	        mTaskFirstRunStartDelay = 10;
	        mCursorSize = 4f;
	        mCursorColorRule = 0;
            mCursorColorMiss = new RGBColorFloat(0.8f, 0f, 0f);
            mCursorColorHit = new RGBColorFloat(0.8f, 0.8f, 0f);
	        mCursorColorHitTime = 0;
            mCursorColorEscape = new RGBColorFloat(0.8f, 0f, 0.8f);
	        mCursorColorEscapeTime = 0;
	        mNumberTargets = 70;
	        mTargetSpeed = 120;
	        mTargetYMode = 3;
	        mTargetWidthMode = 1;
	        mTargetHeightMode = 1;


	        mTargets[0].Clear();	mTargets[0] = new List<float>(new float[6]);
	        mTargets[1].Clear();	mTargets[1] = new List<float>(new float[6]);
	        mTargets[2].Clear();	mTargets[2] = new List<float>(new float[6]);
	        mTargets[0][0] = 25;	mTargets[1][0] = 50;	mTargets[2][0] = 2;
	        mTargets[0][1] = 25;	mTargets[1][1] = 50;	mTargets[2][1] = 2;
	        mTargets[0][2] = 25;	mTargets[1][2] = 50;	mTargets[2][2] = 2;
	        mTargets[0][3] = 75;	mTargets[1][3] = 50;	mTargets[2][3] = 3;
	        mTargets[0][4] = 75;	mTargets[1][4] = 50;	mTargets[2][4] = 5;
	        mTargets[0][5] = 75;	mTargets[1][5] = 50;	mTargets[2][5] = 7;


            mTargetTextures = new List<String>(new String[6]);
	        mTargetTextures[0] = "images/sky.bmp";
	        mTargetTextures[1] = "images/sky.bmp";
	        mTargetTextures[2] = "images/sky.bmp";
	        mTargetTextures[3] = "images/grass.bmp";
	        mTargetTextures[4] = "images/grass.bmp";
	        mTargetTextures[5] = "images/grass.bmp";

            
            
            return true;
        }

        public void initialize() {
                        
            // lock for thread safety
            lock(lockView) {

                // check the scene (thread) already exists, stop and clear the old one.
                destroyScene();

                // create the view
                mSceneThread = new FollowView(mWindowRedrawFreqMax, mWindowLeft, mWindowTop, mWindowWidth, mWindowHeight, false);

                // set the scene background color
                //RGBColor backgroundColor = RGBColor(Parameter("WindowBackgroundColor"));
                // TODO: set background color

            
                // set task specific display attributes 
                mSceneThread.setBlockSpeed(mTargetSpeed);									// target speed
                mSceneThread.setCursorSizePerc(mCursorSize);								// cursor size radius in percentage of the screen height
                mSceneThread.setCursorHitColor(mCursorColorHit);				            // cursor hit color
                mSceneThread.setCursorMissColor(mCursorColorMiss);              			// cursor out color            
                mSceneThread.initBlockTextures(mTargetTextures);							// initialize target textures (do this before the thread start)
                mSceneThread.centerCursor();												// set the cursor to the middle of the screen
                mSceneThread.setFixation(false);											// hide the fixation
                mSceneThread.setCountDown(0);												// hide the countdown

                // check if the cursor rule is set to hitcolor on hit, if so
                // then make the color automatically determined in the Scenethread by it's variable 'mCursorInCurrentBlock',
                // this makes the color update quickly, since the scenethread is executed at a higher frequency
                if (mCursorColorRule == 0) {
                    mSceneThread.setCursorColorSetting(3);
                }

                // start the scene thread
                mSceneThread.start();
            
	            // wait till the resources are loaded or a maximum amount of 30 seconds (30.000 / 50 = 600)
	            int waitCounter = 600;
	            while (!mSceneThread.resourcesLoaded() && waitCounter > 0) {
		            Thread.Sleep(50);
		            waitCounter--;
	            }
            
	            // check if a target sequence is set
	            if (fixedTargetSequence.Count() == 0) {
		            // targetsequence not set in parameters, generate
		
		            // Generate targetlist
		            generateTargetSequence();

	            } else {
		            // targetsequence is set in parameters

		            // clear the targets
		            if (mTargetSequence.Count() != 0)		mTargetSequence.Clear();
                
		            // transfer the targetsequence
                    mTargetSequence = new List<int>(fixedTargetSequence);

	            }
	        
	            // initialize the target sequence
	            mSceneThread.initBlockSequence(mTargetSequence, mTargets);

            }

        }

        public void start() {

            // lock for thread safety
            lock(lockView) {

                if (mSceneThread == null)   return;

	            // reset the score
	            mHitScore = 0;

	            // reset countdown
	            mCountdownCounter = 15;
	
	            if(mTaskFirstRunStartDelay != 0) {
		            // wait

		            // set state to wait
		            setState(TaskStates.Wait);
		
	            } else {
		
		            // countdown
                    setState(TaskStates.CountDown);
	            }

            }

        }

        public void stop() {

            // lock for thread safety
            lock(lockView) {

                // stop the task
                stopTask();

            }

        }

        public bool isStarted() {
            return true;
        }

        public void process(double[] input) {

            /*
            mConnectionLost = (State("ConnectionLost") == 1);
            */

            process(input[mTaskInputChannel - 1]);

        }

        private void process(double input) {

            // lock for thread safety
            lock (lockView) {
                
                if (mSceneThread == null)   return;

                ////////////////////////
                // BEGIN CONNECTION FILTER ACTIONS//
                ////////////////////////

                // check if connection is lost, or was lost
                if (mConnectionLost) {

                    // check if it was just discovered if the connection was lost
                    if (!mConnectionWasLost) {
                        // just discovered it was lost

                        // set the connection as was lost (this also will make sure the lines in this block willl only run once)
                        mConnectionWasLost = true;

                        // pauze the task
                        pauzeTask();

                        /*
			            // show the lost connection warning
			            mSceneThread.setConnectionLost(true);
			            */

                    }

                    // play the caregiver sound every 20 packages
                    if (mConnectionSoundTimer == 0) {
                        /*
			            PlaySound("sounds\\focuson.wav", NULL, SND_FILENAME);
                        */
                        mConnectionSoundTimer = 20;
                    } else
                        mConnectionSoundTimer--;


                } else if (mConnectionWasLost && !mConnectionLost) {
                    // if the connection was lost and is not lost anymore

                    /*
		            // hide the lost connection warning
		            mSceneThread.setConnectionLost(false);
			        */

                    // resume task
                    resumeTask();

                    // reset connection lost variables
                    mConnectionWasLost = false;
                    mConnectionSoundTimer = 0;

                }

                ////////////////////////
                // END CONNECTION FILTER ACTIONS//
                ////////////////////////

	            // check if the task is pauzed, do not process any further if this is the case
	            if (mTaskPauzed)		    return;
                
	            // use the task state
	            switch (taskState) {

		            case TaskStates.Wait:
			            // starting, pauzed or waiting
			
			            if(mWaitCounter == 0) {

				            // start countdown
				            mSceneThread.setCountDown(3);

				            // set the state to countdown
				            setState(TaskStates.CountDown);

			            } else
				            mWaitCounter--;

			            break;

		            case TaskStates.CountDown:
			            // Countdown before start of task
			
			            // check if the task is counting down
			            if (mCountdownCounter > 0) {
				            // still counting down

				            // reduce the countdown timer
				            mCountdownCounter--;
				
				            // show a certain countdown number
					        if (mCountdownCounter > 10)			mSceneThread.setCountDown(3);
					        else if (mCountdownCounter > 5)		mSceneThread.setCountDown(2);
					        else if (mCountdownCounter > 0)		mSceneThread.setCountDown(1);

			            } else {
				            // done counting down

				            // set the current block to no block
				            mCurrentBlock = FollowView.noBlock;

				            // reset the score
				            mHitScore = 0;

				            // set the state to task
				            setState(TaskStates.Task);

			            }

			            break;

		            case TaskStates.Task:

			            // check the input type
			            if (mTaskInputSignalType == 0) {
				            // Normalizer (0 to 1)
                            
				            mSceneThread.setCursorNormY(input);	// setCursorNormY will take care of values below 0 or above 1)
		
			            } else if (mTaskInputSignalType == 1) {
				            // Normalizer (-1 to 1)

				            mSceneThread.setCursorNormY((input + 1.0) / 2.0);

			            } else if (mTaskInputSignalType == 1) {
				            // Constant middle

				            mSceneThread.setCursorNormY(0.5);

			            } else {
				            //mSceneThread->setCursorY(input);

			            }

			            // check if it is the end of the task
			            if (mCurrentBlock == mTargetSequence.Count() - 1 && (mSceneThread.getCurrentBlock() == FollowView.noBlock)) {
				            // end of the task

				            setState(TaskStates.EndText);

			            } else {
				            // not the end of the task

				            // check if the color is based on input
				            if (mCursorColorRule == 1 || mCursorColorRule == 2) {
					            // 1. Hitcolor on input or 
					            // 2. Hitcolor on input - Escape color on escape

					            // check if there is time on the timer left
					            if (mCursorColorTimer > 0) {

						            // count back the timer
						            mCursorColorTimer--;

						            // set the color back to miss if the timer is finished
						            if (mCursorColorTimer == 0)
							            mSceneThread.setCursorColorSetting(0);

					            }

					
					            // check the color rule
					            if (mCursorColorRule == 2) {
						            // 2. Hitcolor on input - Escape color on escape
/*
            #ifndef UNPMENU
						            // check if a keysequence input comes in or a click input comes in
						            if (State("KeySequenceActive") == 1) {

							            // set the color
							            mSceneThread->setCursorColorSetting(2);

							            // set the timer
							            if (mCursorColorEscapeTime == 0)	mCursorColorTimer = 1;
							            else								mCursorColorTimer = mCursorColorEscapeTime;

						            } else {

							            // check if a click was made
							            if (input == 1) {
						
								            // set the color
								            mSceneThread->setCursorColorSetting(1);

								            // set the timer
								            if (mCursorColorHitTime == 0)	mCursorColorTimer = 1;
								            else							mCursorColorTimer = mCursorColorHitTime;

							            }

						            }

            #endif
*/
					            } else {
						            // 1. Hitcolor on input

						            // check if a click was made
						            if (input == 1) {
						
							            // set the color
							            mSceneThread.setCursorColorSetting(1);

							            // set the timer
							            if (mCursorColorHitTime == 0)	mCursorColorTimer = 1;
							            else							mCursorColorTimer = mCursorColorHitTime;

						            }
					            }

				            }

				            // retrieve the current block, whether the cursor is in the block, and (if there is a block) the target index of the block
				            mCurrentBlock = mSceneThread.getCurrentBlock();
				            //bciout << "mCurrentBlock " << mCurrentBlock << endl;

				            // add to score if cursor hits the block
				            if (mSceneThread.getCursorInCurrentBlock()) mHitScore++;

				            // update the score for display
				            if (mShowScore)		mSceneThread.setScore(mHitScore);

			            }

			            break;

		            case TaskStates.EndText:
			            // end text

			            if(mWaitCounter == 0) {
                            /*
				            #ifdef UNPMENU
						
					            if (mUNPMenuTask)	UNP_Stop();

				            #else

					            // suspend BCI2000, this will also call stopTask()
					            if (!mUNPMenuTask)	State( "Running" ) = false;

				            #endif
                            */
			            } else
				            mWaitCounter--;

			            break;
	            }

                /*
	            #ifndef UNPMENU

	            uint16 currentTargetIndex = 9999;
	            uint16 currentTargetY = 9999;
	            float currentTargetWidth = (float)0;
	            uint16 currentTargetHeight = 0;
	            if (mCurrentBlock != FollowSceneThread::noBlock) {
		            currentTargetIndex = mTargetSequence[mCurrentBlock];
		            currentTargetY = (uint16)mTargets[0][mTargetSequence[mCurrentBlock]];
		            currentTargetWidth = mTargets[2][mTargetSequence[mCurrentBlock]];
		            currentTargetHeight = (uint16)mTargets[1][mTargetSequence[mCurrentBlock]];
	            }
	
	            // log
	            State("Log_FirstRunStartDelay").AsUnsigned() = (mTaskFirstRunStartDelay > 0);									// delay running
	            State("Log_Countdown").AsUnsigned() = (taskState == CountDown);													// countdown running
	            State("Log_Task").AsUnsigned() = (taskState == Task);															// task running
	            State("Log_CurrentBlock").AsUnsigned() = mCurrentBlock;															// the current block (index in sequence)
	            State("Log_CursorInCurrentBlock").AsUnsigned() = (mSceneThread->getCursorInCurrentBlock());						// is the cursor in the block
	            State("Log_CurrentTargetIndex").AsUnsigned() = currentTargetIndex;												// the current block's (row)index in the target matrix
	            State("Log_CurrentTargetY").AsUnsigned() = currentTargetY;														// save the current block's Y
	            State("Log_CurrentTargetHeight").AsUnsigned() = currentTargetHeight;											// save the current block's height
	            State("Log_CurrentTargetWidth_f").AsFloat() = currentTargetWidth;												// save the current block's width
	            State("Log_HitScore").AsUnsigned() = mHitScore;																	// save the hitscore
	            State("Log_CursorY").AsSigned() = mSceneThread->getCursorY();													// save the cursor y

	            #endif
                */

            }

        }

        public void destroy() {

            // lock for thread safety
            lock(lockView) {

                destroyScene();

            }
        }


        private void destroyScene() {

	        // check if a scene thread still exists
	        if (mSceneThread != null) {

		        // stop the animation thread (stop waits until the thread is finished)
                mSceneThread.stop();

                // release the thread (For collection)
                mSceneThread = null;

	        }

        }

        // pauzes the task
        private void pauzeTask() {
            if (mSceneThread == null)   return;

	        // set task as pauzed
	        mTaskPauzed = true;

	        // store the previous state
	        previousTaskState = taskState;
	
	        // store the block positions
	        if (previousTaskState == TaskStates.Task) {
		        mSceneThread.storeBlockPositions();
	        }

		    // hide everything
		    mSceneThread.setFixation(false);
		    mSceneThread.setCountDown(0);
		    mSceneThread.setBlocksVisible(false);
		    mSceneThread.setCursorVisible(false);
		    mSceneThread.setBlocksMove(false);
		    mSceneThread.setScore(-1);

        }

        // resumes the task
        private void resumeTask() {
            if (mSceneThread == null)   return;

	        // re-instate the block positions
	        if (previousTaskState == TaskStates.Task) {
		        mSceneThread.loadBlockPositions();
	        }

            // set the previous gamestate
	        setState(previousTaskState);

	        // set task as not longer pauzed
	        mTaskPauzed = false;

        }


        private void setState(TaskStates state) {
            
	        // Set state
	        taskState = state;

	        switch (state) {
		        case TaskStates.Wait:
			        // starting, pauzed or waiting
			
                    /*
				    // hide text if present
				    mSceneThread.setText("");
                    */
				    // show the fixation
				    mSceneThread.setFixation(true);

				    // stop the blocks from moving
				    mSceneThread.setBlocksMove(false);

				    // hide the countdown, blocks, cursor and score
				    mSceneThread.setCountDown(0);
				    mSceneThread.setBlocksVisible(false);
				    mSceneThread.setCursorVisible(false);
				    mSceneThread.setScore(-1);

			        // Set wait counter to startdelay
			        mWaitCounter = mTaskFirstRunStartDelay;

			        break;

		        case TaskStates.CountDown:
			        // countdown when task starts

				/*
				    // hide text if present
				    mSceneThread.setText("");
                    */
				    // hide fixation
				    mSceneThread.setFixation(false);

				    // set countdown
				    if (mCountdownCounter > 10)			mSceneThread.setCountDown(3);
				    else if (mCountdownCounter > 5)		mSceneThread.setCountDown(2);
				    else if (mCountdownCounter > 0)		mSceneThread.setCountDown(1);
				    else								mSceneThread.setCountDown(0);

			        break;


		        case TaskStates.Task:
			        // perform the task

                    /*
				    // hide text if present
				    mSceneThread->setText("");
                    */

				    // hide the countdown counter
				    mSceneThread.setCountDown(0);

				    // set the score for display
				    if (mShowScore)		mSceneThread.setScore(mHitScore);

				    // reset the cursor position
				    mSceneThread.centerCursor();

				    // show the cursor
				    mSceneThread.setCursorVisible(true);

				    // show the blocks and start the blocks animation
				    mSceneThread.setBlocksVisible(true);
				    mSceneThread.setBlocksMove(true);

			        break;

		        case TaskStates.EndText:
			        // show text
			
				    // stop the blocks from moving
				    mSceneThread.setBlocksMove(false);

				    // hide the blocks and cursor
				    mSceneThread.setBlocksVisible(false);
				    mSceneThread.setCursorVisible(false);
                    /*
				    // show text
				    mSceneThread.setText("Exit task");
			        */
			        // set duration for text to be shown at the end
			        mWaitCounter = 15;

			        break;

	        }

        }

        // Stop the task
        private void stopTask() {
            if (mSceneThread == null)   return;

            // set the current block to no block
            mCurrentBlock = FollowView.noBlock;

            // hide countdowns or fixations
            mSceneThread.setFixation(false);
            mSceneThread.setCountDown(0);

            // stop the blocks animation and hide the blocks
            mSceneThread.setBlocksMove(false);
            mSceneThread.setBlocksVisible(false);

            // hide the cursor
            mSceneThread.setCursorVisible(false);

            // hide the score
            mSceneThread.setScore(-1);
                
            // initialize the target sequence already for a possible next run
	        if (fixedTargetSequence.Count() == 0) {

		        // Generate targetlist
		        generateTargetSequence();

	        }

            // initialize the target sequence
	        mSceneThread.initBlockSequence(mTargetSequence, mTargets);

        }


        private void generateTargetSequence() {
	        
	        // clear the targets
	        if (mTargetSequence.Count() != 0)		mTargetSequence.Clear();

	        // create targetsequence array with <NumberTargets>
            mTargetSequence = new List<int>(new int[mNumberTargets]);

	        // put the row indices of each distinct value (from the rows in the matrix) in an array
	        // (this is used for the modes which are set to randomization)
            List<int> catY_unique = new List<int>(0);
            List<List<int>> catY = new List<List<int>>(0);
            List<float> catWidth_unique = new List<float>(0);
            List<List<int>> catWidth = new List<List<int>>(0);
            List<int> catHeight_unique = new List<int>(0);
            List<List<int>> catHeight = new List<List<int>>(0);

            // 
            int i = 0;
            int j = 0;

            // loop through the target rows
	        for (i = 0; i < mTargets[0].Count(); ++i) {

		        // get the values for the row
                int valueY = (int)mTargets[0][i];
		        float valueWidth = mTargets[2][i];
		        int valueHeight = (int)mTargets[1][i];
		
		        // store the unique values and indices
		        for (j = 0; j < catY_unique.Count(); ++j)
			        if (catY_unique[j] == valueY)	break;
		        if (j == catY_unique.Count()) {
			        catY_unique.Add(valueY);						// store the unique value at index j
			        catY.Add(new List<int>(0));				        // store the targets row index in the vector at index j	
						
		        }
		        catY[j].Add(i);

		        for (j = 0; j < catWidth_unique.Count(); ++j)
			        if (catWidth_unique[j] == valueWidth)	break;
		        if (j == (int)catWidth_unique.Count()) {
			        catWidth_unique.Add(valueWidth);				// store the unique value at index j
			        catWidth.Add(new List<int>(0));			        // store the targets row index in the vector at index j							
		        }
		        catWidth[j].Add(i);

		        for (j = 0; j < catHeight_unique.Count(); ++j)
			        if (catHeight_unique[j] == valueHeight)	break;
		        if (j == catHeight_unique.Count()) {
			        catHeight_unique.Add(valueHeight);			    // store the unique value at index j
			        catHeight.Add(new List<int>(0));			    // store the targets row index in the vector at index j							
		        }
		        catHeight[j].Add(i);

	        }

	        // create the arrays to handle the no replace randomization (in case it is needed)
            List<int> catY_noReplace = new List<int>(0);
            List<int> catWidth_noReplace = new List<int>(0);
            List<int> catHeight_noReplace = new List<int>(0);

	        // create random start for each categories (in case it is needed)
	        int catY_randStart = rand.Next(0, catY.Count());
	        int catWidth_randStart = rand.Next(0, catWidth.Count());
	        int catHeight_randStart = rand.Next(0, catHeight.Count());

	        bool catY_randStart_Added = false;
	        bool catWidth_randStart_Added = false;
	        bool catHeight_randStart_Added = false;

	        // create a target sequence
            List<int> currentY = new List<int>(0);          // initial value should be overwritten, but just in case
            List<int> currentWidth = new List<int>(0);
            List<int> currentHeight = new List<int>(0);

	        // loop <NumberTargets> times to generate each target
	        int generateSafetyCounter = mNumberTargets + 1000;
            i = 0;
            while(i < mNumberTargets) {
			
		        // none been added at the beginning of the loop
		        catY_randStart_Added = false;
		        catWidth_randStart_Added = false;
		        catHeight_randStart_Added = false;

		        // count the loops and check for generation
		        if (generateSafetyCounter-- == 0) {
                    logger.Error("Error generating random sequence, the generation rules/parameters (TargetYMode, TargetWidthMode, TargetHeightMode and Target) cause a stalemate");
			        return;
		        }

			
		        // check Y mode
		        if (mTargetYMode == 0) {			// 0: Target(matrix) order
			
			
		        } else if (mTargetYMode == 1) {	// 1: randomize categories
			        currentY = catY[rand.Next(0, catY.Count())];

		        } else if (mTargetYMode == 2) {	// 2:random categories without replacement
				
			        if (catY_noReplace.Count() == 0) {

				        catY_noReplace = new List<int>(new int[catY.Count()]);
				        for (j = 0; j < catY_noReplace.Count(); ++j)	catY_noReplace[j] = j;
					
                        catY_noReplace.Shuffle();
			        }

			        currentY = catY[catY_noReplace[catY_noReplace.Count() - 1]];

                    catY_noReplace.RemoveAt(catY_noReplace.Count -1);

		        } else if (mTargetYMode == 3) {	// 3:sequential categories with rnd start
			
			        currentY = catY[catY_randStart];
			        catY_randStart++;
			        if (catY_randStart == catY.Count())		catY_randStart = 0;
			        catY_randStart_Added = true;

		        }
		
			
		        // check Width mode
		        if (mTargetWidthMode == 0) {			// 0: Target(matrix) order
			
			
		        } else if (mTargetWidthMode == 1) {	// 1: randomize categories
			        currentWidth = catWidth[rand.Next(0, catWidth.Count())];

		        } else if (mTargetWidthMode == 2) {	// 2:random categories without replacement
			        if (catWidth_noReplace.Count() == 0) {
				        
                        catWidth_noReplace = new List<int>(new int[catWidth.Count()]);
				        for (j = 0; j < catWidth_noReplace.Count(); ++j)	catWidth_noReplace[j] = j;
				        
                        catWidth_noReplace.Shuffle();

			        }
			        
                    currentWidth = catWidth[catWidth_noReplace[catWidth_noReplace.Count() - 1]];
                    catWidth_noReplace.RemoveAt(catWidth_noReplace.Count -1);

		        } else if (mTargetWidthMode == 3) {	// 3:sequential categories with rnd start
			        currentWidth = catWidth[catWidth_randStart];
			        catWidth_randStart++;
			        if (catWidth_randStart == catWidth.Count())		catWidth_randStart = 0;
			        catWidth_randStart_Added = true;

		        }

		        // check Height mode
		        if (mTargetHeightMode == 0) {			// 0: Target(matrix) order
			
			
		        } else if (mTargetHeightMode == 1) {	// 1: randomize categories
			        currentHeight = catHeight[rand.Next(0, catHeight.Count())];
                    
		        } else if (mTargetHeightMode == 2) {	// 2:random categories without replacement
			        if (catHeight_noReplace.Count() == 0) {
				        
                        catHeight_noReplace = new List<int>(new int[catHeight.Count()]);
				        for (j = 0; j < catHeight_noReplace.Count(); ++j)	catHeight_noReplace[j] = j;
				        
                        catHeight_noReplace.Shuffle();

			        }
			        currentHeight = catHeight[catHeight_noReplace[catHeight_noReplace.Count() - 1]];
                    catHeight_noReplace.RemoveAt(catHeight_noReplace.Count -1);

		        } else if (mTargetHeightMode == 3) {	// 3:sequential categories with rnd start
			        currentHeight = catHeight[catHeight_randStart];
			        catHeight_randStart++;
			        if (catHeight_randStart == catHeight.Count())		catHeight_randStart = 0;
			        catHeight_randStart_Added = true;

		        }

		        // find a target all modes agree on
		        List<int> currentTarget = new List<int>(new int[mTargets[0].Count()]);
		        for (j = 0; j < currentTarget.Count(); ++j)	currentTarget[j] = j;
                j = 0;
		        while(j < (int)currentTarget.Count()) {

			        // clear out all the target indices which are not in the currentY
			        bool found = false;
			        for (int k = 0; k < currentY.Count(); ++k) {
				        if (currentTarget[j] == currentY[k]) {
					        found = true;	break;
				        }
			        }
			        if (!found && j < currentTarget.Count() && currentTarget.Count() != 0) {
                        currentTarget.Swap(j, currentTarget.Count() - 1);
				        //std::swap(currentTarget[j], currentTarget[currentTarget.Count() - 1]);
                        currentTarget.RemoveAt(currentTarget.Count - 1);
				        continue;
			        }

			        // clear out all the target indices which are not in the currentWidth
			        found = false;
			        for (int k = 0; k < currentWidth.Count(); ++k) {
				        if (currentTarget[j] == currentWidth[k]) {
					        found = true;	break;
				        }
			        }
			        if (!found && currentTarget.Count() != 0) {
				        currentTarget.Swap(j, currentTarget.Count() - 1);
                        //std::swap(currentTarget[j], currentTarget[currentTarget.size() - 1]);
				        currentTarget.RemoveAt(currentTarget.Count - 1);
				        continue;
			        }

			        // clear out all the target indices which are not in the currentHeight
			        found = false;
			        for (int k = 0; k < currentHeight.Count(); ++k) {
				        if (currentTarget[j] == currentHeight[k]) {
					        found = true;	break;
				        }
			        }
			        if (!found && currentTarget.Count() != 0) {
				        currentTarget.Swap(j, currentTarget.Count() - 1);
                        //std::swap(currentTarget[j], currentTarget[currentTarget.size() - 1]);
                        currentTarget.RemoveAt(currentTarget.Count - 1);
				        continue;
			        }

			        // go to the next element
			        j++;

		        }

		        // check if a (agreeable) target has been found
		        if (currentTarget.Count() != 0) {
			        // target found, set in sequence

			        // set it in the sequence
			        mTargetSequence[i] = currentTarget[0];

			        // continue to generate the next target
			        i++;

		        } else {
			        // no target found, revert sequential counters if needed

			        // revert randstart counters
			        if (catY_randStart_Added) {
				        catY_randStart--;
				        if (catY_randStart < 0 )		    catY_randStart = (int)catY.Count() - 1;
			        }
			        if (catWidth_randStart_Added) {
				        catWidth_randStart--;
				        if (catWidth_randStart < 0 )		catWidth_randStart = (int)catWidth.Count() - 1;
			        }
			        if (catHeight_randStart_Added) {
				        catHeight_randStart--;
				        if (catHeight_randStart < 0 )		catHeight_randStart = (int)catHeight.Count() - 1;
			        }
			
		        }

	        }

        }




    }

}
