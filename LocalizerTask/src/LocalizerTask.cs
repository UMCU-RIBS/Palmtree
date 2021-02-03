/**
 * The LocalizerTask class
 * 
 * ...
 * 
 * 
 * Copyright (C) 2017:  RIBS group (Nick Ramsey Lab), University Medical Center Utrecht (The Netherlands) & external contributors
 * Concept:             UNP Team                    (neuroprothese@umcutrecht.nl)
 * Author(s):           Benny van der Vijgh         (benny@vdvijgh.nl)
 * 
 * This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software
 * Foundation, either version 3 of the License, or (at your option) any later version. This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for
 * more details. You should have received a copy of the GNU General Public License along with this program. If not, see <https://www.gnu.org/licenses/>.
 */
using NLog;
using System;

using Palmtree.Core;
using Palmtree.Applications;
using Palmtree.Core.Helpers;
using Palmtree.Core.Params;
using Palmtree.Core.DataIO;


namespace LocalizerTask {

    /// <summary>
    /// The <c>LocalizerTask</c> class.
    /// 
    /// ...
    /// </summary>
    public class LocalizerTask : IApplication {

        // fundamentals
        private const int CLASS_VERSION = 2;                                // class version
        private const string CLASS_NAME = "LocalizerTask";                  // class name
        private const string CONNECTION_LOST_SOUND = "sounds\\connectionLost.wav";

        private static Logger logger = LogManager.GetLogger(CLASS_NAME);    // the logger object for the view
        private static Parameters parameters = ParameterManager.GetParameters(CLASS_NAME, Parameters.ParamSetTypes.Application);        // parameters
 
        // status 
        private bool childApplication = false;                               // flag whether the task is running as a child application (true) or standalone (false)
        private bool childApplicationRunning = false;                            // flag to hold whether the task is running as UNP task
        private bool childApplicationSuspended = false;                          // flag to hold whether the task (when running as UNP task) is suspended          

        private bool connectionLost = false;							    // flag to hold whether the connection is lost
        private bool connectionWasLost = false;						        // flag to hold whether the connection has been lost (should be reset after being re-connected)

        private TaskStates taskState = TaskStates.None;                     // holds current task state
        private TaskStates previousTaskState = TaskStates.None;             // holds previous task state
        private int waitCounter = 0;                                        // counter for task state Start and Wait, used to determine time left in this state

        // view
        private LocalizerView view = null;                                  // view for task
        private Object lockView = new Object();                             // threadsafety lock for all event on the view
        private int windowLeft = 0;                                         // position of window from left of screen
        private int windowTop = 0;                                          // position of windo from top of screen
        private int windowWidth = 800;                                      // window width
        private int windowHeight = 600;                                     // window height
        private int windowRedrawFreqMax = 0;                                // redraw frequency
        private RGBColorFloat windowBackgroundColor = new RGBColorFloat(0f, 0f, 0f);    // background color of view

        // task specific
        private string[][] stimuli = null;                                  // matrix with stimulus information per stimulus: text, sound, image, length
        private int[] stimuliSequence = null;                               // sequence of stimuli, referring to stimuli by index in stimuli matrix 
        private int currentStimulus = 0;                                    // stimulus currently being presented, referred to by index in stimuli matrix
        private int stimulusCounter = 0;                                    // stimulus pointer, indicating which stimulus of sequence is currently being presented
        private int stimulusRemainingTime = -1;                             // remaining time to present current stimulus, in samples
        private int numberOfRepetitions = 0;                                // amount of times a stimuli sequence will be presented
        private int currentRepetition = 1;                                  // the current stimuli sequence repetition
        private int firstSequenceWait = 0;                                  // time to wait at start of task before first sequence is presented, in samples
        private int betweenSequenceWait = 0;                                // time to wait between end of sequence and start of next sequence, in samples
        private string startText = "";                                      // text presented to participant at beginning of task
        private string waitText = "";                                       // text presented to participant during waiting
        private string endText = "";                                        // text presented to participant at end of task
        private enum TaskStates : int {                                     // taskstates
            None,
            Start,
            Run,
            Wait,
            Pause,
            End
        };

        // parameterless constructor calls second constructor
        public LocalizerTask() : this(false) { }
        public LocalizerTask (bool childApplication) {

            // transfer the child application flag
            this.childApplication = childApplication;

            // check if the task is standalone (not a child application)
            if (!this.childApplication) {

                // create a parameter set for the task
                parameters = ParameterManager.GetParameters(CLASS_NAME, Parameters.ParamSetTypes.Application);

                // add parameters
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

                parameters.addParameter<string[][]>(
                    "Stimuli",
                    "Stimuli available for use in a stimuli sequence and their corresponding duration in seconds. Stimuli can be either text, images, or sounds, or any combination of these modalities.\n Each row represents a stimulus.",
                    "", "", "Rust;;;10", new string[] { "Text", "Image", "Sound", "Duration [s]" });

                parameters.addParameter<int[]>(
                    "StimuliSequence",
                    "",
                    "Sequence of one or more stimuli.", "", "", "1");

                parameters.addParameter<int>(
                    "NumberOfRepetitions",
                    "Number of times a stimuli sequence will be repeated.",
                    "1", "", "2");

                parameters.addParameter<int>(
                    "FirstSequenceWait",
                    "Amount of time before the first sequence of the task starts.",
                    "0", "", "5s");

                parameters.addParameter<int>(
                    "BetweenSequenceWait",
                    "Amount of time between end of sequence and start of subsequent sequence.",
                    "0", "", "10s");

                parameters.addParameter<string>(
                    "StartText",
                    "Text shown to participant at beginning of task.",
                    "", "", "Task will begin shortly.");

                parameters.addParameter<string>(
                    "WaitText",
                    "Text shown to participant during waiting periods.",
                    "", "", "Wait.");

                parameters.addParameter<string>(
                    "EndText",
                    "Text shown to participant at end of task.",
                    "", "", "Task is finished.");

            }

            // message
            logger.Info("Application " + CLASS_NAME + " created (version " + CLASS_VERSION + ")");
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

            // PARAMETER TRANSFER 

            // transfer window settings
            windowLeft = parameters.getValue<int>("WindowLeft");
            windowTop = parameters.getValue<int>("WindowTop");
            windowWidth = parameters.getValue<int>("WindowWidth");
            windowHeight = parameters.getValue<int>("WindowHeight");
            windowRedrawFreqMax = parameters.getValue<int>("WindowRedrawFreqMax");
            windowBackgroundColor = parameters.getValue<RGBColorFloat>("WindowBackgroundColor");

            // transfer task specific values
            stimuli = parameters.getValue<string[][]>("Stimuli");
            stimuliSequence = parameters.getValue<int[]>("StimuliSequence");
            numberOfRepetitions = parameters.getValue<int>("NumberOfRepetitions");
            firstSequenceWait = parameters.getValueInSamples("FirstSequenceWait");
            betweenSequenceWait = parameters.getValueInSamples("BetweenSequenceWait");
            startText = parameters.getValue<string>("StartText");
            waitText = parameters.getValue<string>("WaitText");
            endText = parameters.getValue<string>("EndText");


            // PARAMETER CHECK
            // TODO: parameters.checkminimum, checkmaximum

            // check if stimuli are defined
            if (stimuli.Length != 4) {
                logger.Error("Stimuli not defined (correctly).");
                return false;
            } else {
                // if stimuli are defined, check if duration is defined for each stimulus
                for (int i = 0; i < stimuli[3].Length; i++) {
                    if (string.IsNullOrEmpty(stimuli[3][i]) || stimuli[3][i] == " ") {
                        logger.Error("Timing of stimulus " + (i+1).ToString() + " is not defined.");
                        return false;
                    } else {
                        // if timing is defined, see if it is parsable to integer
                        int timing = 0;
                        if(!int.TryParse(stimuli[3][i], out timing)) {
                            logger.Error("The timing given for stimulus " + (i+1).ToString() + " (\"" + stimuli[3][i] + "\") is not a valid value, should be a positive integer.");
                            return false;
                        }
                        // if timing is parsable, check if larger than 0
                        else if (timing <= 0){
                            logger.Error("The timing given for stimulus " + (i+1).ToString() + " (" + stimuli[3][i] + ") is too short. The value should be a positive integer.");
                            return false;
                        }
                    }
                }
            }

            // check if stimulus sequence is defined
            if(stimuliSequence.Length <= 0) {
                logger.Error("No stimuli sequence given.");
                return false;
            }

            // determine maximal stimulus defined in stimulus sequence
            int stimMax = 0;
            for(int i=0; i<stimuliSequence.Length; i++) {
                if (stimuliSequence[i] > stimMax) stimMax = stimuliSequence[i];
            }

            // check if there are stimuli defined that are not included in stimuli definition
            if(stimMax > stimuli[0].Length) {
                logger.Error("In stimuli sequence, stimulus " + stimMax + " is defined. This stimulus can not be found in stimuli definition, as there are only " + stimuli[0].Length + " stimuli defined.");
                return false;
            }

            // check if amount of repetitions is higher than 0
            if (numberOfRepetitions <= 0) {
                logger.Error("Amount of repetitions should be a positive integer.");
                return false;
            }

            // check if first sequence wait is not smaller than 0
            if (firstSequenceWait < 0) {
                logger.Error("The time to wait before the start of the first sequence can not be lower than 0.");
                return false;
            }

            // check if sequence wait is not smaller than 0
            if (betweenSequenceWait < 0) {
                logger.Error("The time to wait before the start of the subsequent sequence can not be lower than 0.");
                return false;
            }

            // view checks
            if (windowRedrawFreqMax < 0) {
                logger.Error("The maximum window redraw frequency can be no smaller then 0");
                return false;
            }

            if (windowWidth < 1) {
                logger.Error("The window width can be no smaller then 1");
                return false;
            }

            if (windowHeight < 1) {
                logger.Error("The window height can be no smaller then 1");
                return false;
            }

            return true;
        }

        public void initialize() {
                    
            // lock for thread safety
            lock (lockView) {

                // check the view (thread) already exists, stop and clear the old one.
                destroyView();

                // initialize view (scene)
                initializeView();

            }

        }

        private void initializeView() {

            // create scene thread
            view = new LocalizerView(windowRedrawFreqMax, windowLeft, windowTop, windowWidth, windowHeight, false);
            view.setBackgroundColor(windowBackgroundColor.getRed(), windowBackgroundColor.getGreen(), windowBackgroundColor.getBlue());

            // start the scene thread
            view.start();
        }

        public void start() {

            // lock for thread safety
            lock (lockView) {

                // check if view is available
                if (view == null) return;

                // log event task is started
                Data.logEvent(2, "TaskStart", CLASS_NAME);

                // feedback to user
                //logger.Debug("Task started.");

                // if a wait before first sequence is required, set state to Start, else to run 
                if (firstSequenceWait != 0)     setState(TaskStates.Start);
                else                            setState(TaskStates.Run);

            }

        }

        public void stop() {

            // stop the connection lost sound from playing
            SoundHelper.stopContinuous();
            
            // set text to display
            if(!string.IsNullOrEmpty(endText))      view.setText(endText);

            // reset all relevant variables in case task is restarted
            currentStimulus = stimulusCounter = 0;
            currentRepetition = 1;
            stimulusRemainingTime = -1;

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
            process();

        }

        public void process() {

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



                // perform actions for state
                switch (taskState) {

                    case TaskStates.Start:

                        // wait until timer reaches zero, then go to Run state
                        if (waitCounter == 0)   setState(TaskStates.Run);
                        else                    waitCounter--;

                        break;

                    case TaskStates.Run:

                        // if the presentation time for this stimulus is up
                        if (stimulusRemainingTime == 0) {

                            // increase stimulus pointer
                            stimulusCounter++;

                            // if we reached end of stimulus sequence, set stimulus pointer to 0, and move to next sequence
                            if (stimulusCounter >= stimuliSequence.Length) {
                                stimulusCounter = 0;
                                currentRepetition++;

                                if (currentRepetition > numberOfRepetitions) {
                                    setState(TaskStates.End);       // if we have presented all sequences, go to task state End

                                } else if (betweenSequenceWait != 0) {
                                    waitCounter = betweenSequenceWait;     // if there are sequences to present and there is a time to wait, set waiting time and go to state Wait
                                    setState(TaskStates.Wait);

                                } else {
                                    setStimulus();                  // if there are sequences to present and we do not have to wait, set next stimulus

                                }

                            } else {        // if we are not at end of sequence, present next stimulus 
                                setStimulus();

                            }

                        // if time is not up, decrease remaining time
                        } else {
                            stimulusRemainingTime--;

                        }

                        break;

                    case TaskStates.Wait:

                        // if there is time left to wait, decrease time, otherwise return to Run state
                        if (waitCounter != 0)   waitCounter--;
                        else                    setState(TaskStates.Run);

                        break;
                        
                    case TaskStates.Pause:
                        break;

                    case TaskStates.End:

                        // log event that task is stopped
                        Data.logEvent(2, "TaskStop", CLASS_NAME + ";end");

                        // stop the run
                        // this will also call stop(), and as a result stopTask()
                        if (childApplication)        AppChild_stop();
                        else                    MainThread.stop(false);

                        break;

                    case TaskStates.None:
                        break;

                    default:
                        logger.Error("Non-existing task state reached. Task will be stopped. Check code.");
                        stop();
                        break;

                }

            }

        }

        // set state of task to given state
        private void setState(TaskStates state) {

            // check if there is a view to modify
            if (view == null) return;

            // store the previous state
            previousTaskState = taskState;

            // transfer state
            taskState = state;

            // perform initial actions for new state
            switch (taskState) {

                case TaskStates.Start:

                    // set text to display 
                    view.setText(startText);

                    // feedback to user
                    //logger.Debug("Participant is waiting for task to begin.");

                    // log event participant is waiting
                    Data.logEvent(2, "WaitPresented", CLASS_NAME);

                    // initialize wait counter, determining how long this state will last
                    waitCounter = firstSequenceWait;

                    break;

                case TaskStates.Run:

                    // show stimulus
                    setStimulus();

                    break;

                case TaskStates.Wait:

                    // set text to display 
                    view.setText(waitText);

                    // feedback to user
                    //logger.Debug("Participant is waiting for next stimulus.");

                    // log event participant is waiting
                    Data.logEvent(2, "WaitPresented", CLASS_NAME);

                    break;

                case TaskStates.Pause:

                    // set text to display 
                    view.setText("Task paused.");

                    // feedback to user
                    //logger.Debug("Task paused.");

                    // log event task is paused
                    Data.logEvent(2, "TaskPause", CLASS_NAME);

                    break;

                case TaskStates.End:

                    // set text to display 
                    view.setText(endText);

                    // feedback to user
                    //logger.Debug("Task finished.");

                    // reset all relevant variables in case task is restarted
                    currentStimulus = stimulusCounter = 0;
                    currentRepetition = 1;
                    stimulusRemainingTime = -1;

                    // wait for two seconds
                    waitCounter = SampleConversion.timeToSamples(2);

                    break;

                default:

                    // message
                    logger.Error("Non-existing task state selected. Task will be stopped. Check code.");

                    // stop the task
                    stop();

                    break;

            }
        }

        private void pauzeTask() {
            if (view == null) return;

            // log event task is paused
            Data.logEvent(2, "TaskPause", CLASS_NAME);
            

        }
        private void resumeTask() {

            // log event task is resumed
            Data.logEvent(2, "TaskResume", CLASS_NAME);

            // set the previous state
            setState(previousTaskState);
        }

        private void setStimulus() {

            // determine current stimulus based on stimulus sequence (-1 because 0-based)
            currentStimulus = stimuliSequence[stimulusCounter] - 1;

            // feedback to user
            //logger.Debug("Presenting stimulus " + (currentStimulus+1) + " (" + stimuli[0][currentStimulus] + ") in sequence " + currentSequence + " for " + stimuli[3][currentStimulus] + " seconds");

            // log event that stimulus is presented
            Data.logEvent(2, "StimulusPresented", currentStimulus.ToString());

            // TODO: set stimulus sound and image
            // set stimulus text to display 
            view.setText(stimuli[0][currentStimulus]);

            // if the stimulus remaining time is not set or is zero, set the remaining time for this stimulus 
            if (stimulusRemainingTime <= 0) {

                // try to parse stimulus presentation time
                if (int.TryParse(stimuli[3][currentStimulus], out stimulusRemainingTime)) {

                    // determine remaining stimulus time in samples
                    // TODO: this should be in ParamStringMat.getvalueInSamples()
                    double samples = stimulusRemainingTime * MainThread.getPipelineSamplesPerSecond();
                    stimulusRemainingTime = (int)Math.Round(samples);

                    // give warning if rounding occurs
                    if (samples != stimulusRemainingTime) {
                        logger.Warn("Remaining time for presenting current stimulus (stimulus " + currentStimulus + ") has been rounded from " + samples + " samples to " + stimulusRemainingTime + " samples.");
                    }

                } else {

                    // if parsing of presentation time of stimulus fails, stop task
                    logger.Error("The timing given for stimulus " + (currentStimulus + 1).ToString() + " (\"" + stimuli[3][currentStimulus] + "\") is not a valid value, should be a positive integer. Execution of task stops.");
                    stop();
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
            lock (lockView) {

                // destroy the view
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

        //
        //  Child application entry points (start, process, stop)
        //
        public void AppChild_start(Parameters parentParameters) {

            // entry point can only be used if initialized as child application
            if (!this.childApplication) {
                logger.Error("Using child entry point while the task was not initialized as child application task, check parameters used to call the task constructor.");
                return;
            }

            // set window settings
            windowRedrawFreqMax = parentParameters.getValue<int>("WindowRedrawFreqMax");      // the view update frequency (in maximum fps)
            windowWidth = parentParameters.getValue<int>("WindowWidth"); ;
            windowHeight = parentParameters.getValue<int>("WindowHeight"); ;
            windowLeft = parentParameters.getValue<int>("WindowLeft"); ;
            windowTop = parentParameters.getValue<int>("WindowTop"); ;

            // set task specific variables
            stimuli = new string[][] { new string[] {"Rust","Concentreer"}, new string[] { "","" }, new string[] { "", "" }, new string[] { "10", "5" } };
            stimuliSequence = new int[] { 1, 2, 1};
            numberOfRepetitions = 2;
            firstSequenceWait = 10 * (int)MainThread.getPipelineSamplesPerSecond();
            betweenSequenceWait = 5 * (int)MainThread.getPipelineSamplesPerSecond();
            startText = "Task will begin shortly";
            waitText = "Wait";
            endText = "End task";

            // initialize
            initialize();

            // start the task
            start();

            // set the task as running as UNP task
            childApplicationRunning = true;
        }

        public void AppChild_stop() {

            // entry point can only be used if initialized as child application
            if (!this.childApplication) {
                logger.Error("Using child entry point while the task was not initialized as child application task, check parameters used to call the task constructor");
                return;
            }

            // stop the task from running
            stop();

            // destroy the task
            destroy();

            // flag the task as no longer running (setting this to false is also used to notify the UNPMenu that the task is finished)
            childApplicationRunning = false;
        }

        public bool AppChild_isRunning() {
            return childApplicationRunning;
        }

        public void AppChild_process(double[] input, bool connectionLost) {

            // check if the task is running
            if (childApplicationRunning) {

                // transfer connection lost
                this.connectionLost = connectionLost;

                // process the input (if the task is not suspended)
                if (!childApplicationSuspended)      process();

            }

        }

        public void AppChild_resume() {

            // flag task as no longer suspended
            childApplicationSuspended = false;
            
            // resume the task
            resumeTask();
        }

        public void AppChild_suspend() {

            // flag task as suspended
            childApplicationSuspended = true;

            // pause the task
            setState(TaskStates.Pause);
        }

    }

}