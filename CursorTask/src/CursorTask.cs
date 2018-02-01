/**
 * The CursorTask class
 * 
 * ...
 * 
 * 
 * Copyright (C) 2017:  RIBS group (Nick Ramsey Lab), University Medical Center Utrecht (The Netherlands) & external contributors
 * Author(s):           Max van den Boom            (info@maxvandenboom.nl)
 * 
 * Adapted from:        BCI2000 (Schalk Lab, www.schalklab.org) and Erik Aarnoutse (E.J.Aarnoutse@umcutrecht.nl)
 * 
 * This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software
 * Foundation, either version 3 of the License, or (at your option) any later version. This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for
 * more details. You should have received a copy of the GNU General Public License along with this program. If not, see <https://www.gnu.org/licenses/>.
 */
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

    /// <summary>
    /// The <c>CursorTask</c> class.
    /// 
    /// ...
    /// </summary>
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

        private const int CLASS_VERSION = 3;
        private const string CLASS_NAME = "CursorTask";
        private const string CONNECTION_LOST_SOUND = "sounds\\focuson.wav";

        private const string TARGET_MODE_DESC = "For every trial, within every column (so within the y, height.. etc), a option (category) is selected given the Target...Mode.\n\n" +
                                                "To determine which target is used, first the deterministic modes are applied (which are '3. sequential with rnd start' and '4. matrix order').\n" +
                                                "Then, depending on which modes are set to '2. Randomize categories (balanced'), the unique targets that are left are indexed and balanced out.\n" +
                                                "Within that selection of unique targets either a target is chosen at random ('1. Randomize categories unbalanced') or just the first ('0. None')\n\n" +
                                                "If a randomization stalemate is produced then an error will be given and more\n" +
                                                "degrees of freedom should be given (either by extending the target options or choosing different Target..Modes.";

        private static Logger logger = LogManager.GetLogger(CLASS_NAME);                        // the logger object for the view
        private static Parameters parameters = null;

        private int inputChannels = 0;
        private CursorView view = null;

        private Random rand = new Random(Guid.NewGuid().GetHashCode());
        private Object lockView = new Object();                                     // threadsafety lock for all event on the view
        private bool taskPauzed = false;								            // flag to hold whether the task is pauzed (view will remain active, e.g. connection lost)

        private bool unpMenuTask = false;								            // flag whether the task is created by the UNPMenu
        private bool unpMenuTaskRunning = false;						            // flag to hold whether the task should is running (setting this to false is also used to notify the UNPMenu that the task is finished)
        private bool unpMenuTaskSuspended = false;						            // flag to hold whether the task is suspended (view will be destroyed/re-initiated)

        private bool connectionLost = false;							            // flag to hold whether the connection is lost
        private bool connectionWasLost = false;						                // flag to hold whether the connection has been lost (should be reset after being re-connected)

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
        private bool mShowScore = false;                                            // show the number of hits during the task
        private bool mShowScoreAtEnd = false;                                       // show the percentage correct at the end of the task

        private double mCursorSize = 1f;
        private RGBColorFloat mCursorColorNeutral = new RGBColorFloat(0.8f, 0.8f, 0f);
        private RGBColorFloat mCursorColorHit = new RGBColorFloat(0f, 1f, 0f);
        private RGBColorFloat mCursorColorMiss = new RGBColorFloat(1f, 0f, 0f);
        private RGBColorFloat mTargetColorNeutral = new RGBColorFloat(1f, 1f, 1f);
        private RGBColorFloat mTargetColorHit = new RGBColorFloat(0f, 1f, 0f);
        private RGBColorFloat mTargetColorMiss = new RGBColorFloat(1f, 0f, 0f);

        private bool mUpdateCursorOnSignal = false;                                 // update the cursor only on signal (or on smooth animation if false)
        private bool mPreTrialCues = false;                                         // whether pre-trial cue texts should be presented
        private string[] mPreTrialCueTexts = new string[0];                         // string array holding the configured pre-trial cue texts
        private int mPreTrialDuration = 0;  								        // 
        private double mTrialDuration = 0;  								        // the total trial time (in seconds if animated, in samples if the cursor is updated by incoming signal)
        private int mPostTrialDuration = 0;  								        // 
        private int mITIDuration = 0;  								                // 

        private int[] fixedTrialSequence = new int[0];				                // the trial sequence (input parameter)
        private int numTrials = 0;
        private int mTargetYMode = 0;
        private int mTargetHeightMode = 0;
        private List<List<float>> mTargets = new List<List<float>>() {              // the target definitions (1ste dimention are respectively Ys, Heights; 2nd dimension target options) 
            new List<float>(0),
            new List<float>(0)
        };

        // task (active) variables
        private List<int> trialSequence = new List<int>(0);					        // the target sequence being used in the task (can either be given by input or generated)
        private double cursorSpeedY = 1;                                            // 

        private int waitCounter = 0;
        private int countdownCounter = 0;											// the countdown timer
        private int cursorCounter = 0; 								                // cursor movement counter when the cursor is moved by the signal
        private int hitScore = 0;                                                   // the score of the cursor hitting a block (in number of samples)

        private TaskStates taskState = TaskStates.Wait;
        private TaskStates previousTaskState = TaskStates.Wait;
        private TrialStates trialState = TrialStates.PreTrial;						// the state of the trial
        private int currentTrial = 0;                                               // the trial in the trial sequence that is currently done
        


        public CursorTask() : this(false) { }
        public CursorTask(bool UNPMenuTask) {
            
            // transfer the UNP menu task flag
            unpMenuTask = UNPMenuTask;

            // check if the task is standalone (not unp menu)
            if (!unpMenuTask) {
            
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
                    "ShowScore",
                    "Show the number of targets hit during the task",
                    "0", "1", "0");

                parameters.addParameter<bool>(
                    "ShowScoreAtEnd",
                    "Show the percentage of targets hit at the end",
                    "0", "1", "1");

                parameters.addParameter<bool>(
                    "PreTrialCues",
                    "Present cue texts during pre-trial duration (target and cue are shown before the cursor appears)",
                    "0", "1", "0");

                parameters.addParameter<string[][]>(
                    "PreTrialCueTexts",
                    "Cue texts that are displayed sequentially",
                    "", "", "cue1,cue2", new string[] { "Text" });

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
                    "Targets Y mode\n\n" + TARGET_MODE_DESC,
                    "0", "4", "3", new string[] { "0. None", "1. Randomize categories (unbalanced)", "2. Randomize categories (balanced)", "3. Sequential categories with rnd start", "4. Target(matrix) order" });

                parameters.addParameter<int>(
                    "TargetHeightMode",
                    "Targets Height mode\n\n" + TARGET_MODE_DESC,
                    "0", "4", "0", new string[] { "0. None", "1. Randomize categories (unbalanced)", "2. Randomize categories (balanced)", "3. Sequential categories with rnd start", "4. Target(matrix) order" });

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
                    "NumberOfTrials",
                    "Number of trials",
                    "1", "", "70");

                parameters.addParameter<int[]>(
                    "TrialSequence",
                    "Fixed trial sequence in which targets should be presented (leave empty for random)\nNote. indexing is 0 based (so a value of 0 will be the first row from the 'Targets' parameter",
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

            // retrieve the score parameters
            mShowScore = parameters.getValue<bool>("ShowScore");
            mShowScoreAtEnd = parameters.getValue<bool>("ShowScoreAtEnd");

            // retrieve the input signal type
            mTaskInputSignalType = parameters.getValue<int>("TaskInputSignalType");

            // retrieve and calculate cursor parameters
            mCursorSize                     = parameters.getValue<double>("CursorSize");
            mCursorColorNeutral             = parameters.getValue<RGBColorFloat>("CursorColorNeutral");
            mCursorColorHit                 = parameters.getValue<RGBColorFloat>("CursorColorHit");
            mCursorColorMiss                = parameters.getValue<RGBColorFloat>("CursorColorMiss");
            mUpdateCursorOnSignal           = parameters.getValue<bool>("UpdateCursorOnSignal");

            // retrieve the pre-trial cue parameters
            mPreTrialCues = parameters.getValue<bool>("PreTrialCues");
            string[][] cuesTexts = parameters.getValue<string[][]>("PreTrialCueTexts");
            if (cuesTexts.Length != 0 && cuesTexts.Length != 1) {
                logger.Error("PreTrialCueTexts parameter must have 1 column (Texts)");
                return false;
            }
            mPreTrialCueTexts = new string[0];
            if (cuesTexts.Length > 0)   mPreTrialCueTexts = cuesTexts[0];

            // retrieve the pre-trial duration parameter
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
                cursorSpeedY = 1.0 / parameters.getValueInSamples("TrialDuration");
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

            // retrieve the number of targets and (fixed) trial sequence
            numTrials = parameters.getValue<int>("NumberOfTrials");
            fixedTrialSequence = parameters.getValue<int[]>("TrialSequence");
            if (fixedTrialSequence.Length == 0) {
                // no fixed sequence

                // check number of trials
                if (numTrials < 1) {
                    logger.Error("Minimum of 1 trial is required");
                    return false;
                }

            } else {
                // fixed sequence

                numTrials = fixedTrialSequence.Length;
                for (int i = 0; i < numTrials; ++i) {

                    if (fixedTrialSequence[i] < 0) {
                        logger.Error("The TrialSequence parameter contains a target index (" + fixedTrialSequence[i] + ") that is below zero, check the TrialSequence");
                        return false;
                    }
                    if (fixedTrialSequence[i] >= mTargets[0].Count) {
                        logger.Error("The TrialSequence parameter contains a target index (" + fixedTrialSequence[i] + ") that is out of range, check the Targets parameter. (note that the indexing is 0 based)");
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

                // check if a trial sequence is set
                if (fixedTrialSequence.Length == 0) {
                    // TrialSequence not set in parameters, generate

                    // Generate targetlist
                    generateTrialSequence();

                } else {
                    // TrialSequence is set in parameters

                    // clear the trial sequence
                    if (trialSequence.Count != 0) trialSequence.Clear();

                    // transfer the targets in the trial sequence
                    trialSequence = new List<int>(fixedTrialSequence);

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
            if (!unpMenuTask) {

                // store the generated sequence in the output parameter xml
                Data.adjustXML(CLASS_NAME, "TrialSequence", string.Join(" ", trialSequence));

            }

            // lock for thread safety
            lock (lockView) {

                if (view == null)   return;

                // log event task is started
                Data.logEvent(2, "TaskStart", CLASS_NAME);

                // reset the score
                hitScore = 0;

                // reset countdown to the countdown time
                countdownCounter = mCountdownTime;

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

            // stop the connection lost sound from playing
            SoundHelper.stopContinuous();

            // lock for thread safety
            lock (lockView) {

                // stop the task
                stopTask();

            }

            // log event app is stopped
            Data.logEvent(2, "AppStopped", CLASS_NAME);

        }

        public bool isStarted() {
            return true;
        }

        public void process(double[] input) {

            // retrieve the connectionlost global
            connectionLost = Globals.getValue<bool>("ConnectionLost");

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
                if (connectionLost) {

                    // check if it was just discovered if the connection was lost
                    if (!connectionWasLost) {
                        // just discovered it was lost

                        // set the connection as was lost (this also will make sure the lines in this block willl only run once)
                        connectionWasLost = true;

                        // pauze the task
                        pauzeTask();

                        // show the lost connection warning
                        view.setConnectionLost(true);

                        // play the connection lost sound continuously every 2 seconds
                        SoundHelper.playContinuousAtInterval(CONNECTION_LOST_SOUND, 2000);

                    }

                    // do not process any further
                    return;

                } else if (connectionWasLost && !connectionLost) {
                    // if the connection was lost and is not lost anymore

                    // stop the connection lost sound from playing
                    SoundHelper.stopContinuous();

                    // hide the lost connection warning
                    view.setConnectionLost(false);

                    // resume task
                    resumeTask();

                    // reset connection lost variables
                    connectionWasLost = false;

                }

                ////////////////////////
                // END CONNECTION FILTER ACTIONS//
                ////////////////////////



                // check if the task is pauzed, do not process any further if this is the case
                if (taskPauzed)		    return;


                // use the task state
                switch (taskState) {

                    case TaskStates.Wait:
                        // starting, pauzed or waiting

                        if (waitCounter == 0) {

                            // set the state to countdown
                            setState(TaskStates.CountDown);

                        } else
                            waitCounter--;

                        break;

                    case TaskStates.CountDown:
                        // Countdown before start of task

                        // check if the task is counting down
                        if (countdownCounter > 0) {

                            // still counting down

                            // display the countdown
                            view.setCountDown((int)Math.Floor((countdownCounter - 1) / MainThread.getPipelineSamplesPerSecond()) + 1);

                            // reduce the countdown timer
                            countdownCounter--;

                        } else {
                            // done counting down

                            // hide the countdown counter
                            view.setCountDown(-1);

                            // set the current target/trial to the first target/trial
                            currentTrial = 0;
                            setTarget();

                            // reset the score
                            hitScore = 0;

                            // set the state to task
                            setState(TaskStates.Task);

                            // start pre-trial
                            startPreTrial();
                            
                        }

                        break;

                    case TaskStates.Task:


                        switch (trialState) {

                            case TrialStates.ITI:

                                if (waitCounter == 0) {

                                    // start pre-trial
                                    startPreTrial();

                                } else
                                    waitCounter--;

                                break;

                            case TrialStates.PreTrial:

                                if (waitCounter == 0) {

                                    // start the trial
                                    startTrial();

                                } else
                                    waitCounter--;

                                break;

                            case TrialStates.Trial:

                                // check if the cursor should move on each incoming signal
                                if (mUpdateCursorOnSignal) {
                                    // movement of the cursor on incoming signal

                                    // package came in, so raise the cursor counter
                                    cursorCounter++;
                                    if (cursorCounter > mTrialDuration) cursorCounter = (int)mTrialDuration;

                                    // set the X position
                                    view.setCursorNormX(cursorCounter / mTrialDuration, true);

                                }

                                // check the input type
                                if (mTaskInputSignalType == 0) {
                                    // Direct input (0 to 1)

                                    view.setCursorNormY(input); // setCursorNormY will take care of values below 0 or above 1)

                                } else if (mTaskInputSignalType == 1) {
                                    // Direct input (-1 to 1)

                                    view.setCursorNormY((input + 1.0) / 2.0);

                                } else if (mTaskInputSignalType == 2) {
                                    // Constant middle

                                    view.setCursorNormY(0.5);

                                } else if (mTaskInputSignalType == 3) {
                                    // Added input

                                    double y = view.getCursorNormY();
                                    y += cursorSpeedY * input;
                                    view.setCursorNormY(y);

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
                                        hitScore++;

                                        // update the score for display
                                        if (mShowScore) view.setScore(hitScore);

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

                                if (waitCounter == 0) {

                                    // check if this was the last target/trial
                                    if (currentTrial == trialSequence.Count - 1) {
                                        // end of the task

                                        // calculate the percentage correct
                                        int percCorrect = (int)Math.Round(((double)hitScore / trialSequence.Count) * 100);

                                        // log event task score
                                        Data.logEvent(2, "TaskScore", hitScore + ";" + trialSequence.Count + ";" + percCorrect);

                                        // set the state to end
                                        setState(TaskStates.EndText);

                                    } else {
                                        // not end of task

                                        // continue to next trial (inside of this funtion it might stop go to ITI, or continue immediately to startPreTrial or startTrial)
                                        nextTrial();

                                    }

                                } else
                                    waitCounter--;

                                break;

                        }

                        break;

                    case TaskStates.EndText:
                        // end text

                        if (waitCounter == 0) {

                            // log event task is stopped
                            Data.logEvent(2, "TaskStop", CLASS_NAME + ";end");

                            // stop the task
                            // this will also call stop(), and as a result stopTask()
                            if (unpMenuTask)        UNP_stop();
                            else                    MainThread.stop(false);

                        } else
                            waitCounter--;

                        break;
                }

            }

        }

        private void startPreTrial() {

            // log event pre-feedback is started
            Data.logEvent(2, "PreFeedbackStart", currentTrial.ToString() + ";" + trialSequence[currentTrial].ToString());

            // check if there is a pre-trial duration
            if (mPreTrialDuration == 0) {
                // no pre-trial duration

                // start the trial (immediately without the pre-trial duration)
                startTrial();

            } else {
                // pre-trial duration

                // set the pre trial duration
                waitCounter = mPreTrialDuration;

                // set the trial state to beginning trial
                setTrialState(TrialStates.PreTrial);

            }

        }

        private void startTrial() {

            // set the cursorcounter to 0 (is only used of the cursor is updated by the signal)
            cursorCounter = 0;

            // set feedback as true
            Globals.setValue<bool>("Feedback", "1");
            Globals.setValue<int>("Target", trialSequence[currentTrial].ToString());

            // log event feedback is started
            Data.logEvent(2, "FeedbackStart", currentTrial.ToString() + ";" + trialSequence[currentTrial].ToString());

            // set the trial state to trial
            setTrialState(TrialStates.Trial);

        }

        private void startPostTrial() {

            // check if there is a post-trial duration
            if (mPostTrialDuration == 0) {
                // no post-trial duration

                // check if this was the last target/trial
                if (currentTrial == trialSequence.Count - 1) {
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
                waitCounter = mPostTrialDuration;

                // set to trial post state
                setTrialState(TrialStates.PostTrial);

            }

        }

        private void nextTrial() {


            // log event feedback is stopped
            Data.logEvent(2, "ITIStart", "");

            // goto the next target/trial
            currentTrial++;
            setTarget();

            // check if a inter-trial interval was set
            if (mITIDuration == 0) {
                // no ITI duration

                // start pre-trial (inside of this funtion it might continue immediately to startTrial)
                startPreTrial();

            } else {
                // ITI duration

                // set the duration of the inter-trial-interval
                waitCounter = mITIDuration;

                // set the trial state to inter-trial-interval
                setTrialState(TrialStates.ITI);

            }

        }

        private void setTarget() {

            // center the cursor on the Y axis
            view.centerCursorY();

            // set the target
            int currentTargetY = (int)mTargets[0][trialSequence[currentTrial]];
            int currentTargetHeight = (int)mTargets[1][trialSequence[currentTrial]];
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
            taskPauzed = true;

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
	        taskPauzed = false;

            // TODO: should be using setTrialState

        }


        private void setState(TaskStates state) {

	        // Set state
	        taskState = state;

            switch (state) {
                case TaskStates.Wait:
                    // starting, pauzed or waiting

                    // hide the cue text
                    view.setCueText("");

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
                        waitCounter = mTaskFirstRunStartDelay;
                        mTaskFirstRunStartDelay = 0;
                    } else
                        waitCounter = mTaskStartDelay;

                    break;

		        case TaskStates.CountDown:
                    // countdown when task starts

                    // log event countdown is started
                    Data.logEvent(2, "CountdownStarted ", "");

                    // hide the cue text
                    view.setCueText("");

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
                    if (countdownCounter > 0)
                        view.setCountDown((int)Math.Floor((countdownCounter - 1) / MainThread.getPipelineSamplesPerSecond()) + 1);
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
                    if (mShowScore) view.setScore(hitScore);

                    break;

		        case TaskStates.EndText:
                    // show text

                    // stop the blocks from moving
                    //view.setBlocksMove(false);

                    // hide the cue text
                    view.setCueText("");

                    // hide the boundary, target and cursor
                    view.setBoundaryVisible(false);
				    view.setCursorVisible(false);
				    view.setCursorMoving(false);		// only if moving is animated, do just in case
				    view.setTargetVisible(false);

                    // calculate the percentage correct
                    int percCorrect = (int)Math.Round(((double)hitScore / trialSequence.Count) * 100);

                    // show text
                    if (mShowScoreAtEnd)
                        view.setText("Done - " + percCorrect + "% hit");
                    else
                        view.setText("Done");

                    // set duration for text to be shown at the end (3s)
                    waitCounter = (int)(MainThread.getPipelineSamplesPerSecond() * 5.0);


			        break;

	        }
        }


        private void setTrialState(TrialStates state) {

	        // Set state
	        trialState = state;
            
	        switch (trialState) {
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
                    
                    // set the cue text
                    if (mPreTrialCues && mPreTrialCueTexts.Length > 0) {
                        string cueText = mPreTrialCueTexts[(currentTrial % mPreTrialCueTexts.Length)];
                        view.setCueText(cueText);
                    }

			        break;

		        case TrialStates.Trial:
			    // trial (cursor is shown and moving)

                    // hide the cue text
                    view.setCueText("");

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

                    // hide the cue text
                    view.setCueText("");

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
                    // rest before trial (cue, cursor and target are hidden)

                    // hide the cue text
                    view.setCueText("");

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
            if (fixedTrialSequence.Length == 0) {

                // Generate targetlist
                generateTrialSequence();

            }

        }

        
        private void generateTrialSequence() {
            int i;

	        // clear the targets
	        if (trialSequence.Count != 0)		trialSequence.Clear();
            
	        // create a trial sequence array with <NumberOfTrials>
            trialSequence = new List<int>(new int[numTrials]);

            // create a array with all targets (will be reused often, so do it once here and make copies)
            int[] allTargets = new int[mTargets[0].Count];
            for (int t = 0; t < mTargets[0].Count; ++t) allTargets[t] = t;

            // variables to hold the unique options per column (global because unmodified over loops)
            List<int> gbl_catY_unique = null;
            List<List<int>> gbl_catY = null;
            List<int> gbl_catHeight_unique = null;
            List<List<int>> gbl_catHeight = null;

            // list the unique options per column (based on all possible targets)
            generateTrialSequence_indexTargets(new List<int>(allTargets), out gbl_catY_unique, out gbl_catY, out gbl_catHeight_unique, out gbl_catHeight);

            // create counters for the targetOrder (in case it is needed)
            int catY_targetOrder = 0;
            int catHeight_targetOrder = 0;

	        // create random start for each categories (in case it is needed)
	        int catY_randStart = rand.Next(0, gbl_catY.Count);
	        int catHeight_randStart = rand.Next(0, gbl_catHeight.Count);
            
            // create a target sequence
            List<int> currentY = new List<int>(0);
            List<int> currentHeight = new List<int>(0);

            // variable to store the subset option lists (over loops)
            Dictionary<string, List<List<int>>> subSelectionOptions = new Dictionary<string, List<List<int>>>();

            // loop <NumberTrials> times to generate each trial
            int generateSafetyCounter = numTrials + 1000;
            i = 0;
            while(i < numTrials) {

                //
                // loop safety (no infinite loops)
                //

                if (generateSafetyCounter-- == 0) {
                    logger.Error("Error generating random sequence, the generation rules/parameters (TargetYMode, TargetHeightMode and Target) cause a stalemate");
                    return;
                }

                // variables to (within this loop) hold unique options
                List<int> lcl_catY_unique = null;
                List<List<int>> lcl_catY = null;
                List<int> lcl_catHeight_unique = null;
                List<List<int>> lcl_catHeight = null;
                
                //
                //
                // list the unique options per column (first based on all possible targets)
                generateTrialSequence_indexTargets(new List<int>(allTargets), out lcl_catY_unique, out lcl_catY, out lcl_catHeight_unique, out lcl_catHeight);

                //
                // first limit the choices for the deterministic modes
                //

                if (mTargetYMode == 3) {                     // 3:sequential categories with rnd start
                    currentY = lcl_catY[catY_randStart];
                } else if (mTargetYMode == 4) {                     // 4: Target(matrix) order
                    currentY = new List<int>(1) { catY_targetOrder };
                } else {
                    currentY = new List<int>(allTargets);
                }

                if (mTargetHeightMode == 3) {                // 3:sequential categories with rnd start
                    currentHeight = lcl_catHeight[catHeight_randStart];
                } else if (mTargetHeightMode == 4) {                // 4: Target(matrix) order
                    currentHeight = new List<int>(1) { catHeight_targetOrder };
                } else {
                    currentHeight = new List<int>(allTargets);
                }
                
                // list the possible targets without the targets that are excluded after applying the deterministic modes
                List<int> currentTarget = new List<int>(allTargets);
                generateTrialSequence_remOptions(ref currentTarget, currentY, currentHeight);

                // check if no options are available after the deterministic modes
                if (currentTarget.Count == 0) {
                    // not targets available any more

                    // the current target modes settings 
                    logger.Error("Error generating random sequence, the generation rules/parameters (TargetYMode, TargetHeightMode and Target) cause a situation where no target is available after applying deterministic modes 3 and 4");
                    return;

                }
                
                //
                // second, for this selection of targets (after applying the deterministic modes); if it does not yet exist, create a sequential (balanced) list
                // of possibilities for the number of trials (trials) that are in this selection of targets.
                //

                // generate a storage key for the subselection to hold all (balanced) possibilities
                string key = string.Join("", currentY) + "_" + string.Join("", currentHeight);

                // try to retrieve the set of options for this subselection
                List<List<int>> subSelectionSet = null;
                if (!subSelectionOptions.TryGetValue(key, out subSelectionSet) || subSelectionSet.Count == 0) {
                    // set not found or empty

                    // if not exists, create the set and add it to the dictionary
                    if (subSelectionSet == null) {
                        subSelectionSet = new List<List<int>>();
                        subSelectionOptions.Add(key, subSelectionSet);
                    }

                    // calculate the number of targets in this subselection
                    float subNumTrials = numTrials;
                    if (mTargetYMode == 3)          subNumTrials = subNumTrials / gbl_catY.Count;
                    if (mTargetHeightMode == 3)     subNumTrials = subNumTrials / gbl_catHeight.Count;
                    if (mTargetYMode == 4)          subNumTrials = subNumTrials / mTargets[0].Count;
                    if (mTargetHeightMode == 4)     subNumTrials = subNumTrials / mTargets[0].Count;
                    subNumTrials = (int)Math.Ceiling(subNumTrials);

                    // create a list of unique combo options per balanced category (or balanced category combination)
                    Dictionary<string, List<int>> uniqueCombos = new Dictionary<string, List<int>>();
                    for (int j = 0; j < currentTarget.Count; j++) {
                        string comboKey = "";
                        if (mTargetYMode == 2)      comboKey += mTargets[0][currentTarget[j]] + "_";
                        else                        comboKey += "*_";

                        if (mTargetHeightMode == 2) comboKey += mTargets[1][currentTarget[j]] + "_";
                        else                        comboKey += "*";
                        
                        // try to retrieve the set
                        List<int> uniqueComboSet = null;
                        if (!uniqueCombos.TryGetValue(comboKey, out uniqueComboSet)) {
                            // set not found

                            // create the set and add it to the dictionary
                            uniqueComboSet = new List<int>();
                            uniqueCombos.Add(comboKey, uniqueComboSet);

                        }

                        // add the option to the unique combo set
                        uniqueComboSet.Add(currentTarget[j]);

                    }
                    List<List<int>> arrUniqueCombos = new List<List<int>>(uniqueCombos.Values);

                    // create a sequence of xx long with options from uniquecomboset in the subSelectionSet variable
                    int comboSetCounter = 0;
                    for (int j = 0; j < subNumTrials; j++) {
                        
                        subSelectionSet.Add(arrUniqueCombos[comboSetCounter]);

                        comboSetCounter++;
                        if (comboSetCounter == arrUniqueCombos.Count) comboSetCounter = 0;

                    }

                    // check if no options are available after the deterministic modes for this subset
                    if (subSelectionSet.Count == 0) {
                        // not targets available any more

                        logger.Error("Error generating random sequence, the generation rules/parameters (TargetYMode, TargetHeightMode and Target) cause a situation where no target is available after applying deterministic modes 3 and 4 in subset '" + key + "'");
                        return;

                    }

                    // randomize the subSelection set
                    subSelectionSet.Shuffle();
                    
                }

                //
                // Third, apply the unbalanced random modes within the sub-selection set
                //

                // retrieve the first options
                List<int> options = subSelectionSet[0];

                // check if random within the options
                if (mTargetYMode == 1 || mTargetHeightMode == 1) {
                    // random within

                    // set it in the sequence
                    trialSequence[i] = options[rand.Next(0, options.Count)];
                    
                } else {
                    // pick the first option

                    trialSequence[i] = options[0];

                }
                
                // remove the first set of options
                subSelectionSet.RemoveAt(0);

                // continue to generate the next target
                i++;

                //
                // progress counters depending on the mode
                //

                if (mTargetYMode == 3) {                                                              // 3:sequential categories with rnd start
                    catY_randStart++;
                    if (catY_randStart == gbl_catY.Count) catY_randStart = 0;
                } else if (mTargetYMode == 4) {                                                       // 4: Target(matrix) order
                    catY_targetOrder++;
                    if (catY_targetOrder == mTargets[0].Count) catY_targetOrder = 0;
                }

                if (mTargetHeightMode == 3) {                                                              // 3:sequential categories with rnd start
                    catHeight_randStart++;
                    if (catHeight_randStart == gbl_catHeight.Count) catHeight_randStart = 0;
                } else if (mTargetHeightMode == 4) {                                                       // 4: Target(matrix) order
                    catHeight_targetOrder++;
                    if (catHeight_targetOrder == mTargets[0].Count) catHeight_targetOrder = 0;
                }

            }   // end while loop

        }

        private void generateTrialSequence_indexTargets(List<int> currentTargets, out List<int> catY_unique, out List<List<int>> catY, out List<int> catHeight_unique, out List<List<int>> catHeight) {

            // put the row indices of each distinct value (from the rows in the matrix) in an array
            // (this is used for the modes which are set to randomization)
            catY_unique = new List<int>(0);
            catY = new List<List<int>>(0);
            catHeight_unique = new List<int>(0);
            catHeight = new List<List<int>>(0);

            // put the row indices of each distinct value (from the rows in the matrix) in an array
            // (this is used for the modes which are set to randomization)
            
            // loop through the target rows
            int i = 0;
            int j = 0;
            for (i = 0; i < currentTargets.Count; ++i) {

                // get the values for the row
                int valueY = (int)mTargets[0][currentTargets[i]];
                int valueHeight = (int)mTargets[1][currentTargets[i]];

                // store the unique values and indices
                for (j = 0; j < catY_unique.Count; ++j)
                    if (catY_unique[j] == valueY) break;
                if (j == catY_unique.Count) {
                    catY_unique.Add(valueY);                        // store the unique value at index j
                    catY.Add(new List<int>(0));                     // store the targets row index in the vector at index j	

                }
                catY[j].Add(i);

                for (j = 0; j < catHeight_unique.Count; ++j)
                    if (catHeight_unique[j] == valueHeight) break;
                if (j == catHeight_unique.Count) {
                    catHeight_unique.Add(valueHeight);              // store the unique value at index j
                    catHeight.Add(new List<int>(0));                // store the targets row index in the vector at index j							
                }
                catHeight[j].Add(i);

            }
        }

        private void generateTrialSequence_remOptions(ref List<int> currentTarget, List<int> currentY, List<int> currentHeight) {

            int j = 0;
            while (j < currentTarget.Count) {
                bool found = false;

                found = false;
                for (int k = 0; k < currentY.Count; ++k) {
                    if (currentTarget[j] == currentY[k]) {
                        found = true; break;
                    }
                }
                if (!found && j < currentTarget.Count && currentTarget.Count != 0) {
                    currentTarget.Swap(j, currentTarget.Count - 1);
                    currentTarget.RemoveAt(currentTarget.Count - 1);
                    continue;
                }

                found = false;
                for (int k = 0; k < currentHeight.Count; ++k) {
                    if (currentTarget[j] == currentHeight[k]) {
                        found = true; break;
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

        }


        ////////////////////////////////////////////////
        //  UNP entry points (start, process, stop)
        ////////////////////////////////////////////////

        public void UNP_start(Parameters parentParameters) {

            // UNP entry point can only be used if initialized as UNPMenu
            if (!unpMenuTask) {
                logger.Error("Using UNP entry point while the task was not initialized as UNPMenu task, check parameters used to call the task constructor");
                return;
            }

            // create a new parameter object and define this task's parameters
            Parameters newParameters = new Parameters("CursorTask", Parameters.ParamSetTypes.Application);
            //defineParameters(ref newParameters);

            // transfer some parameters from the parent
            newParameters.setValue("WindowRedrawFreqMax", parentParameters.getValue<int>("WindowRedrawFreqMax"));
            newParameters.setValue("WindowWidth", parentParameters.getValue<int>("WindowWidth"));
            newParameters.setValue("WindowHeight", parentParameters.getValue<int>("WindowHeight"));
            newParameters.setValue("WindowLeft", parentParameters.getValue<int>("WindowLeft"));
            newParameters.setValue("WindowTop", parentParameters.getValue<int>("WindowTop"));

            // set UNP task standard settings
            mShowScore = false;
            mShowScoreAtEnd = true;
            mTaskInputSignalType = 1;
            mTaskInputChannel = 1;
            mTaskFirstRunStartDelay = 5;
            mTaskStartDelay = 10;
	        mUpdateCursorOnSignal = true;
            mPreTrialCues = false;
            mPreTrialCueTexts = new string[0];
            mPreTrialDuration = (int)(MainThread.getPipelineSamplesPerSecond() * 1.0);             // 
            mTrialDuration = (int)(MainThread.getPipelineSamplesPerSecond() * 2.0); ;              // depends on 'mUpdateCursorOnSignal', now set to packages
            mPostTrialDuration = (int)(MainThread.getPipelineSamplesPerSecond() * 1.0); ;          // 
            mITIDuration = (int)(MainThread.getPipelineSamplesPerSecond() * 2.0); ;                // 

            mCursorSize = 4f;



            numTrials = 10;
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
            unpMenuTaskRunning = true;

        }

        public void UNP_stop() {

            // UNP entry point can only be used if initialized as UNPMenu
            if (!unpMenuTask) {
                logger.Error("Using UNP entry point while the task was not initialized as UNPMenu task, check parameters used to call the task constructor");
                return;
            }

            // stop the task from running
            stop();

            // destroy the task
            destroy();

            // flag the task as no longer running (setting this to false is also used to notify the UNPMenu that the task is finished)
            unpMenuTaskRunning = false;

        }


        public bool UNP_isRunning() {
            return unpMenuTaskRunning;
        }


        public void UNP_process(double[] input, bool connectionLost) {
            
            // check if the task is running
            if (unpMenuTaskRunning) {

                // transfer connection lost
                this.connectionLost = connectionLost;

                // process the input
                if (!unpMenuTaskSuspended)     process(input);

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
            unpMenuTaskSuspended = false;
            
        }

        public void UNP_suspend() {

            // flag task as suspended
            unpMenuTaskSuspended = true;

            // pauze the task
            pauzeTask();

            // lock for thread safety and destroy the scene
            lock (lockView) {
                destroyView();
            }

        }

    }

}
