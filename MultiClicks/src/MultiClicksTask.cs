using NLog;
using System;
using System.Collections.Generic;
using System.Threading;

using UNP.Applications;
using UNP.Core;
using UNP.Core.Helpers;
using UNP.Core.Params;
using UNP.Core.DataIO;
using System.Collections.Specialized;

namespace MultiClicksTask {

    public class MultiClicksTask : IApplication, IApplicationUNP {

		private enum TaskStates:int {
			Wait,
			CountDown,
			Task,
			EndText
		};

        private const int CLASS_VERSION = 1;
        private const string CLASS_NAME = "MultiClicksTask";
        private const string CONNECTION_LOST_SOUND = "sounds\\focuson.wav";

        private static Logger logger = LogManager.GetLogger(CLASS_NAME);                        // the logger object for the view
        private static Parameters parameters = null;

        private int inputChannels = 0;
        private MultiClicksView view = null;

        Random rand = new Random(Guid.NewGuid().GetHashCode());
        private Object lockView = new Object();                         // threadsafety lock for all event on the view
        private bool mTaskPauzed = false;								// flag to hold whether the task is pauzed (view will remain active, e.g. connection lost)

        private bool mUNPMenuTask = false;								// flag whether the task is created by the UNPMenu
        private bool mUNPMenuTaskRunning = false;						// flag to hold whether the task should is running (setting this to false is also used to notify the UNPMenu that the task is finished)
        private bool mUNPMenuTaskSuspended = false;						// flag to hold whether the task is suspended (view will be destroyed/re-initiated)

        private bool mConnectionLost = false;							// flag to hold whether the connection is lost
        private bool mConnectionWasLost = false;						// flag to hold whether the connection has been lost (should be reset after being re-connected)
        private System.Timers.Timer mConnectionLostSoundTimer = null;   // timer to play the connection lost sound on

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
		private int mTargetSpeed = 0;
        private List<List<float>> mTargets = new List<List<float>>() {              // the block/target definitions (1ste dimention are respectively Ys, Heights, Widths; 2nd dimension blocks options) 
            new List<float>(0), 
            new List<float>(0), 
            new List<float>(0),
            new List<float>(0)
        };          
        private List<string> mTargetTextures = new List<string>(0);                 // the block/target texture definitions (each element gives the texture for each block option, corresponds to the 2nd dimension of targets) 
        private int[] mRandomRests = null;                                          // the targets that that can be generated into the target sequence as rest
        private int[][] mRandomTrialCombos = null;                                  // the target combinations that should be generated into the target sequence
        private int[] mRandomTrialQuantities = null;                                // the amount of respective combination that should be generated into the target sequence

        private int mTaskInputChannel = 1;											// input channel
        private int mTaskInputSignalType = 0;										// input signal type (0 = 0 to 1, 1 = -1 to 1)
        private int mTaskFirstRunStartDelay = 0;                                    // the first run start delay in sample blocks
        private int mTaskStartDelay = 0;									        // the run start delay in sample blocks
        private int mCountdownTime = 0;                                             // the time the countdown takes in sample blocks
        private bool mShowScore = false;


        // task (active) variables
        private List<int> mTargetSequence = new List<int>(0);					    // the target sequence being used in the task (can either be given by input or generated)

        private int mWaitCounter = 0;
        private int mCountdownCounter = 0;											// the countdown timer
        private int mHitScore = 0;												    // the score of the cursor hitting a block (in number of samples)
        private bool wasInput = false;                                                  // keep track of previous input

        private TaskStates taskState = TaskStates.Wait;
        private TaskStates previousTaskState = TaskStates.Wait;
        private int mCurrentBlock = MultiClicksView.noBlock;                             // the current block which is in line with X of the cursor (so the middle)
        private int mPreviousBlock = MultiClicksView.noBlock;                            // the previous block that was in line with X of the cursor
        //private bool mIsCursorInCurrentBlock = false;                              // whether the cursor is inside the current block
        //private bool mWasCursorInCurrentBlock = false;                              // whether the cursor was inside the current blockprivate bool mWasCursorInCurrentBlock = false;                              // whether the cursor was inside the current block
        private bool mKeySequenceActive = false;                        // flag to hold whether the keysequence is active
        private bool mKeySequenceWasPressed = false;


        private float[] storedBlockPositions = null;                                // to store the previous block positions while suspended

        public MultiClicksTask() : this(false) { }
        public MultiClicksTask(bool UNPMenuTask) {

            // transfer the UNP menu task flag
            mUNPMenuTask = UNPMenuTask;

            // check if the task is standalone (not unp menu)
            if (!mUNPMenuTask) {
            
                // create a parameter set for the task
                parameters = ParameterManager.GetParameters(CLASS_NAME, Parameters.ParamSetTypes.Application);

                // define the parameters
                defineParameters(ref parameters);

            }

            // message
            logger.Info("Application created (version " + CLASS_VERSION + ")");

        }

        private void defineParameters(ref Parameters parameters) {

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
                "0", "", "50");

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
                "0", "", "5s");

            parameters.addParameter<int>(
                "CountdownTime",
                "Amount of time the countdown before the task takes",
                "0", "", "3s");

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
                "Target colors, positions and widths in percentage coordinates\n\nY_perc: The y position of the block on the screen (in percentages of the screen height), note that the value specifies where the middle of the block will be.\nHeight_perc: The height of the block on the screen (in percentages of the screen height)\nWidth_secs: The width of the target block in seconds\nColor: color of the target block as a 24-bit integer (RRGGBB order)",
                "", "", "50,50,50,-100,-100;100,100,100,0,0;2,0.6,0.6,2,3;100,41704,10701220,0,0", new string[] { "Y_perc", "Height_perc", "Width_secs", "color" });

            parameters.addParameter<string[][]>(
                "TargetTextures",
                "Paths of target texture, relative to executable path",
                "", "", "", new string[] { "filepath" });

            parameters.addParameter<double[]>(
                "RandomRests",
                "The rest target(s) that are generated in the target sequence inbetween the trials.\nTargets are specified by the row number as they appear in the Target parameter (zero-based)",
                "0", "", "3 4");

            parameters.addParameter<string[][]>(
                "RandomTrials",
                "The trials that are generated into the target sequence. Each row defines a trial, where each trial is composed of single target or series of targets.\nTargets are specified by the row number as they appear in the Target parameter (zero-based; e.g. double click could be '1 0 1')\nThe quantity column defines how often a trial will occur in the target sequence, as a result, the length of the entire sequence is defined by the RandomTrials and RandomRestTime parameters.",
                "", "", "Click,DblClick;1,2 0 2;2,3", new string[] { "Label", "Target_combo", "Quantity" });
            
            parameters.addParameter<int>(
                "TargetSpeed",
                "The speed of the targets (in pixels per second)",
                "0", "", "120");

            parameters.addParameter<int[]>(
                "TargetSequence",
                "Fixed sequence in which targets should be presented (leave empty for random)\nNote. indexing is 0 based (so a value of 0 will be the first row from the 'Targets' parameter",
                "0", "", "");

        }

        public Parameters getParameters() {
            return parameters;
        }

        public string getClassName() {
            return CLASS_NAME;
        }

        public int getClassVersion() {
            return CLASS_VERSION;
        }

        public bool configure(ref PackageFormat input) {

            // store the number of input channels
            inputChannels = input.getNumberOfChannels();

            // check if the number of input channels is higher than 0
            if (inputChannels <= 0) {
                logger.Error("Number of input channels cannot be 0");
                return false;
            }

            // configure the parameters
            return configure(parameters);

        }

        public bool configure(Parameters newParameters) {
			
            // 
            // TODO: parameters.checkminimum, checkmaximum

            
            // retrieve window settings
            mWindowLeft = newParameters.getValue<int>("WindowLeft");
            mWindowTop = newParameters.getValue<int>("WindowTop");
            mWindowWidth = newParameters.getValue<int>("WindowWidth");
            mWindowHeight = newParameters.getValue<int>("WindowHeight");
            mWindowRedrawFreqMax = newParameters.getValue<int>("WindowRedrawFreqMax");
            mWindowBackgroundColor = newParameters.getValue<RGBColorFloat>("WindowBackgroundColor");
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
            mTaskInputChannel = newParameters.getValue<int>("TaskInputChannel");
	        if (mTaskInputChannel < 1) {
		        logger.Error("Invalid input channel, should be higher than 0 (1...n)");
                return false;
	        }
	        if (mTaskInputChannel > inputChannels) {
                logger.Error("Input should come from channel " + mTaskInputChannel + ", however only " + inputChannels + " channels are coming in");
                return false;
	        }

            // retrieve the task delays
            mTaskFirstRunStartDelay = newParameters.getValueInSamples("TaskFirstRunStartDelay");
            mTaskStartDelay = newParameters.getValueInSamples("TaskStartDelay");
            if (mTaskFirstRunStartDelay < 0 || mTaskStartDelay < 0) {
                logger.Error("Start delays cannot be less than 0");
                return false;
            }

            // retrieve the countdown time
            mCountdownTime = newParameters.getValueInSamples("CountdownTime");
            if (mCountdownTime < 0) {
                logger.Error("Countdown time cannot be less than 0");
                return false;
            }

            // retrieve the score parameter
            mShowScore = newParameters.getValue<bool>("TaskShowScore");

            // retrieve the input signal type
            mTaskInputSignalType = newParameters.getValue<int>("TaskInputSignalType");
            
            // retrieve cursor parameters
            mCursorSize = newParameters.getValue<double>("CursorSize");
            mCursorColorRule = newParameters.getValue<int>("CursorColorRule");
            mCursorColorMiss = newParameters.getValue<RGBColorFloat>("CursorColorMiss");
            mCursorColorHit = newParameters.getValue<RGBColorFloat>("CursorColorHit");
            mCursorColorHitTime = newParameters.getValueInSamples("CursorColorHitTime");
            mCursorColorEscape = newParameters.getValue<RGBColorFloat>("CursorColorEscape");
            mCursorColorEscapeTime = newParameters.getValueInSamples("CursorColorEscapeTime");

            // retrieve target settings
            double[][] parTargets = newParameters.getValue<double[][]>("Targets");
            if (parTargets.Length != 4 || parTargets[0].Length < 1) {
                logger.Error("Targets parameter must have at least 1 row and 4 columns (Y_perc, Height_perc, Width_secs, Color)");
                return false;
            }
            
            // TODO: convert mTargets to 3 seperate arrays instead of jagged list?
            mTargets[0] = new List<float>(new float[parTargets[0].Length]);
            mTargets[1] = new List<float>(new float[parTargets[0].Length]);
            mTargets[2] = new List<float>(new float[parTargets[0].Length]);
            mTargets[3] = new List<float>(new float[parTargets[0].Length]);
            for(int row = 0; row < parTargets[0].Length; ++row) {
                mTargets[0][row] = (float)parTargets[0][row];
                mTargets[1][row] = (float)parTargets[1][row];
                mTargets[2][row] = (float)parTargets[2][row];
                if (mTargets[2][row] <= 0) {
                    logger.Error("The value '" + parTargets[2][row] + "' in the Targets parameter is not a valid width value, should be a positive numeric");
                    return false;
                }
                mTargets[3][row] = (float)parTargets[3][row];
                if (mTargets[3][row] < 0 || mTargets[3][row] > 16777215) {
                    logger.Error("The value '" + parTargets[3][row] + "' in the Targets parameter is not a valid color value, should be a numeric value between 0 (including) and 16777215 (including)");
                    return false;
                }
            }
            
            string[][] parTargetTextures = newParameters.getValue<string[][]>("TargetTextures");
            if (parTargetTextures.Length == 0) {
                mTargetTextures = new List<string>(0);
            } else {
                mTargetTextures = new List<string>(new string[parTargetTextures[0].Length]);
                for (int row = 0; row < parTargetTextures[0].Length; ++row) mTargetTextures[row] = parTargetTextures[0][row];
            }

            // retrieve the target speed
            mTargetSpeed = newParameters.getValue<int>("TargetSpeed");
            if (mTargetSpeed < 1) {
                logger.Error("The TargetSpeed parameter be at least 1");
                return false;
            }

            // retrieve the number of targets and (fixed) target sequence
            fixedTargetSequence = newParameters.getValue<int[]>("TargetSequence");
            if (fixedTargetSequence.Length == 0) {
                // no fixed sequence

                // retrieve randomRestTime settings
                mRandomRests = newParameters.getValueInSamples<int[]>("RandomRests");
                if (mRandomRests.Length == 0) {
                    logger.Error("At least one random rest target should be given in order to generate a random target sequence");
                    return false;
                }

                // retrieve random trials settings
                string[][] parRandomTrials = newParameters.getValue<string[][]>("RandomTrials");
                if (parRandomTrials.Length != 3 || parRandomTrials[0].Length == 0) {
                    logger.Error("RandomTrials parameter must have 3 columns (Label, Target_combo, Quantity) and at least one row in order to generate a random target sequence");
                    return false;
                }
				
                mRandomTrialCombos = new int[parRandomTrials[0].Length][];
                mRandomTrialQuantities = new int[parRandomTrials[0].Length];
                int totalQuantity = 0;
                for (int i = 0; i < parRandomTrials[0].Length; i++) {
                    string[] trTargets = parRandomTrials[1][i].Split(Parameters.ArrDelimiters, StringSplitOptions.RemoveEmptyEntries);
                    if (trTargets.Length == 0) {
                        logger.Error("Invalid or empty target_combo '" + parRandomTrials[1][i] + "'");
                        return false;
                    }

                    mRandomTrialCombos[i] = new int[trTargets.Length];
                    for (int j = 0; j < trTargets.Length; j++) {
                        int targetValue = 0;
                        if (!int.TryParse(trTargets[j], out targetValue)) {
                            logger.Error("Cannot interpret all values in the target_combo '" + parRandomTrials[1][i] + "', invalid targets");
                            return false;
                        }
                        if (targetValue < 0 || targetValue >= mTargets[0].Count) {
                            logger.Error("The value '" + targetValue + "' in target_combo '" + parRandomTrials[1][i] + "' is an invalid target index");
                            return false;
                        }
                        mRandomTrialCombos[i][j] = targetValue;
                    }

                    int targetQuantity = 0;
                    if (!int.TryParse(parRandomTrials[2][i], out targetQuantity) || targetQuantity < 0) {
                        logger.Error("Cannot interpret the quantity value '" + parRandomTrials[2][i] + "', should be a positive numeric value");
                        return false;
                    }
                    mRandomTrialQuantities[i] = targetQuantity;
                    totalQuantity += targetQuantity;
                }
                if (totalQuantity == 0) {
                    logger.Error("The total quantity of all trials cannot be 0, specify at least one row with a quantity above 0");
                    return false;
                }

            } else {
                // fixed sequence

                // loop through the targets in the sequence
                for (int i = 0; i < fixedTargetSequence.Length; ++i) {
                    
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

            // retun success
            return true;

        }
		
        public void initialize() {
                        
            // lock for thread safety
            lock(lockView) {

                // check the view (thread) already exists, stop and clear the old one.
                destroyView();

                // initialize the view
                initializeView();

                // check if a target sequence is set
                if (fixedTargetSequence.Length == 0) {
		            // targetsequence not set in parameters, generate
		
		            // Generate targetlist
		            generateTargetSequence();

	            } else {
		            // targetsequence is set in parameters

		            // clear the targets
		            if (mTargetSequence.Count != 0)		mTargetSequence.Clear();
                
		            // transfer the targetsequence
                    mTargetSequence = new List<int>(fixedTargetSequence);

	            }
	        
	            // initialize the target sequence
	            view.initBlockSequence(mTargetSequence, mTargets);

            }

        }

        private void initializeView() {

            // create the view
            view = new MultiClicksView(mWindowRedrawFreqMax, mWindowLeft, mWindowTop, mWindowWidth, mWindowHeight, false);
            view.setBackgroundColor(mWindowBackgroundColor.getRed(), mWindowBackgroundColor.getGreen(), mWindowBackgroundColor.getBlue());

            // set task specific display attributes 
            view.setBlockSpeed(mTargetSpeed);                                   // target speed
            view.setCursorSizePerc(mCursorSize);                                // cursor size radius in percentage of the screen height
            view.setCursorHitColor(mCursorColorHit);                            // cursor hit color
            view.setCursorMissColor(mCursorColorMiss);                          // cursor out color            
            view.initBlockTextures(mTargetTextures);                            // initialize target textures (do this before the thread start)
            view.centerCursor();                                                // set the cursor to the middle of the screen
            view.setFixation(false);                                            // hide the fixation
            view.setCountDown(-1);                                              // hide the countdown

            // check if the cursor rule is set to hitcolor on hit, if so
            // then make the color automatically determined in the Scenethread by it's variable 'mCursorInCurrentBlock',
            // this makes the color update quickly, since the scenethread is executed at a higher frequency
            if (mCursorColorRule == 0) {
                view.setCursorColorSetting(3);
            }

            // start the scene thread
            view.start();

            // wait till the resources are loaded or a maximum amount of 30 seconds (30.000 / 50 = 600)
            // (resourcesLoaded also includes whether GL is loaded)
            int waitCounter = 600;
            while (!view.resourcesLoaded() && waitCounter > 0) {
                Thread.Sleep(50);
                waitCounter--;
            }

        }

        public void start() {

            // check if the task is standalone (not unp menu)
            if (!mUNPMenuTask) {

                // store the generated sequence in the output parameter xml
                Data.adjustXML(CLASS_NAME, "TargetSequence", string.Join(" ", mTargetSequence));

            }

            // lock for thread safety
            lock (lockView) {

                if (view == null)   return;

                // log event task is started
                Data.logEvent(2, "TaskStart", CLASS_NAME);

                // reset the score
                mHitScore = 0;

                // reset countdown to the countdown time
                mCountdownCounter = mCountdownTime;

                if (mTaskStartDelay != 0 || mTaskFirstRunStartDelay != 0) {
		            // wait

		            // set state to wait
		            setState(TaskStates.Wait);

                    // show the fixation
                    view.setFixation(true);
		
	            } else {
		
		            // countdown
                    setState(TaskStates.CountDown);

	            }
            }

        }

        public void stop() {

            // log event task is stopped
            Data.logEvent(2, "TaskStop", CLASS_NAME + ";user");

            // lock for thread safety
            lock (lockView) {

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
                
                if (view == null)   return;
                
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
			            view.setConnectionLost(true);

                        // play the connection lost sound
                        Sound.Play(CONNECTION_LOST_SOUND);

                        // setup and start a timer to play the connection lost sound every 2 seconds
                        mConnectionLostSoundTimer = new System.Timers.Timer(2000);
                        mConnectionLostSoundTimer.Elapsed += delegate (object source, System.Timers.ElapsedEventArgs e) {

                            // play the connection lost sound
                            Sound.Play(CONNECTION_LOST_SOUND);

                        };
                        mConnectionLostSoundTimer.AutoReset = true;
                        mConnectionLostSoundTimer.Start();

                    }

                    // do not process any further
                    return;

                } else if (mConnectionWasLost && !mConnectionLost) {
                    // if the connection was lost and is not lost anymore

                    // stop and clear the connection lost timer
                    if (mConnectionLostSoundTimer != null) {
                        mConnectionLostSoundTimer.Stop();
                        mConnectionLostSoundTimer = null;
                    }

                    // hide the lost connection warning
                    view.setConnectionLost(false);

                    // resume task
                    resumeTask();

                    // reset connection lost variables
                    mConnectionWasLost = false;

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

                            // display the countdown
                            view.setCountDown((int)Math.Floor((mCountdownCounter - 1) / MainThread.getPipelineSamplesPerSecond()) + 1);

                            // reduce the countdown timer
                            mCountdownCounter--;

                        } else {
                            // done counting down

                            // hide the countdown counter
                            view.setCountDown(-1);

                            // set the current block to no block
                            mCurrentBlock = MultiClicksView.noBlock;

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
                            
				            view.setCursorNormY(input);	// setCursorNormY will take care of values below 0 or above 1)
		
			            } else if (mTaskInputSignalType == 1) {
				            // Normalizer (-1 to 1)

				            view.setCursorNormY((input + 1.0) / 2.0);

			            } else if (mTaskInputSignalType == 2) {
				            // Constant middle

				            view.setCursorNormY(0.5);

			            }

			            // check if it is the end of the task
			            if (mCurrentBlock == mTargetSequence.Count - 1 && (view.getCurrentBlock() == MultiClicksView.noBlock)) {
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
							            view.setCursorColorSetting(0);

					            }


                                // log if current clickstate has changed
                                if (wasInput != (input == 1)) {
                                    Data.logEvent(2, "ClickChange", (input == 1) ? "1" : "0");
                                    wasInput = (input == 1);
                                }

                                // check the color rule
                                if (mCursorColorRule == 2) {
                                    // 2. Hitcolor on input - Escape color on escape

                                    // only in non UNP-menu tasks
                                    if (!mUNPMenuTask) {

                                        // retrieve the keysequenceactive global
                                        mKeySequenceActive = Globals.getValue<bool>("KeySequenceActive");

                                        // log if the escapestate has changed
                                        if (mKeySequenceActive != mKeySequenceWasPressed) {
                                            Data.logEvent(2, "EscapeChange", (mKeySequenceActive) ? "1" : "0");
                                            mKeySequenceWasPressed = mKeySequenceActive;
                                        }

                                        // check if a keysequence input comes in or a click input comes in
                                        if (mKeySequenceActive) {

                                            // set the color
                                            view.setCursorColorSetting(2);

							                // set the timer
							                if (mCursorColorEscapeTime == 0)	mCursorColorTimer = 1;
							                else								mCursorColorTimer = mCursorColorEscapeTime;

						                } else {

                                            // check if a click was made
                                            if (input == 1) {

                                                // set the color
                                                view.setCursorColorSetting(1);

                                                // set the timer
                                                if (mCursorColorHitTime == 0)	mCursorColorTimer = 1;
								                else							mCursorColorTimer = mCursorColorHitTime;

							                }

						                }
                                        
                                    }

					            } else {
                                    // 1. Hitcolor on input

                                    // check if a click was made
                                    if (input == 1) {
						
							            // set the color
							            view.setCursorColorSetting(1);

							            // set the timer
							            if (mCursorColorHitTime == 0)   mCursorColorTimer = 1;
							            else							mCursorColorTimer = mCursorColorHitTime;

						            }
					            }

				            }

				            // retrieve the current block and if cursor is in this block
				            mCurrentBlock = view.getCurrentBlock();
                            bool mIsCursorInCurrentBlock = view.getCursorInCurrentBlock();

                            // retrieve which block condition the current block is
                            int blockCondition = -1;
                            if (mCurrentBlock != MultiClicksView.noBlock) blockCondition = mTargetSequence[mCurrentBlock];

                            // log event if the current block has changed and update the previous block placeholder
                            if (mCurrentBlock != mPreviousBlock)     Data.logEvent(2, "Changeblock", (mCurrentBlock.ToString() + ";" + blockCondition.ToString()));
                            mPreviousBlock = mCurrentBlock;

                            // log event if cursor entered or left the current block
                            //if (mIsCursorInCurrentBlock != mWasCursorInCurrentBlock) {
                            //    if (mIsCursorInCurrentBlock) { Data.logEvent(2, "CursorEnter", mCurrentBlock.ToString()); }
                            //    else { Data.logEvent(2, "CursorExit", mCurrentBlock.ToString()); }
                            //}

                            // update whether cursor was in current block 
                            //mWasCursorInCurrentBlock = mIsCursorInCurrentBlock;

                            // add to score if cursor hits the block
                            if (mIsCursorInCurrentBlock) mHitScore++;

				            // update the score for display
				            if (mShowScore)     view.setScore(mHitScore);

			            }

			            break;

		            case TaskStates.EndText:
			            // end text

			            if(mWaitCounter == 0) {

                            // log event task is stopped
                            Data.logEvent(2, "TaskStop", CLASS_NAME + ";end");

                            // check if we are running from the UNPMenu
                            if (mUNPMenuTask) {

                                // stop the task (UNP)
                                UNP_stop();

                            } else {

                                // stop the run, this will also call stopTask()
                                MainThread.stop(false);
                            }

			            } else
				            mWaitCounter--;

			            break;
	            }

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
                
                // destroy the view
                destroyView();

                // stop and clear the connection lost timer
                if (mConnectionLostSoundTimer != null) {
                    mConnectionLostSoundTimer.Stop();
                    mConnectionLostSoundTimer = null;
                }

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

        // pauzes the task
        private void pauzeTask() {
            if (view == null)   return;

            // log event task is paused
            Data.logEvent(2, "TaskPause", CLASS_NAME);

            // set task as pauzed
            mTaskPauzed = true;

	        // store the previous state
	        previousTaskState = taskState;
	
	        // store the block positions
	        if (previousTaskState == TaskStates.Task) {
                storedBlockPositions = view.getBlockPositions();
	        }

		    // hide everything
		    view.setFixation(false);
		    view.setCountDown(-1);
		    view.setBlocksVisible(false);
		    view.setCursorVisible(false);
		    view.setBlocksMove(false);
		    view.setScore(-1);

        }

        // resumes the task
        private void resumeTask() {
            if (view == null)   return;

            // log event task is paused
            Data.logEvent(2, "TaskResume", CLASS_NAME);

            // re-instate the block positions
            if (previousTaskState == TaskStates.Task) {
                view.setBlockPositions(storedBlockPositions);
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
                    view.setText("");

				    // hide the fixation and countdown
				    view.setFixation(false);
                    view.setCountDown(-1);

				    // stop the blocks from moving
				    view.setBlocksMove(false);

				    // hide the countdown, blocks, cursor and score
				    view.setBlocksVisible(false);
				    view.setCursorVisible(false);
				    view.setScore(-1);

                    // Set wait counter to startdelay
                    if (mTaskFirstRunStartDelay != 0) {
                        mWaitCounter = mTaskFirstRunStartDelay;
                        mTaskFirstRunStartDelay = 0;
                    } else
			            mWaitCounter = mTaskStartDelay;

			        break;

		        case TaskStates.CountDown:
                    // countdown when task starts

                    // log event countdown is started
                    Data.logEvent(2, "CountdownStarted ", "");

                    // hide text if present
                    view.setText("");

				    // hide fixation
				    view.setFixation(false);

				    // set countdown
                    if (mCountdownCounter > 0)
                        view.setCountDown((int)Math.Floor((mCountdownCounter - 1) / MainThread.getPipelineSamplesPerSecond()) + 1);
                    else
                        view.setCountDown(-1);

			        break;


		        case TaskStates.Task:
                    // perform the task

                    // log event countdown is started
                    Data.logEvent(2, "TrialStart ", "");

                    /*
				    // hide text if present
				    view->setText("");
                    */

                    // hide the countdown counter
                    view.setCountDown(-1);

				    // set the score for display
				    if (mShowScore)		view.setScore(mHitScore);

				    // reset the cursor position
				    view.centerCursor();

				    // show the cursor
				    view.setCursorVisible(true);

				    // show the blocks and start the blocks animation
				    view.setBlocksVisible(true);
				    view.setBlocksMove(true);

			        break;

		        case TaskStates.EndText:
			        // show text
			
				    // stop the blocks from moving
				    view.setBlocksMove(false);

				    // hide the blocks and cursor
				    view.setBlocksVisible(false);
				    view.setCursorVisible(false);
                    
				    // show text
				    view.setText("Done");

                    // set duration for text to be shown at the end (3s)
                    mWaitCounter = (int)(MainThread.getPipelineSamplesPerSecond() * 3.0);

                    break;

	        }

        }

        // Stop the task
        private void stopTask() {
            if (view == null)   return;

            // set the current block to no block
            mCurrentBlock = MultiClicksView.noBlock;

            // set state to wait
            setState(TaskStates.Wait);
    
            // initialize the target sequence already for a possible next run
	        if (fixedTargetSequence.Length == 0) {

		        // Generate targetlist
		        generateTargetSequence();

	        }

            // initialize the target sequence
	        view.initBlockSequence(mTargetSequence, mTargets);

        }


        private void generateTargetSequence() {
	        
	        // clear the targets
	        if (mTargetSequence.Count != 0)		mTargetSequence.Clear();

            // count the number of trials
            int totalQuantity = 0;
            for (int i = 0; i < mRandomTrialQuantities.Length; i++) {
                totalQuantity += mRandomTrialQuantities[i];
            }

            // count the number of rests (= number of trials - 1, but + 1 to have an additional rest in the end after all the trials)
            int totalRests = totalQuantity;

            // create an array with the rests to be used inbetween the trials and after the last trial
            int[] arrRestIndices = new int[totalRests];
            int restCounter = 0;
            for (int i = 0; i < arrRestIndices.Length; i++) {
                arrRestIndices[i] = restCounter;
                restCounter++;
                if (restCounter == mRandomRests.Length) restCounter = 0;
            }
            arrRestIndices.Shuffle();

            // create an array the size of the number of trials and fill it with the indices of the trial-combination that should be in there (given the quantity each trial-combination
            int[] arrTrialIndices = new int[totalQuantity];
            int counter = 0;
            for (int i = 0; i < mRandomTrialQuantities.Length; i++) {
                for (int j = 0; j < mRandomTrialQuantities[i]; j++) {
                    arrTrialIndices[counter] = i;
                    counter++;
                }
            }

            // shuffle the array to make the trials random
            arrTrialIndices.Shuffle();

            // loop throug the trials that should be added
            for (int i = 0; i < arrTrialIndices.Length; i++) {

                // add the combination to the target sequence
                for (int j = 0; j < mRandomTrialCombos[arrTrialIndices[i]].Length; j++) {
                    mTargetSequence.Add(mRandomTrialCombos[arrTrialIndices[i]][j]);
                }

                // check if this is not the last item
                if (i != arrTrialIndices.Length - 1) {

                    // add a random rest (inbetween the trials)
                    mTargetSequence.Add(mRandomRests[arrRestIndices[i]]);
                    
                }

            }

            // add a rest after all the trials
            mTargetSequence.Add(mRandomRests[arrRestIndices[arrRestIndices.Length - 1]]);


        }


        ////////////////////////////////////////////////
        //  UNP entry points (start, process, stop)
        ////////////////////////////////////////////////

        public void UNP_start(Parameters parentParameters) {

            // UNP entry point can only be used if initialized as UNPMenu
            if (!mUNPMenuTask) {
                logger.Error("Using UNP entry point while the task was not initialized as UNPMenu task, check parameters used to call the task constructor");
                return;
            }

            // create a new parameter object and define this task's parameters
            Parameters newParameters = new Parameters("MultiClicksTask", Parameters.ParamSetTypes.Application);
            defineParameters(ref newParameters);

            // transfer some parameters from the parent
            newParameters.setValue("WindowRedrawFreqMax", parentParameters.getValue<int>("WindowRedrawFreqMax"));
            newParameters.setValue("WindowWidth", parentParameters.getValue<int>("WindowWidth"));
            newParameters.setValue("WindowHeight", parentParameters.getValue<int>("WindowHeight"));
            newParameters.setValue("WindowLeft", parentParameters.getValue<int>("WindowLeft"));
            newParameters.setValue("WindowTop", parentParameters.getValue<int>("WindowTop"));

            // set UNP task standard settings
            inputChannels = 1;
            newParameters.setValue("WindowBackgroundColor", "0;0;0");
            newParameters.setValue("CountdownTime", "3s");
            newParameters.setValue("TaskShowScore", true);
            newParameters.setValue("TaskInputSignalType", 1);
            newParameters.setValue("TaskInputChannel", 1);
            newParameters.setValue("TaskFirstRunStartDelay", "2s");
            newParameters.setValue("TaskStartDelay", "2s");
            newParameters.setValue("CursorSize", 4.0);
            newParameters.setValue("CursorColorRule", 0);
            newParameters.setValue("CursorColorMiss", "204;0;0");
            newParameters.setValue("CursorColorHit", "204;204;0");
            newParameters.setValue("CursorColorHitTime", 0.0);
            newParameters.setValue("CursorColorEscape", "170;0;170");
            newParameters.setValue("CursorColorEscapeTime", 0.0);
            newParameters.setValue("TargetSpeed", 120);
            newParameters.setValue("TargetSequence", "");

            // get parameter values from app.config
            // cycle through app.config parameter values and try to set the parameter
            var appSettings = System.Configuration.ConfigurationManager.GetSection(CLASS_NAME) as NameValueCollection;
            if (appSettings != null) {
                for (int i = 0; i < appSettings.Count; i++) {

                    // message
                    logger.Info("Setting parameter '" + appSettings.GetKey(i) + "' to value '" + appSettings.Get(i) + "' from app.config.");

                    // set the value
                    newParameters.setValue(appSettings.GetKey(i), appSettings.Get(i));

                }
            }

            // configure task with new parameters
            configure(newParameters);

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

                // initialize the view
                initializeView();
                
                // (re-) initialize the block sequence
		        view.initBlockSequence(mTargetSequence, mTargets);
                
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

            // lock for thread safety and destroy the scene
            lock (lockView) {
                destroyView();
            }

        }

    }

}
