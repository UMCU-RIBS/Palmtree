using NLog;
using System;
using System.Collections.Generic;
using System.Threading;
using UNP.Applications;
using UNP.Core;
using UNP.Core.DataIO;
using UNP.Core.Helpers;
using UNP.Core.Params;

namespace CursorTask {

    public class CursorTask : IApplication, IApplicationUNP {

        enum TaskStates : int {
            Wait,
            CountDown,
            Task,
            EndText
        };

        enum TrialStates : int {
            PreTrial,           // rest before trial (cursor is hidden, only target is shown)
            Trial,              // trial (cursor is shown and moving)
            PostTrial,          // post trial (cursor will stay at the end)
            ITI                 // inter trial interval (both the cursor and the target are hidden)
        };

        private const int CLASS_VERSION = 1;
        private const string CLASS_NAME = "CursorTask";
        private const string CONNECTION_LOST_SOUND = "sounds\\focuson.wav";

        private static Logger logger = LogManager.GetLogger(CLASS_NAME);                        // the logger object for the view
        private static Parameters parameters = null;

        private int inputChannels = 0;
        private CursorView view = null;

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

        private int mTaskInputChannel = 1;                                          // input channel
        private int mTaskInputSignalType = 0;										// input signal type (0 = 0 to 1, 1 = -1 to 1)
        private int mTaskFirstRunStartDelay = 0;                                    // the first run start delay in sample blocks
        private int mTaskStartDelay = 0;									        // the run start delay in sample blocks
        private int mCountdownTime = 0;                                             // the time the countdown takes in sample blocks
        private bool mShowScore = false;

        private double mCursorSize = 1f;
        private RGBColorFloat mCursorColorNeutral = new RGBColorFloat(0.8f, 0.8f, 0f);
        private RGBColorFloat mCursorColorHit = new RGBColorFloat(0f, 1f, 0f);
        private RGBColorFloat mCursorColorMiss = new RGBColorFloat(1f, 0f, 0f);
        private RGBColorFloat mTargetColorNeutral = new RGBColorFloat(1f, 1f, 1f);
        private RGBColorFloat mTargetColorHit = new RGBColorFloat(0f, 1f, 0f);
        private RGBColorFloat mTargetColorMiss = new RGBColorFloat(1f, 0f, 0f);

        private bool mUpdateCursorOnSignal = false;                                 // update the cursor only on signal (or on smooth animation if false)
        private int mPreTrialDuration = 0;  								        // 
        private double mTrialDuration = 0;  								        // the total trial time (in seconds if animated, in samples if the cursor is updated by incoming signal)
        private int mPostTrialDuration = 0;  								        // 
        private int mITIDuration = 0;  								                // 

        private int[] fixedTargetSequence = new int[0];				                // the target sequence (input parameter)
        private int numTargets = 0;
        private int mTargetYMode = 0;
        private int mTargetHeightMode = 0;
        private List<List<float>> mTargets = new List<List<float>>() {              // the target definitions (1ste dimention are respectively Ys, Heights; 2nd dimension target options) 
            new List<float>(0),
            new List<float>(0)
        };

        // task (active) variables
        private List<int> mTargetSequence = new List<int>(0);					    // the target sequence being used in the task (can either be given by input or generated)
        private double mCursorSpeedY = 1;                                              // 

        private int mWaitCounter = 0;
        private int mCountdownCounter = 0;											// the countdown timer
        private int mCursorCounter = 0; 								            // cursor movement counter when the cursor is moved by the signal
        private int mHitScore = 0;                                                  // the score of the cursor hitting a block (in number of samples)

        private TaskStates taskState = TaskStates.Wait;
        private TaskStates previousTaskState = TaskStates.Wait;
        private TrialStates mTrialState = TrialStates.PreTrial;								// the state of the trial
        private int mCurrentTarget = 0;                                     // the target/trial in the targetsequence that is currently done
        


        public CursorTask() : this(false) { }
        public CursorTask(bool UNPMenuTask) {
            
            // transfer the UNP menu task flag
            mUNPMenuTask = UNPMenuTask;

            // check if the task is standalone (not unp menu)
            if (!mUNPMenuTask) {
            
                // create a parameter set for the task
                parameters = ParameterManager.GetParameters(CLASS_NAME, Parameters.ParamSetTypes.Application);

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
                    "0", "", "10s");

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
                    "Task input signal type.\n\nWith the direct input setting, the location of the cursor will be set directly by the input.\nWith added input, the cursor position will be set to the cursor's position plus the input multiplied by a cursor speed factor (this factor is based on the trialduration; 1 / trialduration in samples / 2)",
                    "0", "3", "0", new string[] { "Direct input (0 to 1)", "Direct input (-1 to 1)", "Constant middle", "Added input" });

                parameters.addParameter<bool>(
                    "TaskShowScore",
                    "Show the score",
                    "0", "1", "1");

                parameters.addParameter<double>(
                    "PreTrialDuration",
                    "Duration of displaying the target before the cursor appears and starts moving",
                    "0", "", "1s");

                parameters.addParameter<double>(
                    "TrialDuration",
                    "Duration per trial to hit the target. This is the time from when the cursor appears and starts moving till when it hits or misses the target",
                    "1", "", "2s");

                parameters.addParameter<double>(
                    "PostTrialDuration",
                    "Duration of result display after feedback.",
                    "0", "", "1s");

                parameters.addParameter<double>(
                    "ITIDuration",
                    "Duration of inter-trial interval. During this period both the cursor and target will be hidden and only the playfield rectangle will be shown",
                    "0", "", "2s");

                parameters.addParameter<double>(
                    "CursorSize",
                    "Cursor size radius in percentage of the bounding box size",
                    "0.0", "50.0", "4.0");

                parameters.addParameter<RGBColorFloat>(
                    "CursorColorNeutral",
                    "Cursor color when at the start or moving",
                    "", "", "204;204;0");

                parameters.addParameter<RGBColorFloat>(
                    "CursorColorHit",
                    "Cursor color when hitting the target",
                    "", "", "0;255;0");

                parameters.addParameter<RGBColorFloat>(
                    "CursorColorMiss",
                    "Cursor color when missing the target",
                    "", "", "255;0;0");

                parameters.addParameter<bool>(
                    "UpdateCursorOnSignal",
                    "Only update the cursor on incoming signal",
                    "", "", "1");

                parameters.addParameter<double[][]>(
                    "Targets",
                    "Target positions and heights in percentage coordinates\n\nY_perc: The y position of the target on the screen (in percentages of the screen height), note that the value specifies where the middle of the target will be.\nHeight_perc: The height of the block on the screen (in percentages of the screen height)",
                    "", "", "25,75;50,50", new string[] { "Y_perc", "Height_perc" });

                parameters.addParameter<int>(
                    "TargetYMode",
                    "Targets Y mode",
                    "0", "3", "1", new string[] { "Target(matrix) order", "Randomize categories", "Randomize cat without replacement", "Sequential categories with rnd start" });

                parameters.addParameter<int>(
                    "TargetHeightMode",
                    "Targets Height mode",
                    "0", "3", "1", new string[] { "Target(matrix) order", "Randomize categories", "Randomize cat without replacement", "Sequential categories with rnd start" });

                parameters.addParameter<RGBColorFloat>(
                    "TargetColorNeutral",
                    "Target color when the cursor is still at the start or moving",
                    "", "", "255;255;255");

                parameters.addParameter<RGBColorFloat>(
                    "TargetColorHit",
                    "Target color when the cursor hits the target",
                    "", "", "0;255;0");

                parameters.addParameter<RGBColorFloat>(
                    "TargetColorMiss",
                    "Target color when the cursor misses the target",
                    "", "", "255;0;0");

                parameters.addParameter<int>(
                    "NumberTargets",
                    "Number of targets",
                    "1", "", "70");

                parameters.addParameter<int[]>(
                    "TargetSequence",
                    "Fixed sequence in which targets should be presented (leave empty for random)\nNote. indexing is 0 based (so a value of 0 will be the first row from the 'Targets' parameter",
                    "0", "", "");





            }

            // message
            logger.Info("Application created (version " + CLASS_VERSION + ")");

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

        public bool configure(ref PackageFormat input) {

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

            // retrieve the countdown time
            mCountdownTime = parameters.getValueInSamples("CountdownTime");
            if (mCountdownTime < 0) {
                logger.Error("Countdown time cannot be less than 0");
                return false;
            }

            // retrieve the score parameter
            mShowScore = parameters.getValue<bool>("TaskShowScore");

            // retrieve the input signal type
            mTaskInputSignalType = parameters.getValue<int>("TaskInputSignalType");

            // retrieve and calculate cursor parameters
            mCursorSize                     = parameters.getValue<double>("CursorSize");
            mCursorColorNeutral             = parameters.getValue<RGBColorFloat>("CursorColorNeutral");
            mCursorColorHit                 = parameters.getValue<RGBColorFloat>("CursorColorHit");
            mCursorColorMiss                = parameters.getValue<RGBColorFloat>("CursorColorMiss");
            mUpdateCursorOnSignal           = parameters.getValue<bool>("UpdateCursorOnSignal");

            // retrieve the trial pre-duration parameters
            mPreTrialDuration = parameters.getValueInSamples("PreTrialDuration");
            if (mPreTrialDuration < 0) {
                logger.Error("The pre-trial duration parameter cannot be smaller than 0");
                return false;
            }

            // retrieve the trial duration parameters
            if (mUpdateCursorOnSignal)      mTrialDuration = parameters.getValueInSamples("TrialDuration");
            else                            mTrialDuration = parameters.getValue<double>("TrialDuration");
            if (mTrialDuration <= 0) {
                logger.Error("Trial duration parameter cannot be 0");
                return false;
            }
            // check if the type is added input
            if (mTaskInputSignalType == 3) {
                mCursorSpeedY = 1.0 / parameters.getValueInSamples("TrialDuration");
                //mCursorSpeedY = 1.0 / parameters.getValueInSamples("TrialDuration") / 2.0;
            }

            // retrieve the trial post-duration parameters
            mPostTrialDuration = parameters.getValueInSamples("PostTrialDuration");
            if (mPostTrialDuration < 0) {
                logger.Error("The post-trial duration parameter cannot be smaller than 0");
                return false;
            }

            // retrieve the ITI duration parameters
            mITIDuration = parameters.getValueInSamples("ITIDuration");
            if (mITIDuration < 0) {
                logger.Error("The ITI duration parameter cannot be smaller than 0");
                return false;
            }

            // retrieve target settings
            double[][] parTargets = parameters.getValue<double[][]>("Targets");
            if (parTargets.Length != 2 || parTargets[0].Length < 1) {
                logger.Error("Targets parameter must have at least 1 row and 2 columns (Y_perc, Height_perc)");
                return false;
            }

            // TODO: convert mTargets to 2 seperate arrays instead of jagged list?
            mTargets[0] = new List<float>(new float[parTargets[0].Length]);
            mTargets[1] = new List<float>(new float[parTargets[0].Length]);
            for (int row = 0; row < parTargets[0].Length; ++row) {
                mTargets[0][row] = (float)parTargets[0][row];
                mTargets[1][row] = (float)parTargets[1][row];
            }

            mTargetYMode = parameters.getValue<int>("TargetYMode");
            mTargetHeightMode = parameters.getValue<int>("TargetHeightMode");
            mTargetColorNeutral = parameters.getValue<RGBColorFloat>("TargetColorNeutral");
            mTargetColorHit = parameters.getValue<RGBColorFloat>("TargetColorHit");
            mTargetColorMiss = parameters.getValue<RGBColorFloat>("TargetColorMiss");

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

            // return succes
            return true;
        }

        public void initialize() {
                    
            // lock for thread safety
            lock (lockView) {

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
                    if (mTargetSequence.Count != 0) mTargetSequence.Clear();

                    // transfer the targetsequence
                    mTargetSequence = new List<int>(fixedTargetSequence);

                }

            }

        }


        private void initializeView() {

            // create the view
            view = new CursorView(mWindowRedrawFreqMax, mWindowLeft, mWindowTop, mWindowWidth, mWindowHeight, false);
            view.setBackgroundColor(mWindowBackgroundColor.getRed(), mWindowBackgroundColor.getGreen(), mWindowBackgroundColor.getBlue());

            // set task specific display attributes 
            view.setCursorSizePerc(mCursorSize);                         // cursor size radius in percentage of the screen height
            view.setCursorSpeed((float)mTrialDuration);                             // set the cursor speed
            view.setCursorNeutralColor(mCursorColorNeutral);                    // 
            view.setCursorHitColor(mCursorColorHit);                            // 
            view.setCursorMissColor(mCursorColorMiss);                          // 
            view.setTargetNeutralColor(mTargetColorNeutral);                    // 
            view.setTargetHitColor(mTargetColorHit);                            // 
            view.setTargetMissColor(mTargetColorMiss);                          // 
            view.centerCursorY();                                               // set the cursor to the middle of the screen
            view.setFixation(false);                                            // hide the fixation
            view.setCountDown(-1);                                              // hide the countdown

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

                if (view == null) return;

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

                        if (mWaitCounter == 0) {

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
                            view.setCountDown((int)Math.Floor((mCountdownCounter - 1) / MainThread.SamplesPerSecond()) + 1);

                            // reduce the countdown timer
                            mCountdownCounter--;

                        } else {
                            // done counting down

                            // hide the countdown counter
                            view.setCountDown(-1);

                            // set the current target/trial to the first target/trial
                            mCurrentTarget = 0;
                            setTarget();

                            // reset the score
                            mHitScore = 0;

                            // set the state to task
                            setState(TaskStates.Task);

                            // start pre-trial
                            startPreTrial();
                            
                        }

                        break;

                    case TaskStates.Task:


                        switch (mTrialState) {

                            case TrialStates.ITI:

                                if (mWaitCounter == 0) {

                                    // start pre-trial
                                    startPreTrial();

                                } else
                                    mWaitCounter--;

                                break;

                            case TrialStates.PreTrial:

                                if (mWaitCounter == 0) {

                                    // start the trial
                                    startTrial();

                                } else
                                    mWaitCounter--;

                                break;

                            case TrialStates.Trial:

                                // check if the cursor should move on each incoming signal
                                if (mUpdateCursorOnSignal) {
                                    // movement of the cursor on incoming signal

                                    // package came in, so raise the cursor counter
                                    mCursorCounter++;
                                    if (mCursorCounter > mTrialDuration) mCursorCounter = (int)mTrialDuration;

                                    // set the X position
                                    view.setCursorNormX(mCursorCounter / mTrialDuration, true);

                                }

                                // check the input type
                                if (mTaskInputSignalType == 0) {
                                    // Direct input (0 to 1)

                                    view.setCursorNormY(input); // setCursorNormY will take care of values below 0 or above 1)

                                } else if (mTaskInputSignalType == 1) {
                                    // Direct input (-1 to 1)

                                    view.setCursorNormY((input + 1.0) / 2.0);

                                } else if (mTaskInputSignalType == 3) {
                                    // Added input

                                    //logger.Error("----");
                                    //double y = view.getCursorY();
                                    double y = view.getCursorNormY();
                                    //logger.Error("input: " + input);
                                    //logger.Error("mCursorSpeedY: " + mCursorSpeedY);
                                    //logger.Error("y: " + y);
                                    y += mCursorSpeedY * input;
                                    //logger.Error("after y: " + y);
                                    view.setCursorNormY(y);
                                    //view.setCursorY(y);
                                    

                                } else {


                                }

                                // check if the end has been reached by the target (the trial has ended)
                                if (view.isCursorAtEnd(true)) {

                                    // set feedback as false
                                    Globals.setValue<bool>("Feedback", "0");

                                    // log event feedback is stopped
                                    Data.logEvent(2, "FeedbackStop", (view.isTargetHit() ? "1" : "0"));

                                    // check if the target was hit
                                    if (view.isTargetHit()) {
                                        // hit

                                        // set the cursor against the target
                                        view.setCursorNormX(1, true);

                                        // add to score if cursor hits the block
                                        mHitScore++;

                                        // update the score for display
                                        if (mShowScore) view.setScore(mHitScore);

                                    } else {
                                        // miss

                                        // set the cursor against the target
                                        view.setCursorNormX(1, false);

                                    }

                                    // start post-trial
                                    startPostTrial();

                                }

                                break;

                            case TrialStates.PostTrial:

                                if (mWaitCounter == 0) {

                                    // check if this was the last target/trial
                                    if (mCurrentTarget == mTargetSequence.Count - 1) {
                                        // end of the task

                                        // set the state to end
                                        setState(TaskStates.EndText);

                                    } else {
                                        // not end of task

                                        // continue to next trial (inside of this funtion it might stop go to ITI, or continue immediately to startPreTrial or startTrial)
                                        nextTrial();

                                    }

                                } else
                                    mWaitCounter--;

                                break;

                        }

                        break;

                    case TaskStates.EndText:
                        // end text

                        if (mWaitCounter == 0) {

                            // log event task is stopped
                            Data.logEvent(2, "TaskStop", CLASS_NAME + ";end");

                            // check if we are running from the UNPMenu
                            if (mUNPMenuTask) {

                                // stop the task (UNP)
                                UNP_stop();

                            } else {

                                // stop the run, this will also call stopTask()
                                MainThread.stop();
                            }

                        } else
                            mWaitCounter--;

                        break;
                }

            }

        }

        private void startPreTrial() {

            // log event pre-feedback is started
            Data.logEvent(2, "PreFeedbackStart", mCurrentTarget.ToString() + ";" + mTargetSequence[mCurrentTarget].ToString());

            // check if there is a pre-trial duration
            if (mPreTrialDuration == 0) {
                // no pre-trial duration

                // start the trial (immediately without the pre-trial duration)
                startTrial();

            } else {
                // pre-trial duration

                // set the pre trial duration
                mWaitCounter = mPreTrialDuration;

                // set the trial state to beginning trial
                setTrialState(TrialStates.PreTrial);

            }

        }

        private void startTrial() {

            // set the cursorcounter to 0 (is only used of the cursor is updated by the signal)
            mCursorCounter = 0;

            // set feedback as true
            Globals.setValue<bool>("Feedback", "1");
            Globals.setValue<int>("Target", mTargetSequence[mCurrentTarget].ToString());

            // log event feedback is started
            Data.logEvent(2, "FeedbackStart", mCurrentTarget.ToString() + ";" + mTargetSequence[mCurrentTarget].ToString());

            // set the trial state to trial
            setTrialState(TrialStates.Trial);

        }

        private void startPostTrial() {

            // check if there is a post-trial duration
            if (mPostTrialDuration == 0) {
                // no post-trial duration

                // check if this was the last target/trial
                if (mCurrentTarget == mTargetSequence.Count - 1) {
                    // end of the task

                    // set the state to end
                    setState(TaskStates.EndText);

                } else {
                    // not end of task

                    // continue to next trial (inside of this funtion it might stop go to ITI, or continue immediately to startPreTrial or startTrial)
                    nextTrial();

                }

            } else {
                // posttrial duration

                // set the wait after the trial
                mWaitCounter = mPostTrialDuration;

                // set to trial post state
                setTrialState(TrialStates.PostTrial);

            }

        }

        private void nextTrial() {


            // log event feedback is stopped
            Data.logEvent(2, "ITIStart", "");

            // goto the next target/trial
            mCurrentTarget++;
            setTarget();

            // check if a inter-trial interval was set
            if (mITIDuration == 0) {
                // no ITI duration

                // start pre-trial (inside of this funtion it might continue immediately to startTrial)
                startPreTrial();

            } else {
                // ITI duration

                // set the duration of the inter-trial-interval
                mWaitCounter = mITIDuration;

                // set the trial state to inter-trial-interval
                setTrialState(TrialStates.ITI);

            }

        }

        private void setTarget() {

            // center the cursor on the Y axis
            view.centerCursorY();

            // set the target
            int currentTargetY = (int)mTargets[0][mTargetSequence[mCurrentTarget]];
            int currentTargetHeight = (int)mTargets[1][mTargetSequence[mCurrentTarget]];
            view.setTarget(currentTargetY, currentTargetHeight);

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

                // destroy the view
                destroyView();

                // stop and clear the connection lost timer
                if (mConnectionLostSoundTimer != null) {
                    mConnectionLostSoundTimer.Stop();
                    mConnectionLostSoundTimer = null;
                }

            }

            // destroy/Cursor more task variables

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
            if (view == null) return;

            // log event task is paused
            Data.logEvent(2, "TaskPause", CLASS_NAME);

            // TODO: should log FeedbackSttop event, in case the task is within the trial

            // set task as pauzed
            mTaskPauzed = true;

	        // store the previous state
	        previousTaskState = taskState;

		    // hide everything
		    view.setFixation(false);
		    view.setCountDown(-1);
		    view.setCursorVisible(false);
		    view.setTargetVisible(false);
		    view.setScore(-1);

            // TODO: should store TrialState as well

        }

        // resumes the task
        private void resumeTask() {
            if (view == null) return;

            // log event task is paused
            Data.logEvent(2, "TaskResume", CLASS_NAME);

            // TODO: should log FeedbackStart event, in case the task is within the trial

            // set the previous gamestate
            setState(previousTaskState);

	        // set task as not longer pauzed
	        mTaskPauzed = false;

            // TODO: should be using setTrialState

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

                    // hide the cursor, target and score
				    view.setCursorVisible(false);
				    view.setCursorMoving(false);		// only moving is animated
				    view.setTargetVisible(false);
				    view.setScore(-1);

				    // hide the boundary
				    view.setBoundaryVisible(false);

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

                    // hide the cursor, target and boundary
                    view.setBoundaryVisible(false);
                    view.setCursorVisible(false);
                    view.setCursorMoving(false);        // only moving is animated
                    view.setTargetVisible(false);

                    // set countdown
                    if (mCountdownCounter > 0)
                        view.setCountDown((int)Math.Floor((mCountdownCounter - 1) / MainThread.SamplesPerSecond()) + 1);
                    else
                        view.setCountDown(-1);

                    break;


		        case TaskStates.Task:
                    // perform the task

                    // log event countdown is started
                    Data.logEvent(2, "TrialStart ", "");

				    // hide text if present
				    view.setText("");

                    // hide the countdown counter
                    view.setCountDown(-1);

                    // show the boundary
                    view.setBoundaryVisible(true);

                    // set the score for display
                    if (mShowScore) view.setScore(mHitScore);

                    break;

		        case TaskStates.EndText:
			        // show text

				    // stop the blocks from moving
				    //view.setBlocksMove(false);

				    // hide the boundary, target and cursor
				    view.setBoundaryVisible(false);
				    view.setCursorVisible(false);
				    view.setCursorMoving(false);		// only if moving is animated, do just in case
				    view.setTargetVisible(false);


                    // show text
                    view.setText("Done");

                    // set duration for text to be shown at the end (3s)
                    mWaitCounter = (int)(MainThread.SamplesPerSecond() * 3.0);


			        break;

	        }
        }


        private void setTrialState(TrialStates state) {

	        // Set state
	        mTrialState = state;
            
	        switch (mTrialState) {
		        case TrialStates.PreTrial:
                    // rest before trial (cursor is hidden, target is shown)

                    // set the cursor at the beginning
                    view.setCursorNormX(0, true);

                    // hide the cursor and make the cursor color neutral
                    view.setCursorVisible(false);
				    view.setCursorColor(CursorView.ColorStates.Neutral);

				    // show the target and make the target color neutral
				    view.setTargetVisible(true);
				    view.setTargetColor(CursorView.ColorStates.Neutral);

			        break;

		        case TrialStates.Trial:
			    // trial (cursor is shown and moving)

				    // make the cursor visible and make the cursor color neutral
				    view.setCursorVisible(true);
				    view.setCursorColor(CursorView.ColorStates.Neutral);
                        
				    // show the target and make the target color neutral
				    view.setTargetVisible(true);
				    view.setTargetColor(CursorView.ColorStates.Neutral);

				    // check if the cursor movement is animated, if so, set the cursor moving
				    if (!mUpdateCursorOnSignal)
					    view.setCursorMoving(true);

			        break;

		        case TrialStates.PostTrial:
			        // post trial (cursor will stay at the end)

				    // make the cursor visible
				    view.setCursorVisible(true);

				    // show the target
				    view.setTargetVisible(true);

				    // check if it was a hit or a miss
				    if (view.isTargetHit()) {
					    // hit
					    view.setCursorColor(CursorView.ColorStates.Hit);
					    view.setTargetColor(CursorView.ColorStates.Hit);
				    } else {
					    // miss
					    view.setCursorColor(CursorView.ColorStates.Miss);
					    view.setTargetColor(CursorView.ColorStates.Miss);
				    }

				    // check if the cursor movement is animated, if so, stop the cursor from moving
				    if (!mUpdateCursorOnSignal)
					    view.setCursorMoving(false);

			        break;

                case TrialStates.ITI:
                    // rest before trial (cursor and target are hidden)
                    
                    // hide the cursor
                    view.setCursorVisible(false);

                    // hide the target
                    view.setTargetVisible(false);

                    break;
            }

        }
        
        // Stop the task
        private void stopTask() {
            if (view == null) return;

            // set feedback as false
            Globals.setValue<bool>("Feedback", "0");

            // log event feedback is stopped
            Data.logEvent(2, "FeedbackStop", "user");

            // set state to wait
            setState(TaskStates.Wait);

            // initialize the target sequence already for a possible next run
            if (fixedTargetSequence.Length == 0) {

                // Generate targetlist
                generateTargetSequence();

            }

        }


        private void generateTargetSequence() {
	        
	        // clear the targets
	        if (mTargetSequence.Count != 0)		mTargetSequence.Clear();

	        // create targetsequence array with <NumberTargets>
            mTargetSequence = new List<int>(new int[numTargets]);

	        // put the row indices of each distinct value (from the rows in the matrix) in an array
	        // (this is used for the modes which are set to randomization)
            List<int> catY_unique = new List<int>(0);
            List<List<int>> catY = new List<List<int>>(0);
            List<int> catHeight_unique = new List<int>(0);
            List<List<int>> catHeight = new List<List<int>>(0);

            // 
            int i = 0;
            int j = 0;

            // loop through the target rows
	        for (i = 0; i < mTargets[0].Count; ++i) {

		        // get the values for the row
                int valueY = (int)mTargets[0][i];
		        int valueHeight = (int)mTargets[1][i];
		
		        // store the unique values and indices
		        for (j = 0; j < catY_unique.Count; ++j)
			        if (catY_unique[j] == valueY)	break;
		        if (j == catY_unique.Count) {
			        catY_unique.Add(valueY);						// store the unique value at index j
			        catY.Add(new List<int>(0));				        // store the targets row index in the vector at index j	
						
		        }
		        catY[j].Add(i);

		        for (j = 0; j < catHeight_unique.Count; ++j)
			        if (catHeight_unique[j] == valueHeight)	break;
		        if (j == catHeight_unique.Count) {
			        catHeight_unique.Add(valueHeight);			    // store the unique value at index j
			        catHeight.Add(new List<int>(0));			    // store the targets row index in the vector at index j							
		        }
		        catHeight[j].Add(i);

	        }

	        // create the arrays to handle the no replace randomization (in case it is needed)
            List<int> catY_noReplace = new List<int>(0);
            List<int> catHeight_noReplace = new List<int>(0);

	        // create random start for each categories (in case it is needed)
	        int catY_randStart = rand.Next(0, catY.Count);
	        int catHeight_randStart = rand.Next(0, catHeight.Count);

	        bool catY_randStart_Added = false;
	        bool catHeight_randStart_Added = false;

	        // create a target sequence
            List<int> currentY = new List<int>(0);          // initial value should be overwritten, but just in case
            List<int> currentHeight = new List<int>(0);

	        // loop <NumberTargets> times to generate each target
	        int generateSafetyCounter = numTargets + 1000;
            i = 0;
            while(i < numTargets) {
			
		        // none been added at the beginning of the loop
		        catY_randStart_Added = false;
		        catHeight_randStart_Added = false;

		        // count the loops and check for generation
		        if (generateSafetyCounter-- == 0) {
                    logger.Error("Error generating random sequence, the generation rules/parameters (TargetYMode, TargetHeightMode and Target) cause a stalemate");
			        return;
		        }

			
		        // check Y mode
		        if (mTargetYMode == 0) {			// 0: Target(matrix) order
			
			
		        } else if (mTargetYMode == 1) {	// 1: randomize categories
			        currentY = catY[rand.Next(0, catY.Count)];

		        } else if (mTargetYMode == 2) {	// 2:random categories without replacement
				
			        if (catY_noReplace.Count == 0) {

				        catY_noReplace = new List<int>(new int[catY.Count]);
				        for (j = 0; j < catY_noReplace.Count; ++j)	catY_noReplace[j] = j;
					
                        catY_noReplace.Shuffle();
			        }

			        currentY = catY[catY_noReplace[catY_noReplace.Count - 1]];

                    catY_noReplace.RemoveAt(catY_noReplace.Count -1);

		        } else if (mTargetYMode == 3) {	// 3:sequential categories with rnd start
			
			        currentY = catY[catY_randStart];
			        catY_randStart++;
			        if (catY_randStart == catY.Count)		catY_randStart = 0;
			        catY_randStart_Added = true;

		        }

		        // check Height mode
		        if (mTargetHeightMode == 0) {			// 0: Target(matrix) order
			
			
		        } else if (mTargetHeightMode == 1) {	// 1: randomize categories
			        currentHeight = catHeight[rand.Next(0, catHeight.Count)];
                    
		        } else if (mTargetHeightMode == 2) {	// 2:random categories without replacement
			        if (catHeight_noReplace.Count == 0) {
				        
                        catHeight_noReplace = new List<int>(new int[catHeight.Count]);
				        for (j = 0; j < catHeight_noReplace.Count; ++j)	catHeight_noReplace[j] = j;
				        
                        catHeight_noReplace.Shuffle();

			        }
			        currentHeight = catHeight[catHeight_noReplace[catHeight_noReplace.Count - 1]];
                    catHeight_noReplace.RemoveAt(catHeight_noReplace.Count -1);

		        } else if (mTargetHeightMode == 3) {	// 3:sequential categories with rnd start
			        currentHeight = catHeight[catHeight_randStart];
			        catHeight_randStart++;
			        if (catHeight_randStart == catHeight.Count)		catHeight_randStart = 0;
			        catHeight_randStart_Added = true;

		        }

		        // find a target all modes agree on
		        List<int> currentTarget = new List<int>(new int[mTargets[0].Count]);
		        for (j = 0; j < currentTarget.Count; ++j)	currentTarget[j] = j;
                j = 0;
		        while(j < (int)currentTarget.Count) {

			        // clear out all the target indices which are not in the currentY
			        bool found = false;
			        for (int k = 0; k < currentY.Count; ++k) {
				        if (currentTarget[j] == currentY[k]) {
					        found = true;	break;
				        }
			        }
			        if (!found && j < currentTarget.Count && currentTarget.Count != 0) {
                        currentTarget.Swap(j, currentTarget.Count - 1);
                        currentTarget.RemoveAt(currentTarget.Count - 1);
				        continue;
			        }

			        // clear out all the target indices which are not in the currentHeight
			        found = false;
			        for (int k = 0; k < currentHeight.Count; ++k) {
				        if (currentTarget[j] == currentHeight[k]) {
					        found = true;	break;
				        }
			        }
			        if (!found && currentTarget.Count != 0) {
				        currentTarget.Swap(j, currentTarget.Count - 1);
                        currentTarget.RemoveAt(currentTarget.Count - 1);
				        continue;
			        }

			        // go to the next element
			        j++;

		        }

		        // check if a (agreeable) target has been found
		        if (currentTarget.Count != 0) {
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
				        if (catY_randStart < 0 )		    catY_randStart = (int)catY.Count - 1;
			        }
			        if (catHeight_randStart_Added) {
				        catHeight_randStart--;
				        if (catHeight_randStart < 0 )		catHeight_randStart = (int)catHeight.Count - 1;
			        }
			
		        }

	        }

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

            // set the parameter set as not visible (for GUI configuration)
            //parameters.ParamSetVisible = false;

            // transfer the window settings
            mWindowRedrawFreqMax = parentParameters.getValue<int>("WindowRedrawFreqMax");      // the view update frequency (in maximum fps)
            //mWindowed = true;
            mWindowWidth = parentParameters.getValue<int>("WindowWidth"); ;
            mWindowHeight = parentParameters.getValue<int>("WindowHeight"); ;
            mWindowLeft = parentParameters.getValue<int>("WindowLeft"); ;
            mWindowTop = parentParameters.getValue<int>("WindowTop"); ;
            //mFullscreenMonitor = 0;


            // set the UNP task standard settings
            mShowScore = true;
            mTaskInputSignalType = 1;
            mTaskInputChannel = 1;
            mTaskFirstRunStartDelay = 5;
            mTaskStartDelay = 10;
	        mUpdateCursorOnSignal = true;
	        mPreTrialDuration = (int)(MainThread.SamplesPerSecond() * 1.0);             // 
            mTrialDuration = (int)(MainThread.SamplesPerSecond() * 2.0); ;              // depends on 'mUpdateCursorOnSignal', now set to packages
            mPostTrialDuration = (int)(MainThread.SamplesPerSecond() * 1.0); ;          // 
            mITIDuration = (int)(MainThread.SamplesPerSecond() * 2.0); ;                // 

            mCursorSize = 4f;



            numTargets = 10;
            mTargetYMode = 1;				// random categories
	        mTargetHeightMode = 1;          // random categories
            mTargets[0].Clear(); mTargets[0] = new List<float>(new float[2]);
            mTargets[1].Clear(); mTargets[1] = new List<float>(new float[2]);
            mTargets[0][0] = 25; mTargets[1][0] = 50;
            mTargets[0][1] = 75; mTargets[1][1] = 50;

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
                if (!mUNPMenuTaskSuspended) process(input);

            }
            
        }

        public void UNP_resume() {


            // lock for thread safety
            lock (lockView) {

                // initialize the view
                initializeView();

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
