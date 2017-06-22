using NLog;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using UNP;
using UNP.Applications;
using UNP.Core;
using UNP.Core.Helpers;
using UNP.Core.Params;

namespace FollowTask {

    public class FollowTask : IApplication, IApplicationUNP {

		private enum TaskStates:int {
			Wait,
			CountDown,
			Task,
			EndText
		};

        private const int CLASS_VERSION = 0;
        private const string CLASS_NAME = "FollowTask";

        private static Logger logger = LogManager.GetLogger(CLASS_NAME);                        // the logger object for the view
        private static Parameters parameters = null;

        private uint inputChannels = 0;
        private FollowView mSceneThread = null;

        Random rand = new Random(Guid.NewGuid().GetHashCode());
        private Object lockView = new Object();                         // threadsafety lock for all event on the view
        private bool mTaskPauzed = false;								// flag to hold whether the task is pauzed (view will remain active, e.g. connection lost)

        private bool mUNPMenuTask = false;								// flag whether the task is created by the UNPMenu
        private bool mUNPMenuTaskRunning = false;						// flag to hold whether the task should is running (setting this to false is also used to notify the UNPMenu that the task is finished)
        private bool mUNPMenuTaskSuspended = false;						// flag to hold whether the task is suspended (view will be destroyed/re-initiated)

        private int mConnectionSoundTimer = 0;							// counter to play a sound when the connection is lost
        private bool mConnectionLost = false;							// flag to hold whether the connection is lost
        private bool mConnectionWasLost = false;						// flag to hold whether the connection has been lost (should be reset after being re-connected)


        // task input parameters
        private int mWindowLeft = 0;
        private int mWindowTop = 0;
        private int mWindowWidth = 800;
        private int mWindowHeight = 600;
        private int mWindowRedrawFreqMax = 0;
        private RGBColorFloat mWindowBackgroundColor = new RGBColorFloat(0f, 0f, 0f);
        //private bool mWindowed = true;
        //private int mFullscreenMonitor = 0;

        private double mCursorSize = 1f;
        private int mCursorColorRule = 0;
        private RGBColorFloat mCursorColorMiss = new RGBColorFloat(0.8f, 0f, 0f);
        private RGBColorFloat mCursorColorHit = new RGBColorFloat(0.8f, 0.8f, 0f);
        private int mCursorColorHitTime = 0;
        private RGBColorFloat mCursorColorEscape = new RGBColorFloat(0.8f, 0f, 0.8f);
        private int mCursorColorEscapeTime = 0;
        private int mCursorColorTimer = 0;

		private int[] fixedTargetSequence = new int[0];				                // the target sequence (input parameter)
		private int numTargets = 0;
		private int mTargetSpeed = 0;
		private int mTargetYMode = 0;
		private int mTargetWidthMode = 0;
		private int mTargetHeightMode = 0;
        private List<List<float>> mTargets = new List<List<float>>() {              // the block/target definitions (1ste dimention are respectively Ys, Heights, Widths; 2nd dimension blocks options) 
            new List<float>(0), 
            new List<float>(0), 
            new List<float>(0)  
        };          
        private List<string> mTargetTextures = new List<string>(0);			        // the block/target texture definitions (each element gives the texture for each block option, corresponds to the 2nd dimension of targets) 
		
		private int mTaskInputChannel = 1;											// input channel
        private int mTaskInputSignalType = 0;										// input signal type (0 = 0 to 1, 1 = -1 to 1)
        private int mTaskFirstRunStartDelay = 0;                                    // the first run start delay in sample blocks
        private int mTaskStartDelay = 0;									        // the run start delay in sample blocks


        // task (active) variables
        private List<int> mTargetSequence = new List<int>(0);					    // the target sequence being used in the task (can either be given by input or generated)

        private int mWaitCounter = 0;
        private int mCountdownCounter = 0;											// the countdown timer

        private int mHitScore = 0;												    // the score of the cursor hitting a block (in number of samples)
		private bool mShowScore = false;

        private TaskStates taskState = TaskStates.Wait;
        private TaskStates previousTaskState = TaskStates.Wait;
        private int mCurrentBlock = FollowView.noBlock;	                            // the current block which is in line with X of the cursor (so the middle)


        public FollowTask() : this(false) { }
        public FollowTask(bool UNPMenuTask) {

            // transfer the UNP menu task flag
            mUNPMenuTask = UNPMenuTask;

            // check if the task is standalone (not unp menu)
            if (!mUNPMenuTask) {
            
                // create a parameter set for the task
                parameters = ParameterManager.GetParameters("FollowTask", Parameters.ParamSetTypes.Application);

                // define the parameters
                parameters.addParameter<int>(
                    "WindowLeft",
                    "Screen coordinate of application window's left edge",
                    "", "", "0");

                parameters.addParameter<int>(
                    "WindowTop",
                    "Screen coordinate of application window's top edge",
                    "", "", "0");

                parameters.addParameter<int>(
                    "WindowWidth",
                    "Width of application window (fullscreen and 0 will take monitor resolution)",
                    "", "", "800");

                parameters.addParameter<int>(
                    "WindowHeight",
                    "Height of application window (fullscreen and 0 will take monitor resolution)",
                    "", "", "600");

                parameters.addParameter<int>(
                    "WindowRedrawFreqMax",
                    "Maximum display redraw interval in FPS (0 for as fast as possible)",
                    "0", "", "60");

                parameters.addParameter<RGBColorFloat>(
                    "WindowBackgroundColor",
                    "Window background color",
                    "", "", "0");

                /*
                parameters.addParameter <int>       (
                    "Windowed",
                    "Window or Fullscreen - fullscreen is only applied with two monitors",
                    "0", "1", "1", new string[] {"Fullscreen", "Window"});

                parameters.addParameter <int>       (
                    "FullscreenMonitor",
                    "Full screen Monitor",
                    "0", "1", "1", new string[] {"Monitor 1", "Monitor 2"});
                */

                parameters.addParameter<int>(
                    "TaskFirstRunStartDelay",
                    "Amount of time before the task starts (on the first run of the task)",
                    "0", "", "5s");

                parameters.addParameter<int>(
                    "TaskStartDelay",
                    "Amount of time before the task starts (after the first run of the task)",
                    "0", "", "10s");

                parameters.addParameter<int>(
                    "TaskInputChannel",
                    "Channel to base the cursor position on  (1...n)",
                    "1", "", "1");

                parameters.addParameter<int>(
                    "TaskInputSignalType",
                    "Task input signal type",
                    "0", "2", "0", new string[] { "Normalizer (0 to 1)", "Normalizer (-1 to 1)", "Constant middle" });

                parameters.addParameter<bool>(
                    "TaskShowScore",
                    "Show the score",
                    "0", "1", "1");

                

                parameters.addParameter<double>(
                    "CursorSize",
                    "Cursor size radius in percentage of the screen height",
                    "0.0", "50.0", "4.0");

                parameters.addParameter<int>(
                    "CursorColorRule",
                    "Cursor color rule",
                    "0", "2", "0", new string[] { "Hitcolor on target hit (normal)", "Hitcolor on input", "Hitcolor on input - Escape color on escape" });

                parameters.addParameter<RGBColorFloat>(
                    "CursorColorMiss",
                    "Cursor color when missing",
                    "", "", "204;0;0");

                parameters.addParameter<RGBColorFloat>(
                    "CursorColorHit",
                    "Cursor color when hitting",
                    "", "", "204;204;0");

                parameters.addParameter<double>(
                    "CursorColorHitTime",
                    "Time that the cursor remains in hit color",
                    "0", "", "2s");

                parameters.addParameter<RGBColorFloat>(
                    "CursorColorEscape",
                    "Cursor color when hitting",
                    "", "", "170;0;170");

                parameters.addParameter<double>(
                    "CursorColorEscapeTime",
                    "Time that the cursor remains in escape color",
                    "0", "", "2s");

                parameters.addParameter<double[][]>(
                    "Targets",
                    "Target positions and widths in percentage coordinates\n\nY_perc: The y position of the block on the screen (in percentages of the screen height), note that the value specifies where the middle of the block will be.\nHeight_perc: The height of the block on the screen (in percentages of the screen height)\nWidth_secs: The width of the target block in seconds",
                    "", "", "25,25,25,75,75,75;50,50,50,50,50,50;2,2,2,3,5,7", new string[] {"Y_perc", "Height_perc", "Width_secs" });

                parameters.addParameter<string[][]>(
                    "TargetTextures",
                    "Paths of target texture, relative to executable path",
                    "", "", "", new string[] {"filepath" });

                parameters.addParameter<int>(
                    "TargetYMode",
                    "Targets Y mode",
                    "0", "3", "3", new string[] { "Target(matrix) order", "Randomize categories", "Randomize cat without replacement", "Sequential categories with rnd start"});

                parameters.addParameter<int>(
                    "TargetWidthMode",
                    "Targets Width mode",
                    "0", "3", "1", new string[] { "Target(matrix) order", "Randomize categories", "Randomize cat without replacement", "Sequential categories with rnd start"});
                
                parameters.addParameter<int>(
                    "TargetHeightMode",
                    "Targets Height mode",
                    "0", "3", "1", new string[] { "Target(matrix) order", "Randomize categories", "Randomize cat without replacement", "Sequential categories with rnd start"});

                parameters.addParameter<int>(
                    "TargetSpeed",
                    "The speed of the targets (in pixels per second)",
                    "0", "", "120");

                parameters.addParameter<int>(
                    "NumberTargets",
                    "Number of targets",
                    "1", "", "70");

                parameters.addParameter<int[]>(
                    "TargetSequence",
                    "Fixed sequence in which targets should be presented (leave empty for random)",
                    "0", "", "");


            }

        }

        public Parameters getParameters() {
            return parameters;
        }

        public String getClassName() {
            return CLASS_NAME;
        }

        public int getClassVersion() {
            return CLASS_VERSION;
        }

        public bool configure(ref SampleFormat input) {

            // store the number of input channels
            inputChannels = input.getNumberOfChannels();

            // check if the number of input channels is higher than 0
            if (inputChannels <= 0) {
                logger.Error("Number of input channels cannot be 0");
                return false;
            }

            // 
            // TODO: parameters.checkminimum, checkmaximum

            
            // retrieve window settings
            mWindowLeft = parameters.getValue<int>("WindowLeft");
            mWindowTop = parameters.getValue<int>("WindowTop");
            mWindowWidth = parameters.getValue<int>("WindowWidth");
            mWindowHeight = parameters.getValue<int>("WindowHeight");
            mWindowRedrawFreqMax = parameters.getValue<int>("WindowRedrawFreqMax");
            mWindowBackgroundColor = parameters.getValue<RGBColorFloat>("WindowBackgroundColor");
            //mWindowed = true;           // fullscreen not implemented, so always windowed
            //mFullscreenMonitor = 0;     // fullscreen not implemented, default to 0 (does nothing)
            if (mWindowRedrawFreqMax < 0) {
                logger.Error("The maximum window redraw frequency can be no smaller then 0");
                return false;
            }
            if (mWindowWidth < 1) {
                logger.Error("The window width can be no smaller then 1");
                return false;
            }
            if (mWindowHeight < 1) {
                logger.Error("The window height can be no smaller then 1");
                return false;
            }

            // retrieve the input channel setting
            mTaskInputChannel = parameters.getValue<int>("TaskInputChannel");
	        if (mTaskInputChannel < 1) {
		        logger.Error("Invalid input channel, should be higher than 0 (1...n)");
                return false;
	        }
	        if (mTaskInputChannel > inputChannels) {
                logger.Error("Input should come from channel " + mTaskInputChannel + ", however only " + inputChannels + " channels are coming in");
                return false;
	        }

            // retrieve the task delays
            mTaskFirstRunStartDelay = parameters.getValueInSamples("TaskFirstRunStartDelay");
            mTaskStartDelay = parameters.getValueInSamples("TaskStartDelay");
            if (mTaskFirstRunStartDelay < 0 || mTaskStartDelay < 0) {
                logger.Error("Start delays cannot be less than 0");
                return false;
            }
            
            // retrieve the score parameter
            mShowScore = parameters.getValue<bool>("TaskShowScore");

            // retrieve the input signal type
            mTaskInputSignalType = parameters.getValue<int>("TaskInputSignalType");
            
            // retrieve cursor parameters
            mCursorSize = parameters.getValue<double>("CursorSize");
            mCursorColorRule = parameters.getValue<int>("CursorColorRule");
            mCursorColorMiss = parameters.getValue<RGBColorFloat>("CursorColorMiss");
            mCursorColorHit = parameters.getValue<RGBColorFloat>("CursorColorHit");
            mCursorColorHitTime = parameters.getValueInSamples("CursorColorHitTime");
            mCursorColorEscape = parameters.getValue<RGBColorFloat>("CursorColorEscape");
            mCursorColorEscapeTime = parameters.getValueInSamples("CursorColorEscapeTime");

            // retrieve target settings
            double[][] parTargets = parameters.getValue<double[][]>("Targets");
            if (parTargets.Length != 3 || parTargets[0].Length < 1) {
                logger.Error("Targets parameter must have at least 1 row and 3 columns (Y_perc, Height_perc, Width_secs)");
                return false;
            }
            
            // TODO: convert mTargets to 3 seperate arrays instead of jagged list?
            mTargets[0] = new List<float>(new float[parTargets[0].Length]);
            mTargets[1] = new List<float>(new float[parTargets[0].Length]);
            mTargets[2] = new List<float>(new float[parTargets[0].Length]);
            for(int row = 0; row < parTargets[0].Length; ++row) {
                mTargets[0][row] = (float)parTargets[0][row];
                mTargets[1][row] = (float)parTargets[1][row];
                mTargets[2][row] = (float)parTargets[2][row];
                if (mTargets[2][row] <= 0) {
                    logger.Error("The value '" + parTargets[2][row] + "' in the Targets parameter is not a valid width value, should be a positive numeric");
                    return false;
                }
            }
            
            string[][] parTargetTextures = parameters.getValue<string[][]>("TargetTextures");
            if (parTargetTextures.Length == 0) {
                mTargetTextures = new List<string>(0);
            } else {
                mTargetTextures = new List<string>(new string[parTargetTextures[0].Length]);
                for (int row = 0; row < parTargetTextures[0].Length; ++row) mTargetTextures[row] = parTargetTextures[0][row];
            }

            mTargetYMode = parameters.getValue<int>("TargetYMode");
            mTargetWidthMode = parameters.getValue<int>("TargetWidthMode");
            mTargetHeightMode = parameters.getValue<int>("TargetHeightMode");

            mTargetSpeed = parameters.getValue<int>("TargetSpeed");
            if (mTargetSpeed < 1) {
                logger.Error("The TargetSpeed parameter be at least 1");
                return false;
            }





            // retrieve the number of targets and (fixed) target sequence
            numTargets = parameters.getValue<int>("NumberTargets");
            fixedTargetSequence = parameters.getValue<int[]>("TargetSequence");
            if (fixedTargetSequence.Length == 0) {
                // no fixed sequence

                // check number of targets
                if (numTargets < 1) {
                    logger.Error("Minimum of 1 target is required");
                    return false;
                }

            } else {
                // fixed sequence

                numTargets = fixedTargetSequence.Length;
                for (int i = 0; i < numTargets; ++i) {
                    
                    if (fixedTargetSequence[i] < 0) {
                        logger.Error("The TargetSequence parameter contains a target index (" + fixedTargetSequence[i] + ") that is below zero, check the TargetSequence");
                        return false;
                    }
                    if (fixedTargetSequence[i] >= mTargets[0].Count) {
                        logger.Error("The TargetSequence parameter contains a target index (" + fixedTargetSequence[i] + ") that is out of range, check the Targets parameter. (note that the indexing is 0 based)");
                        return false;
                    }
                }

            }
            

            
            /*
                // other parameters
                State("Running");
                State("ConnectionLost");
                State("KeySequenceActive");
            */          
            
            return true;
        }

        public void initialize() {
                        
            // lock for thread safety
            lock(lockView) {

                // check the scene (thread) already exists, stop and clear the old one.
                destroyScene();

                // create the view
                mSceneThread = new FollowView(mWindowRedrawFreqMax, mWindowLeft, mWindowTop, mWindowWidth, mWindowHeight, false);
                mSceneThread.setBackgroundColor(mWindowBackgroundColor.getRed(), mWindowBackgroundColor.getGreen(), mWindowBackgroundColor.getBlue());
            
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
                // (resourcesLoaded also includes whether GL is loaded)
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

                // log the task start event
                Data.LogEvent(2, "TaskStart", "Followtask");

                if (mSceneThread == null)   return;

	            // reset the score
	            mHitScore = 0;

	            // reset countdown
	            mCountdownCounter = 15;

	            if(mTaskStartDelay != 0 || mTaskFirstRunStartDelay != 0) {
		            // wait

		            // set state to wait
		            setState(TaskStates.Wait);

                    // show the fixation
                    mSceneThread.setFixation(true);
		
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
            
            // retrieve the connectionlost global
            mConnectionLost = Globals.getValue<bool>("ConnectionLost");
            
            // process input
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

			            // show the lost connection warning
			            mSceneThread.setConnectionLost(true);

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

		            // hide the lost connection warning
		            mSceneThread.setConnectionLost(false);

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

                                    // only in non UNP-menu tasks
                                    if (!mUNPMenuTask) {

                                        /*
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
                                        */

                                    }

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
                            //logger.Debug("mCurrentBlock " + mCurrentBlock);

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
	            State("Log_FirstRunStartDelay").AsUnsigned() = (mTaskStartDelay > 0);									// delay running
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

            // stop the application
            // Note: At this point stop will probably have been called from the mainthread before destroy, however there is a slight
            // chance that in the future someone accidentally will put something in the configure/initialize that should have
            // actually been put in the start. If start is not called in the mainthread, then stop will also not be called at the
            // modules. For these accidents we do an extra stop here.
            stop();

            // lock for thread safety
            lock(lockView) {
                destroyScene();
            }

            // destroy/empty more task variables


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

        // pauses the task
        private void pauzeTask() {
            if (mSceneThread == null)   return;

            // log the task pause event
            Data.LogEvent(2, "TaskPause", "Followtask");

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

            // log the task pause event
            Data.LogEvent(2, "TaskResume", "Followtask");

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

                    // hide text if present
                    mSceneThread.setText("");

				    // hide the fixation and countdown
				    mSceneThread.setFixation(false);
                    mSceneThread.setCountDown(0);

				    // stop the blocks from moving
				    mSceneThread.setBlocksMove(false);

				    // hide the countdown, blocks, cursor and score
				    mSceneThread.setBlocksVisible(false);
				    mSceneThread.setCursorVisible(false);
				    mSceneThread.setScore(-1);

                    // Set wait counter to startdelay
                    if (mTaskFirstRunStartDelay != 0) {
                        mWaitCounter = mTaskFirstRunStartDelay;
                        mTaskFirstRunStartDelay = 0;
                    } else
			            mWaitCounter = mTaskStartDelay;

			        break;

		        case TaskStates.CountDown:
			        // countdown when task starts

				    // hide text if present
				    mSceneThread.setText("");

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
                    
				    // show text
				    mSceneThread.setText("Done");
			        
			        // set duration for text to be shown at the end
			        mWaitCounter = 15;

			        break;

	        }

        }

        // Stop the task
        private void stopTask() {
            
            if (mSceneThread == null)   return;

            // log the task stop event
            Data.LogEvent(2, "TaskStop", "Followtask");

            // set the current block to no block
            mCurrentBlock = FollowView.noBlock;

            // set state to wait
            setState(TaskStates.Wait);
    
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
            mTargetSequence = new List<int>(new int[numTargets]);

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
	        int generateSafetyCounter = numTargets + 1000;
            i = 0;
            while(i < numTargets) {
			
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


        ////////////////////////////////////////////////
        //  UNP entry points (start, process, stop)
        ////////////////////////////////////////////////
        /*
        //#ifdef UNPMENU
        */
        public void UNP_start(Parameters parentParameters) {

            // UNP entry point can only be used if initialized as UNPMenu
            if (!mUNPMenuTask) {
                logger.Error("Using UNP entry point while the task was not initialized as UNPMenu task, check parameters used to call the task constructor");
                return;
            }

            // set the parameter set as not visible (for GUI configuration)
            //parameters.ParamSetVisible = false;

            // transfer the window settings
            mWindowRedrawFreqMax = parentParameters.getValue<int>("WindowRedrawFreqMax");      // the view update frequency (in maximum fps)
            //mWindowed = true;
            mWindowWidth = parentParameters.getValue<int>("WindowWidth");;
            mWindowHeight = parentParameters.getValue<int>("WindowHeight");;
            mWindowLeft = parentParameters.getValue<int>("WindowLeft");;
            mWindowTop = parentParameters.getValue<int>("WindowTop");;
            //mFullscreenMonitor = 0;

            // set the UNP task standard settings
            mShowScore = true;
            mTaskInputSignalType = 1;
            mTaskInputChannel = 1;
            mTaskFirstRunStartDelay = 5;
            mTaskStartDelay = 10;
            mCursorSize = 4f;
            mCursorColorRule = 0;
            mCursorColorMiss = new RGBColorFloat(0.8f, 0f, 0f);
            mCursorColorHit = new RGBColorFloat(0.8f, 0.8f, 0f);
            mCursorColorHitTime = 0;
            mCursorColorEscape = new RGBColorFloat(0.8f, 0f, 0.8f);
            mCursorColorEscapeTime = 0;
            numTargets = 70;
            mTargetSpeed = 120;
            mTargetYMode = 3;
            mTargetWidthMode = 1;
            mTargetHeightMode = 1;


            mTargets[0].Clear(); mTargets[0] = new List<float>(new float[6]);
            mTargets[1].Clear(); mTargets[1] = new List<float>(new float[6]);
            mTargets[2].Clear(); mTargets[2] = new List<float>(new float[6]);
            mTargets[0][0] = 25; mTargets[1][0] = 50; mTargets[2][0] = 2;
            mTargets[0][1] = 25; mTargets[1][1] = 50; mTargets[2][1] = 2;
            mTargets[0][2] = 25; mTargets[1][2] = 50; mTargets[2][2] = 2;
            mTargets[0][3] = 75; mTargets[1][3] = 50; mTargets[2][3] = 3;
            mTargets[0][4] = 75; mTargets[1][4] = 50; mTargets[2][4] = 5;
            mTargets[0][5] = 75; mTargets[1][5] = 50; mTargets[2][5] = 7;


            mTargetTextures = new List<string>(new string[6]);
            mTargetTextures[0] = "images\\sky.bmp";
            mTargetTextures[1] = "images\\sky.bmp";
            mTargetTextures[2] = "images\\sky.bmp";
            mTargetTextures[3] = "images\\grass.bmp";
            mTargetTextures[4] = "images\\grass.bmp";
            mTargetTextures[5] = "images\\grass.bmp";

	        // initialize
	        initialize();

	        // start the task
	        start();

            // set the task as running
            mUNPMenuTaskRunning = true;

        }

        public void UNP_stop() {
            
            // UNP entry point can only be used if initialized as UNPMenu
            if (!mUNPMenuTask) {
                logger.Error("Using UNP entry point while the task was not initialized as UNPMenu task, check parameters used to call the task constructor");
                return;
            }

            // stop the task from running
            stop();

            // destroy the task
            destroy();

            // flag the task as no longer running (setting this to false is also used to notify the UNPMenu that the task is finished)
            mUNPMenuTaskRunning = false;

        }

        public bool UNP_isRunning() {
            return mUNPMenuTaskRunning;
        }

        public void UNP_process(double[] input, bool connectionLost) {

	        // check if the task is running
	        if (mUNPMenuTaskRunning) {

		        // transfer connection lost
		        mConnectionLost = connectionLost;

		        // process the input
		        if (!mUNPMenuTaskSuspended)		process(input);

	        }

        }

        public void UNP_resume() {

            // lock for thread safety
            lock(lockView) {

	            // restart the view thread
	            if (mSceneThread != null) {

		            // continue followtask
		            mSceneThread.initBlockTextures(mTargetTextures);
		            mSceneThread.centerCursor();
                    
	                // restart the view thread
		            mSceneThread.start();

	                // wait till the resources are loaded or a maximum amount of 5 seconds (5.000 / 10 = 500)
                    // (resourcesLoaded also includes whether GL is loaded)
	                int waitCounter = 500;
	                while (!mSceneThread.resourcesLoaded() && waitCounter > 0) {
		                Thread.Sleep(10);
		                waitCounter--;
	                }

		            mSceneThread.initBlockSequence(mTargetSequence, mTargets);

	            }
                
            }

	        // resume the task
	        resumeTask();

	        // flag task as no longer suspended
	        mUNPMenuTaskSuspended = false;

        }

        public void UNP_suspend() {

            // flag task as suspended
            mUNPMenuTaskSuspended = true;

            // pauze the task
            pauzeTask();

            // lock for thread safety
            lock(lockView) {

                // stop the view thread
                if (mSceneThread != null)   mSceneThread.stop();

            }

        }
        /*
        #endif
        */

    }

}
