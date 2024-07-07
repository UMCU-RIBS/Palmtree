/**
 * The CMoleTask class
 * 
 * ...
 * 
 * 
 * Copyright (C) 2024:  RIBS group (Nick Ramsey Lab), University Medical Center Utrecht (The Netherlands) & external contributors
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
using System.Collections.Generic;
using System.Threading;
using Palmtree.Applications;
using Palmtree.Core;
using Palmtree.Core.Helpers;
using Palmtree.Filters;
using Palmtree.Core.Params;
using Palmtree.Core.DataIO;
using System.Collections.Specialized;

namespace CMoleTask {

    /// <summary>
    /// The <c>CMoleTask</c> class.
    /// 
    /// ...
    /// </summary>
    
    public class CMoleTask : IApplication, IApplicationChild {

		private enum TaskStates:int {
			Wait,
			CountDown,
			ColumnSelect,
			ColumnSelected,
            EscapeTrial,
			EndText
		};

        public enum scoreTypes : int {
            TruePositive,
            FalsePositive,
            FalseNegative,
            TrueNegative,
            TruePositiveEscape,
            FalseNegativeEscape
        };

        private const int CLASS_VERSION = 6;
        private const string CLASS_NAME = "CMoleTask";
        private const string CONNECTION_LOST_SOUND = "sounds\\connectionLost.wav";

        private static Logger logger = LogManager.GetLogger(CLASS_NAME);            // the logger object for the view
        private static Parameters parameters = null;
        
        private SamplePackageFormat inputFormat = null;
        private CMoleView view = null;

        private Random rand = new Random(Guid.NewGuid().GetHashCode());
        private Object lockView = new Object();                                     // threadsafety lock for all event on the view
        private bool taskPaused = false;								            // flag to hold whether the task is pauzed (view will remain active, e.g. connection lost)

        private bool childApplication = false;								        // flag whether the task is running as a child application (true) or standalone (false)
        private bool childApplicationRunning = false;						        // flag to hold whether the application should be or is running (setting this to false is also used to notify the parent application that the task is finished)
        private bool umpMenuTaskSuspended = false;						            // flag to hold whether the task is suspended (view will be destroyed/re-initiated)

        private bool connectionLost = false;							            // flag to hold whether the connection is lost
        private bool connectionWasLost = false;						                // flag to hold whether the connection has been lost (should be reset after being re-connected)

        // task input parameters
        private int windowLeft = 0;
        private int windowTop = 0;
        private int windowWidth = 800;
        private int windowHeight = 600;
        private int windowRedrawFreqMax = 0;
        private RGBColorFloat windowBackgroundColor = new RGBColorFloat(0f, 0f, 0f);

        private int taskInputChannel = 1;										   // input channel
        private int taskFirstRunStartDelay = 0;                                    // the first run start delay in sample blocks
        private int taskStartDelay = 0;                                            // the run start delay in sample blocks
        private int countdownTime = 0;                                             // the time the countdown takes in sample blocks

        private bool keySequenceState = false;
        private bool keySequencePreviousState = false;
        private int waitCounter = 0;
        private int columnSelectDelay = 0;
        private int columnSelectedDelay = 0;
        private int[] fixedTrialSequence = new int[0];                              // target sequence (input parameter)
        private bool showScore = false;
        private int taskMode = 0;                                                   // the mode used: 0: Continuous WAM, 1: Continuous WAM with computer help, 2: Dynamic mode
        private int dynamicParameter = 0;                                           // Parameter to be optimised in Dynamic Mode. 0: None, 1: Threshold, 2: Active Rate, 3: Active Period, 4: Mean, 5: ColumnSelectDelay


        // task (active) variables
        private List<MoleCell> holes = new List<MoleCell>(0);
        private TaskStates taskState = TaskStates.Wait;
        private TaskStates previousTaskState = TaskStates.Wait;

        private int holeRows = 1;
        private int holeColumns = 8;
        private int minMoleDistance = 0;
        private int maxMoleDistance = 0;
        private int currentRowID = 0;
        private int currentColumnID = 0;
        private int numberOfMoles = 1;
        private int numberOfEscapes = 0;
        private int escapeInterval = 0;                                             // minimal amount of moles between consecutive escape trials
        private int escapeDuration = 0;                                             
        private List<int> trialSequencePositions = new List<int>(0);		        // the cue sequence being used in the task (can either be given by input or generated)
        private int currentMoleIndex = -1;							                // specify the position of the mole (grid index)
        private int currentTrial = 0;						                        // specify the position in the sequence of trials
        private int countdownCounter = 0;					                        // the countdown timer
        private int score = 0;						                                // the score of the user hitting a mole
        private int scoreEscape = 0;						                        // the score of the user creating escapes
        private int scoringType = 0;
        private bool seperateEscapes = false;
        private List<scoreTypes> posAndNegs = new List<scoreTypes>(0);                  // list holding the different scores aggregated

        // computer help mode
        private List<bool> helpClickVector = null;
        private int posHelpPercentage = 0;                                          // percentage of samples that will be corrected: if a false negative no-click is made during such a sample, it will be corrected into a true positive
        private int negHelpPercentage = 0;                                          // percentage of samples that will be corrected: if a false positive click is made during such a sample, it will be corected into a true negative 

        // dynamic mode
        private bool firstUpdate = true;
        private string filter = "";
        private string param = "";
        private Parameters dynamicParameterSet = null;
        private Parameters originalParameterSet = null;
        private string paramType = null;
        private scoreTypes increaseType = scoreTypes.FalsePositive;
        private scoreTypes decreaseType = scoreTypes.FalseNegative;
        private dynamic localParam = null;
        private dynamic addInfo = null;
        private int stopOrUpdateAfterCorrect = 0;
        private int currentCorrect = 0;
        private double stepSize = 0;                                                // stepsize with which the dynamic parameter is being adjusted per step

        public CMoleTask() : this(false) { }
        public CMoleTask(bool childApplication) {

            // transfer the child application flag
            this.childApplication = childApplication;
            
            // check if the task is standalone (not a child application)
            if (!childApplication) {

                // create a parameter set for the task
                parameters = ParameterManager.GetParameters(CLASS_NAME, Parameters.ParamSetTypes.Application);

                // define the parameters
                defineParameters(ref parameters);
            }

            // message
            logger.Info("Application " + CLASS_NAME + " created (version " + CLASS_VERSION + ")");
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


            //
            // Timings and durations
            //

            parameters.addHeader("Timings and durations");
            
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

            parameters.addParameter<double>(
                "ColumnSelectDelay",
                "Amount of time before continuing to next column",
                "0", "", "3s");

            parameters.addParameter<double>(
                "ColumnSelectedDelay",
                "Amount of time after selecting a column to wait",
                "0", "", "1s");

            parameters.addParameter<double>(
                "EscapeDuration",
                "Amount of time an escape trial is presented",
                "0", "", "3s");


            //
            // Task settings
            //

            parameters.addHeader("Task");            

            parameters.addParameter<int>(
                "TaskInputChannel",
                "Input channel to use as click",
                "1", "", "1");

            parameters.addParameter<int>(
                "NumberOfMoles",
                "Amount of moles presented",
                "1", "", "10");

            parameters.addParameter<int>(
                "TaskMode",
                "Select the mode in which the task operates",
                "0", "2", "0", new string[] { "Continuous WAM (CWAM)", "CWAM with computer help", "Dynamic" });
            
            parameters.addParameter<int>(
               "PositiveHelpPercentage",
               "Only in CWAM with computer help: percentage of samples during cell selection that will be corrected if a false negative no-click is made during that sample",
               "0", "", "5");

            parameters.addParameter<int>(
               "NegativeHelpPercentage",
               "Only in CWAM with computer help: percentage of samples during cell selection that will be corrected if a false positive click is made during that sample",
               "0", "", "10");

            parameters.addParameter<int>(
                "DynamicParameter",
                "The parameter to dynamically optimze.\n\nNote: only used when TaskMode is set to 'Dynamic'",
                "0", "5", "0", new string[] { "None", "Threshold Classifier - Threshold", "Click Translator - Active Rate Click Threshold", "Click Translator - Active Period" , "Adaptation - Initial Channel Means", "CMole Task - Column Select Delay"});

            parameters.addParameter<double>(
                "Stepsize",
                "Only in Dynamic Mode: absolute stepsize with which dynamic parameter is adjusted per step, given in unit of specific parameter, with the exception of dynamic parameter 4: here the stepsize is relative, defined as a fraction of the initial standard deviation.",
                "0", "", "5");

            parameters.addParameter<int>(
               "StopOrUpdateAfterCorrect",
               "Only in Dynamic Mode: for dynamic parameters 1-4 this parameter determines after how many correct responses in a row the task will end. Set to 0 to not end task based on amount of correct responses. \n For parameter 5, this parameter determines after how many consecutive true positives or false negatives the parameter is adjusted. Setting to 0 is not allowed in this case.",
               "0", "", "1");



            //
            // Conditions
            //

            parameters.addHeader("Conditions and trials sequence");

            parameters.addParameter<int[]>(
                "TrialSequence",
                "Fixed sequence in which moles and escapes should be presented (leave empty for random).\nNote. the parameters ('NumberOfMoles', 'MinimalMoleDistance', 'MinimalMoleDistance'\n'NumberOfMoles') that are normally used to generate the trials sequence will be ignored",
                "0", "", "");
            parameters.addParameter<int>(
                "MinimalMoleDistance",
                "Minimal distance, expressed in cells, from currently selected cell to appearing mole",
                "1", "", "3");

            parameters.addParameter<int>(
               "MaximalMoleDistance",
               "Maximal distance, expressed in cells, from currently selected cell to appearing mole",
               "1", "", "8");

            parameters.addParameter<int>(
                "NumberOfEscapes",
                "Amount of escape trials presented",
                "1", "", "2");

            parameters.addParameter<int>(
                "EscapeInterval",
                "Minimum amount of moles between consecutive escape trials",
                "1", "", "2");

            //
            // Display
            //

            parameters.addHeader("Display");

            parameters.addParameter<bool>(
                "ShowScore",
                "Enable/disable showing of scoring",
                "1");
            
            parameters.addParameter<int>(
                "ScoringType",
                "Type of scoring used",
                "0", "1", "0", new string[] { "Score = TP / (TP + FP + FN)", "Score = (TP + TN) / (TP + TN + FP + FN)" });
            
            parameters.addParameter<bool>(
                "ShowEscapeScoreSeperate",
                "If enabled, shows scores for presented escapes seperately/",
                "1");
            
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

        public bool configure(ref SamplePackageFormat input) {

            // check sample-major ordered input
            if (input.valueOrder != SamplePackageFormat.ValueOrder.SampleMajor) {
                logger.Error("This application is designed to work only with sample-major ordered input");
                return false;
            }

            // check if the number of input channels is higher than 0
            if (input.numChannels <= 0) {
                logger.Error("Number of input channels cannot be 0");
                return false;
            }

            // store a reference to the input format
            inputFormat = input;
            
            // configure the parameters
            return configure(parameters);

        }


        public bool configure(Parameters newParameters) {
            
            // 
            // TODO: parameters.checkminimum, checkmaximum
            //

            // retrieve window settings
            windowLeft = newParameters.getValue<int>("WindowLeft");
            windowTop = newParameters.getValue<int>("WindowTop");
            windowWidth = newParameters.getValue<int>("WindowWidth");
            windowHeight = newParameters.getValue<int>("WindowHeight");
            windowRedrawFreqMax = newParameters.getValue<int>("WindowRedrawFreqMax");
            windowBackgroundColor = newParameters.getValue<RGBColorFloat>("WindowBackgroundColor");

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

            // retrieve the input channel setting
            taskInputChannel = newParameters.getValue<int>("TaskInputChannel");
            if (taskInputChannel < 1) {
                logger.Error("Invalid input channel, should be higher than 0 (1...n)");
                return false;
            }
            if (taskInputChannel > inputFormat.numChannels) {
                logger.Error("Input should come from channel " + taskInputChannel + ", however only " + inputFormat.numChannels + " channels are coming in");
                return false;
            }

            // retrieve the task mode
            taskMode = newParameters.getValue<int>("TaskMode");
            if (taskMode < 0 || taskMode > 2) {
                logger.Error("Unknown taskMode parameter value: " + taskMode);
                return false;
            }

            // retrieve the task delays 
            taskFirstRunStartDelay = newParameters.getValueInSamples("TaskFirstRunStartDelay");
            taskStartDelay = newParameters.getValueInSamples("TaskStartDelay");
            if (taskFirstRunStartDelay < 0 || taskStartDelay < 0) {
                logger.Error("Start delays cannot be less than 0");
                return false;
            }

            // retrieve the countdown time
            countdownTime = newParameters.getValueInSamples("CountdownTime");
            if (countdownTime < 0) {
                logger.Error("Countdown time cannot be less than 0");
                return false;
            } 

            // retrieve selection delays
            columnSelectDelay = newParameters.getValueInSamples("ColumnSelectDelay");
            columnSelectedDelay = newParameters.getValueInSamples("ColumnSelectedDelay");
            if (columnSelectDelay < 1 || columnSelectedDelay < 1) {
                logger.Error("The 'ColumnSelectDelay' or 'ColumnSelectedDelay' parameters should not be less than 1");
                return false;
            } 

            // retrieve the number of moles
            numberOfMoles = newParameters.getValue<int>("NumberOfMoles");
            if (numberOfMoles < 1) {
                logger.Error("Minimum of 1 mole is required");
                return false;
            }

            // retrieve minimal distance between current cell and appearing moles
            minMoleDistance = newParameters.getValue<int>("MinimalMoleDistance");
            if (minMoleDistance < 1 || minMoleDistance > holeColumns) {
                logger.Error("Minimal distance of at least 1 cell is required and can not be higher than the amount of cells available.");
                return false;
            }

            // retrieve maximal distance between current cell and appearing moles
            maxMoleDistance = newParameters.getValue<int>("MaximalMoleDistance");
            if (maxMoleDistance <= minMoleDistance) {
                logger.Error("Maximal distance needs to be larger than minimal distance");
                return false;
            }

            // retrieve the number of escape trials and interval between trials
            numberOfEscapes = newParameters.getValue<int>("NumberOfEscapes");
            escapeInterval = newParameters.getValue<int>("EscapeInterval");
            if ( ((numberOfEscapes-1) * escapeInterval) > numberOfMoles) {
                logger.Error("To present " + numberOfEscapes + " escape trials with a minimum of " + escapeInterval + " moles between the escapes, at least " + ((numberOfEscapes - 1) * escapeInterval) + " moles are needed. Adjust 'Number of moles' parameter accordingly");
                return false;
            }

            // retrieve how long an escape trial is presented 
            escapeDuration = newParameters.getValueInSamples("EscapeDuration");
            if (escapeDuration <= 0 ) {
                logger.Error("The escape trial duration must be at least one sample.");
                return false;
            }

            // retrieve whether to show score, how to calculate score, and whether to show the escape score seperate
            showScore = newParameters.getValue<bool>("ShowScore");
            scoringType = newParameters.getValue<int>("ScoringType");
            if (!(scoringType == 0 || scoringType == 1)) {
                logger.Error("Unknown scoringType parameter value: " + scoringType);
                return false;
            }
            seperateEscapes = newParameters.getValue<bool>("ShowEscapeScoreSeperate");

            // retrieve (fixed) trial sequence
            fixedTrialSequence = newParameters.getValue<int[]>("TrialSequence");
            if (fixedTrialSequence.Length > 0) {
                int numHoles = holeRows * holeColumns;
                for (int i = 0; i < fixedTrialSequence.Length; ++i) {
                    if (fixedTrialSequence[i] < 0) {
                        logger.Error("The TrialSequence parameter contains a target index (" + fixedTrialSequence[i] + ") that is below zero, check the TrialSequence");
                        return false;
                    }
                    if (fixedTrialSequence[i] >= numHoles) {
                        logger.Error("The TrialSequence parameter contains a target index (" + fixedTrialSequence[i] + ") that is out of range, check the HoleRows and HoleColumns parameters. (note that the indexing is 0 based)");
                        return false;
                    }
                    // TODO: check if the mole is not on an empty spot
                }
            }

            // configure buffers for computer help mode
            if (taskMode == 1) {

                // create help click vector to hold help clicks
                helpClickVector = new List<bool>(new bool[columnSelectDelay]);

                // retrieve help percentages
                posHelpPercentage = newParameters.getValue<int>("PositiveHelpPercentage");
                negHelpPercentage = newParameters.getValue<int>("NegativeHelpPercentage");
                if (posHelpPercentage < 0 || posHelpPercentage > 100 || negHelpPercentage < 0 || negHelpPercentage > 100) {
                    logger.Error("Positive and negative help percentages can not be below 0% or above 100%");
                    return false;
                }

            }

            // retrieve parameters for dynamic mode 
            dynamicParameter = newParameters.getValue<int>("DynamicParameter");
            stepSize = newParameters.getValue<double>("Stepsize");
            stopOrUpdateAfterCorrect = newParameters.getValue<int>("StopOrUpdateAfterCorrect");

            // perform checks on parameters for dynamic mode, if mode is set to dynamic mode
            if (taskMode == 2) {

                // amount of correct responses should be positive, but less than total amount of moles presented         
                if (stopOrUpdateAfterCorrect < 0 || stopOrUpdateAfterCorrect > numberOfMoles) {
                    logger.Error("The required amount of correct responses to end the task needs to be larger than 0, and less than the total amount of moles presented.");
                    return false;
                } else if (stopOrUpdateAfterCorrect == 0 && taskMode == 2 && dynamicParameter == 5) {
                    logger.Error("When adjusting parameter 5 in dynamic mode, the parameter stopOrUpdateAfterCorrect can not be 0, as this would result in never updating the parameter.");
                    return false;
                }
                
                if (dynamicParameter == 0) {
                    logger.Error("No 'DynamicParameter' parameter cannot be None, select one of the options");
                    return false;
                }

                // check range parameter: we only have 5 dynamic paramters
                if ((dynamicParameter < 1 || dynamicParameter > 5)) {
                    logger.Error("Only dynamic parameter values between 1 and 5 are allowed.");
                    return false;
                }

                // if dynamic paramter is the mean, check in adaptation filter if this filter is also trying to optimize the mean
                if (dynamicParameter == 4) {

                    // retrieve adaptation settings from Adaptationfilter
                    Parameters adapParams = MainThread.getFilterParametersClone("Adaptation");
                    int[] adaptations = adapParams.getValue<int[]>("Adaptation");

                    // set temp bool
                    bool proceed = true;

                    // cycle through adaptation settings for the different channels to check if there is a channel that is set to adaptation
                    for (int i = 0; i < adaptations.Length; i++)
                        if (adaptations[i] != 1) proceed = false;

                    // if there exists channels set to adaptation, give feedback and return
                    if (!proceed) {
                        logger.Error("The adaptationfilter is either set to no adaptation at all, or set to calibration (ie the adaptation parameter of this filter is 0, or larger than 1). It is not possible to optimize the mean if the adaptationfilter is set to no adaptation, or to attempt to optimize the mean both in the adaptationfilter and with this task.");
                        return false;
                    }
                }
            }

            // return success
            return true;

        }

        public bool initialize() {
                                
            // lock for thread safety
            lock(lockView) {

                // calculate the cell holes for the task
                int numHoles = holeRows * holeColumns;

                // create the array of cells for the task
                holes = new List<MoleCell>(0);
                for (int i = 0; i < numHoles; i++) {
                        holes.Add(new MoleCell(0, 0, 0, 0, MoleCell.CellType.Hole));
                }

                // check the view (thread) already exists, stop and clear the old one.
                destroyView();

                // initialize the view
                initializeView();

                // check if a target sequence is set
	            if (fixedTrialSequence.Length == 0) {
		            // trialSequence not set in parameters, generate
		            
		            // Generate targetlist
		            generateTrialSequence();

	            } else {
		            // trialsequence is set in parameters

                    // clear the trials
		            if (trialSequencePositions.Count != 0)		trialSequencePositions.Clear();
                
		            // transfer the fixed trial sequence
                    trialSequencePositions = new List<int>(fixedTrialSequence);

	            }	            
            }
            
            // return success
            return true;

        }

        private void initializeView() {

            // create the view
            view = new CMoleView(windowRedrawFreqMax, windowLeft, windowTop, windowWidth, windowHeight, false);
            view.setBackgroundColor(windowBackgroundColor.getRed(), windowBackgroundColor.getGreen(), windowBackgroundColor.getBlue());

            // set task specific display attributes 
            view.setFixation(false);                                            // hide the fixation
            view.setCountDown(-1);                                              // hide the countdown
            view.viewScore(showScore);                                          // show/hide score according to parameter setting
            view.setSeperateEscScore(seperateEscapes);                          // whether escape score is presented seperately

            // initialize the holes for the scene
            view.initGridPositions(holes, holeRows, holeColumns, 10);

            // initialize the score grid
            view.initScoreGrid(numberOfMoles, numberOfEscapes, holes);

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

            // check if the task is standalone (not a child application)
            if (!childApplication) {
                
                // store the generated sequence in the output parameter xml
                Data.adjustXML(CLASS_NAME, "TrialSequence", string.Join(" ", trialSequencePositions));
                
            }

            // lock for thread safety
            lock(lockView) {

                if (view == null)   return;

                // log event task is started
                Data.logEvent(2, "TaskStart", CLASS_NAME);

                // reset the score, and positive and negative list
                score = 0;
                posAndNegs.Clear();

                // set countdown to the countdown time
                countdownCounter = countdownTime == 0 ? countdownTime : countdownTime - 1;

                if (taskStartDelay != 0 || taskFirstRunStartDelay != 0) {

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

            // process
            for (int sample = 0; sample < input.Length; sample += inputFormat.numChannels)
                process(sample + input[taskInputChannel - 1]);

        }

        private void process(double input) {
            
            // lock for thread safety
            lock (lockView) {
                
                if (view == null)   return;

                ////////////////////////
                // BEGIN CONNECTION FILTER ACTIONS//
                ////////////////////////

                // check if connection is lost, or was lost
                if (connectionLost) {

                    // check if it was just discovered if the connection was lost
                    if (!connectionWasLost) {

                        // set the connection as was lost (this also will make sure the lines in this block willl only run once)
                        connectionWasLost = true;

                        // pause the task
                        pauseTask();

			            // show the lost connection warning
			            view.setConnectionLost(true);

                        // play the connection lost sound continuously every 2 seconds
                        SoundHelper.playContinuousAtInterval(CONNECTION_LOST_SOUND, 2000);

                    }

                    // do not process any further
                    return;
            
                } else if (connectionWasLost && !connectionLost) {

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
	            if (taskPaused)		    return;
                
                // check if the escape-state has changed
                keySequenceState = Globals.getValue<bool>("KeySequenceActive");
                if (keySequenceState != keySequencePreviousState) {

                    // log and update
                    Data.logEvent(2, "EscapeChange", (keySequenceState) ? "1" : "0");
                    keySequencePreviousState = keySequenceState;

                    // starts the full refractory period on all translated channels
                    MainThread.configureRunningFilter("ClickTranslator", null, (int)ClickTranslatorFilter.ResetOptions.StartFullRefractoryPeriod);

                }

                // check if there is a click
                bool click = (input == 1 && !keySequenceState);

                // use the task state
                switch (taskState) {

                    // starting, pauzed or waiting
                    case TaskStates.Wait:

                        // set the state to countdown
                        if (waitCounter == 0) {
                            setState(TaskStates.CountDown);
			            } else
				            waitCounter--;

                        break;

                    // Countdown before start of task
                    case TaskStates.CountDown:
                        
                        // check if the task is counting down
                        if (countdownCounter > 0) {

                            // display the countdown
                            view.setCountDown((int)Math.Floor((countdownCounter - 1) / MainThread.getPipelineSamplesPerSecond()) + 1);

                            // reduce the countdown timer
                            countdownCounter--;

                        // done counting down
                        } else {
				            
				            // hide the countdown counter
				            view.setCountDown(-1);

				            // begin first trial, and set the mole and selectionbox at the right position
				            currentTrial = 0;
                            currentColumnID = 0;

                            // Show hole grid and score
                            view.setGrid(true);
                            view.viewScore(true);

                            // log event countdown is started
                            Data.logEvent(2, "TrialStart ", CLASS_NAME);

                            // set mole and corresponding state
                            setCueAndState(trialSequencePositions[currentTrial]);
			            }

			            break;

                    // highlighting columns
                    case TaskStates.ColumnSelect:

                        // get whether the current column contains a mole
                        bool containsMole = currentMoleIndex == holeColumns * currentRowID + currentColumnID;

                        int curIndex = holeColumns * currentRowID + currentColumnID;
                        //logger.Info("At cell " + curIndex + ", at sample " + waitCounter + " with value: " + input);

                        // if in computer help mode and we are not moving on to next column, combine click with computer help (no-)click
                        if (taskMode == 1) {

                            // get computer help click or no click
                            bool helpclick = helpClickVector[columnSelectDelay - (waitCounter + 1)];
                            bool newClick = false;

                            // combine help click with actual click made, depending on the current column 
                            if (containsMole)   newClick = helpclick || click;
                            else                newClick = helpclick && click;

                            // if we changed click, set or unset refractoryperiod accordingly and give feedback
                            if (newClick != click) {
                                if (newClick)   MainThread.configureRunningFilter("ClickTranslator", null, (int)ClickTranslatorFilter.ResetOptions.StartFullRefractoryPeriod);
                                else            MainThread.configureRunningFilter("ClickTranslator", null, (int)ClickTranslatorFilter.ResetOptions.StopRefractoryPeriod);

                                logger.Info("At sample " + waitCounter + " in this column the click is: " + click + " and is adjusted to: " + newClick);
                                Data.logEvent(2, "clickAdjusted", click + ";" + newClick);
                            }

                            // set click to adjusted click
                            click = newClick;
                            
                        }

                        // if clicked, log click and go to next state
                        if (click) {
                            Data.logEvent(2, "CellClick", currentColumnID.ToString());
                            setState(TaskStates.ColumnSelected);
			            } else {
                            
                            // if time to highlight column has passed
                            if (waitCounter == 0) {

                                // if we missed a mole, store a false negative, and go to next trial
                                if (containsMole) {

                                    // store false negative
                                    posAndNegs.Add(scoreTypes.FalseNegative);

                                    // sotre as event
                                    Data.logEvent(2, "FalseNegative", currentColumnID.ToString());

                                    // increase trial index
                                    currentTrial++;

                                    // advance to next cell, if the end of row has been reached, reset column id
                                    currentColumnID++;
                                    if (currentColumnID >= holeColumns) currentColumnID = 0;

                                    // if at end of trial sequence, go to Endtext state, otherwise set mole and correpsonding state
                                    if (currentTrial == trialSequencePositions.Count)   setState(TaskStates.EndText);
                                    else                                                setCueAndState(trialSequencePositions[currentTrial]);

                                    // if in dynamic mode, adjust dynamic parameter and check if we need to stop task because enough correct responses have been given
                                    if (taskMode == 2) updateParameter();

                                // if no mole was missed, store a true negative, go to next cell and reset time 
                                } else {

                                    // store true negative and event
                                    posAndNegs.Add(scoreTypes.TrueNegative);
                                    Data.logEvent(2, "TrueNegative", currentColumnID.ToString());


                                    // advance to next cell, if the end of row has been reached, reset column id
                                    currentColumnID++;
                                    if (currentColumnID >= holeColumns) currentColumnID = 0;

                                    // re-set state to same state, to trigger functions that occur at beginning of processing of this state
                                    setState(TaskStates.ColumnSelect);
                                }

				            } else 
					            waitCounter--;
			            }

			            break;

                    // column was selected
                    case TaskStates.ColumnSelected:
			            
			            if(waitCounter == 0) {

                            // if mole is selected, store true positive
                            if (currentMoleIndex == holeColumns * currentRowID + currentColumnID) {

                                // store true positive and log event
                                posAndNegs.Add(scoreTypes.TruePositive);
                                Data.logEvent(2, "TruePositive", currentColumnID.ToString());

                                // go to next trial in the sequence and set mole and selectionbox
                                currentTrial++;
                                currentColumnID++;
                                if (currentColumnID >= holeColumns) currentColumnID = 0;

                                // check whether at the end of trial sequence
                                if (currentTrial == trialSequencePositions.Count) {

                                    // update score and show end text
                                    updateScore();
                                    setState(TaskStates.EndText);

                                } else {

                                    // set mole and corresponding state
                                    setCueAndState(trialSequencePositions[currentTrial]);

                                }

                                // if in dynamic mode, adjust dynamic parameter and check if we need to stop task because enough correct responses have been given
                                if (taskMode == 2) updateParameter();

                            // no hit, store false positive
                            } else {

                                // store false positive and event
                                posAndNegs.Add(scoreTypes.FalsePositive);
                                Data.logEvent(2, "FalsePositive", currentColumnID.ToString());

                                // increase column ID and select enxt cell
                                currentColumnID++;
                                if (currentColumnID >= holeColumns) currentColumnID = 0;
                                setState(TaskStates.ColumnSelect);

                                // if in dynamic mode, adjust dynamic parameter and check if we need to stop task because enough correct responses have been given
                                if (taskMode == 2) updateParameter();
                            }

			            } else
				            waitCounter--;

			            break;

                    // escape trial is presented
                    case TaskStates.EscapeTrial:

                        // if escape trial has been presented for the complete duration, log false negative and go to next mole or escape
                        if (waitCounter == 0 || keySequenceState) {

                            // store or true positive or false negative depending on whether an escape sequence was made, and store events
                            if (keySequenceState) {
                                posAndNegs.Add(scoreTypes.TruePositiveEscape);
                                Data.logEvent(2, "TruePositiveEscape", "");
                            } else {
                                posAndNegs.Add(scoreTypes.FalseNegativeEscape);
                                Data.logEvent(2, "FalseNegativeEscape", "");
                            }

                            // remove escape cue
                            view.setEscape(false);

                            // go to next trial in the sequence and check whether at the end of trial sequence
                            currentTrial++;
                            if (currentTrial == trialSequencePositions.Count)   setState(TaskStates.EndText);       
                            else                                                setCueAndState(trialSequencePositions[currentTrial]);

                            // if in dynamic mode, adjust dynamic parameter and check if we need to stop task because enough correct responses have been given
                            if (taskMode == 2) updateParameter();

                        } else
                            waitCounter--;
                        
                        break;

                    // end text
                    case TaskStates.EndText:
			            
			            if (waitCounter == 0) {

                            // hide hole grid
                            view.setGrid(false);
                            view.viewScore(false);

                            // show text for three seconds
                            view.setText("Done");

                            // log event task is stopped
                            Data.logEvent(2, "TaskStop", CLASS_NAME + ";end");

                            // stop the task, this will also call stop(), and as a result stopTask()
                            if (childApplication)        AppChild_stop();
                            else                    MainThread.stop(false);

                        } else
				            waitCounter--;

			            break;

	            }

                // update the score 
                updateScore();

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

            }

            // destroy/empty more task variables


        }


        // pauses the task
        private void pauseTask() {
	        if (view == null)   return;

            // log event task is paused
            Data.logEvent(2, "TaskPause", CLASS_NAME);

            // set task as pauzed
            taskPaused = true;

	        // store the previous state
	        previousTaskState = taskState;
			
            // hide everything
            view.setFixation(false);
            view.setCountDown(-1);
            view.setGrid(false);
        }

        // resumes the task
        private void resumeTask() {
            if (view == null)   return;

            // log event task is resumed
            Data.logEvent(2, "TaskResume", CLASS_NAME);

            // show the grid and set the mole
            if (previousTaskState == TaskStates.ColumnSelect || previousTaskState == TaskStates.ColumnSelected) {
			
			    // show the grid and reset the current mole and state
			    view.setGrid(true);
			    setCueAndState(trialSequencePositions[currentTrial]);
		    }
	    
	        // set the previous state
	        setState(previousTaskState);

	        // set task as not longer pauzed
	        taskPaused = false;
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

        // update dynamic parameter
        public void updateParameter() {

            // if not in dynamic mode or if there no scores to base parameter update on, exit
            if (taskMode != 2 && posAndNegs.Count > 0)      return;

            // retrieve last score to base parameter update on
            scoreTypes lastScore = posAndNegs[posAndNegs.Count - 1];

            // check if we need to end task because enough correct responses have been given
            if (dynamicParameter != 5 && stopOrUpdateAfterCorrect != 0) {

                if (lastScore == scoreTypes.TruePositive)                                                   currentCorrect++;
                else if (lastScore == scoreTypes.FalsePositive || lastScore == scoreTypes.FalseNegative)    currentCorrect = 0;

                if (currentCorrect >= stopOrUpdateAfterCorrect) {
                    setState(TaskStates.EndText);
                    logger.Info("Ending task because set amount of correct responses in a row have been reached.");
                    return;
                }
            }

            // for first update, retrieve original parameter value and store local copy (not done for paramter 5, since this already is a local variable)
            // NB. we store local copy becasue we *can* update the local variables in a running filter, but we *cannot* query these, which is needed for subsequent parameter updates. Ath the same time, we *cannot* update the Parameters in a running filter, but we *can* query these.
            if (firstUpdate) {

                // set information on which parameter in which filter will be increased or decreased for which score type
                switch (dynamicParameter) {

                    // dynamic parameter: threshold
                    case 1:
                        filter = "ThresholdClassifier";
                        param = "Thresholding";
                        paramType = "double[][]";
                        increaseType = scoreTypes.FalsePositive;
                        decreaseType = scoreTypes.FalseNegative;
                        addInfo = 1;                                    // column in the parameter matrix holding threshold paramter

                        break;

                    // dynamic parameter: active rate
                    case 2:
                        filter = "ClickTranslator";
                        param = "ActiveRateClickThreshold";
                        paramType = "double";
                        increaseType = scoreTypes.FalsePositive;
                        decreaseType = scoreTypes.FalseNegative;

                        break;

                    // dynamic parameter: active period
                    case 3:
                        filter = "ClickTranslator";
                        param = "ActivePeriod";
                        paramType = "samples";
                        increaseType = scoreTypes.FalsePositive;
                        decreaseType = scoreTypes.FalseNegative;

                        break;

                    // dynamic parameter: mean
                    case 4:
                        filter = "Adaptation";
                        param = "InitialChannelMeans";
                        paramType = "double[]";
                        increaseType = scoreTypes.FalsePositive;
                        decreaseType = scoreTypes.FalseNegative;

                        // for dynamic parameter mean the stepsize is not an absolute value, but is given as fraction of the standard deviation. 
                        // Therefore store a stepsize modifier based on the standard deviations per channel in addInfo variable, multiplied by their respective signs from the weights in the linear classifier filter, which ensures the adjustment occurs in the desired direction 

                        // get stds and weights
                        Parameters adaptationParams = MainThread.getFilterParametersClone("Adaptation");
                        double[] initStds = adaptationParams.getValue<double[]>("InitialChannelStds");
                        Parameters linearClassParams = MainThread.getFilterParametersClone("LinearClassifier");
                        double[][] redistributionValues = linearClassParams.getValue<double[][]>("Redistribution");
                        addInfo = redistributionValues[2];

                        // check if same amount channels are defined in both filters
                        if (addInfo.Length != initStds.Length) logger.Error("Different amount of initial channel standard deviations (in Adaptation filter) and redistribution channels (in Linear Classifier filter) set.");

                        // calculate stepsize modifier
                        for (int i = 0; i < addInfo.Length; i++)
                            addInfo[i] = Math.Sign(addInfo[i]) * initStds[i];

                        break;

                    // dynamic parameter: columnSelectDelay
                    case 5:
                        filter = "";
                        param = "ColumnSelectDelay";
                        paramType = "int";
                        increaseType = scoreTypes.FalseNegative;
                        decreaseType = scoreTypes.TruePositive;

                        break;

                    default:
                        logger.Error("Non-existing dynamic parameter ID encountered. Check code.");
                                             
                        break;
                }

                // if there is a parameter to update, retrieve current value for this parameter from filter (except for columnSelectDelay, which is stored locally)
                if (param != "" && dynamicParameter != 5) {
                    
                    // retrieve parameter set from given filter
                    dynamicParameterSet = MainThread.getFilterParametersClone(filter);
                    originalParameterSet = MainThread.getFilterParametersClone(filter);

                    // retrieve value for given parameter and store local copy    
                    if (paramType == "double")          localParam = dynamicParameterSet.getValue<double>(param);
                    else if (paramType == "double[]")   localParam = dynamicParameterSet.getValue<double[]>(param);
                    else if (paramType == "double[][]") localParam = dynamicParameterSet.getValue<double[][]>(param);
                    else if (paramType == "samples")    localParam = dynamicParameterSet.getValueInSamples(param);               

                }

                // prevent from retrieving value from parameter set again
                firstUpdate = false;
            }

            // if local parameter needs updating and is not empty, update local copy of parameter and push to filter
            if (localParam != null && (lastScore == decreaseType || lastScore == increaseType) ) {

                // make backup of local parameter value, in case the newly calculated value is rejected by filter we can use this backup to retore the value if the local parameter
                dynamic localParamBackup = localParam;

                // based on parameter type, adjust parameter by increasing or decreasing based on whether the last score was a (true or false) positive or negative
                if (paramType == "double") {
                    if          (lastScore == decreaseType)     localParam = localParam - stepSize;
                    else if     (lastScore == increaseType)     localParam = localParam + stepSize;

                } else if (paramType == "double[]") {

                    // if addInfo is not set earlier (because the specific paramter being updated here does need this additional info), then init addInfo to all ones, having no effect
                    if (addInfo == null) {
                        addInfo = new int[localParam.Length];
                        for (int i = 0; i < addInfo.Length; i++) { addInfo[i] = 1; }
                    }

                    // NOTE: stepsize direction is adjusted based on sign in addInfo, useful for example in adjusting means of different channels
                    for (int channel = 0; channel < localParam.Length; ++channel) {
                        if      (lastScore == decreaseType) localParam[channel] = localParam[channel] - (stepSize * addInfo[channel]);
                        else if (lastScore == increaseType) localParam[channel] = localParam[channel] + (stepSize * addInfo[channel]);
                    }

                } else if (paramType == "double[][]") {

                    // if addInfo is not set earlier (because the specific parameter being updated here does need this additional info), then set addInfo to 0, defaulting to first column in matrix to adjust
                    if (addInfo == null)    addInfo = 1;

                    // NOTE: column in matrix is adjusted based on addInfo, defaulting to 0
                    for (int channel = 0; channel < localParam[0].Length; ++channel) {                  
                        if      (lastScore == decreaseType) localParam[addInfo][channel] = localParam[addInfo][channel] - stepSize;
                        else if (lastScore == increaseType) localParam[addInfo][channel] = localParam[addInfo][channel] + stepSize;
                    }

                } else if (paramType == "samples") {

                    if          (lastScore == decreaseType)     localParam = Math.Round(localParam - stepSize);
                    else if     (lastScore == increaseType)     localParam = Math.Round(localParam + stepSize);

                }

                // store adjusted threshold in dynamic parameter set and re-configure running filter using this adjusted parameter set
                dynamicParameterSet.setValue(param, localParam);
                bool suc = MainThread.configureRunningFilter(filter, dynamicParameterSet);

                // if update of filter did not succeed, adjust local copy by restoring backup, to ensure local copy and filter paramter value stay in sync
                if (!suc) {
                    localParam = localParamBackup;
                    logger.Info("Value of dynamic parameter exceeds allowed thresholds after updating, current value is retained.");
                    Data.logEvent(2, "VariableUpdate ", dynamicParameter.ToString() + "Unsuccessful, exceeded bounds.");
                } else {
                    // give feedback on new value of local copy
                    logger.Info("Value of dynamic parameter updated, new value: " + Extensions.arrayToString(localParam));
                    Data.logEvent(2, "VariableUpdate ", dynamicParameter.ToString() + ";" + Extensions.arrayToString(localParam));
                }
            }

            // update dynamic parameter 5 if needed, is done seperately, because it is a local variable
            if (dynamicParameter == 5) {

                // create backup in case updating the paramter results in values beyond allowed limits
                int columnSelectDelayBackup = columnSelectDelay;

                // temp var to base console output on
                bool updated = false;

                // false negative
                if (lastScore == scoreTypes.FalseNegative) {
                    columnSelectDelay = (int)Math.Round(columnSelectDelay + stepSize);
                    updated = true;
                } else if (lastScore == scoreTypes.TruePositive) {
                    currentCorrect++;
                    if (currentCorrect >= stopOrUpdateAfterCorrect) {
                        columnSelectDelay = (int)Math.Round(columnSelectDelay - stepSize);
                        currentCorrect = 0;
                        updated = true;
                    }
                }

                // scanning rate can not go below 1, same limit as used during configure() step
                if (columnSelectDelay < 1) {
                    columnSelectDelay = columnSelectDelayBackup;
                    logger.Info("Value of dynamic parameter exceeds allowed thresholds after updating, current value of " + columnSelectDelay + " is retained.");
                    Data.logEvent(2, "VariableUpdate ", "ColumnSelectDelay Unsuccessful, exceeded bounds.");

                } else if (updated) {
                    logger.Info("Value of dynamic parameter updated, new value: " + columnSelectDelay);
                    Data.logEvent(2, "VariableUpdate ", "ColumnSelectDelay; " + columnSelectDelay.ToString());

                }
            }
        }

        // update score based on (true and false) positives and negatives and push to view
        private void updateScore() {

            // init
            double tp = 0;
            double fp = 0;
            double fn = 0;
            double tn = 0;
            double tpEsc = 0;
            double fnEsc = 0;

            // cycle through list and count (true and false) positives and negatives
            for(int i = 0; i < posAndNegs.Count; i++) {
                if      (posAndNegs[i] == scoreTypes.TruePositive) tp++;
                else if (posAndNegs[i] == scoreTypes.FalsePositive) fp++;
                else if (posAndNegs[i] == scoreTypes.TrueNegative) tn++;
                else if (posAndNegs[i] == scoreTypes.FalseNegative) fn++;
                else if (posAndNegs[i] == scoreTypes.TruePositiveEscape) tpEsc++;
                else if (posAndNegs[i] == scoreTypes.FalseNegativeEscape) fnEsc++;
            }

            // if we are not using seperate escape scoring, we also count tpEsc and fnEsc as tp's and fn's, respectively
            if (!seperateEscapes) {
                tp = tp + tpEsc;
                fn = fn + fnEsc;
            }

            // calculate score, based on required scoreType
            if (scoringType == 0) {
                if (tp + fp + fn > 0) score = (int)Math.Floor((tp / (tp + fp + fn)) * 100.0);
            } else if (scoringType == 1) {
                if (tp + tn + fp + fn > 0) score = (int)Math.Floor(((tp + tn) / (tp + tn + fp + fn)) * 100.0);
            }

            // calculate escapeScore
            if (tpEsc + fnEsc > 0)      scoreEscape = (int)Math.Floor((tpEsc / (tpEsc + fnEsc)) * 100.0);

            // push to view
            if (view != null)    view.setScore(posAndNegs, score, scoreEscape);

        } 

        private void setState(TaskStates state) {

	        // Set state
	        taskState = state;

	        switch (state) {

                // starting, pauzed or waiting
                case TaskStates.Wait:
			         
			        // hide text if present
			        view.setText("");

			        // hide the fixation and countdown
			        view.setFixation(false);
                    view.setCountDown(-1);

			        // hide countdown, selection, mole and score
                    view.selectRow(-1, false);
                    view.setGrid(false);
                    view.viewScore(false);

                    // Set wait counter to startdelay NB: WAITCOUNTER IS INITILAISED WITHOUT MINUS 1, BECAUSE WAIT STATE IS CALLED FROM START(), WHICH IS IMMEDIATELY FOLLOWED BY PROCESS CYCLE WHICH REDUCES WAITCOUNTER BY 1
                    if (taskFirstRunStartDelay != 0) {
                        waitCounter = taskFirstRunStartDelay;
                        taskFirstRunStartDelay = 0;
                    } else
                        waitCounter = taskStartDelay;

                    break;

                // countdown when task starts
                case TaskStates.CountDown:

                    // log event countdown is started
                    Data.logEvent(2, "CountdownStarted ", CLASS_NAME);

                    // hide fixation
                    view.setFixation(false);

                    // set countdown
                    if (countdownCounter > 0)
                        view.setCountDown((int)Math.Floor((countdownCounter - 1) / MainThread.getPipelineSamplesPerSecond()) + 1);
                    else
                        view.setCountDown(-1);

                    break;

                // escape trial is being shown
                case TaskStates.EscapeTrial:

                    view.selectRow(-1, false);
                    view.selectCell(-1, -1, false);

                    // Data.logEvent(2, "Escape presented", "");
                    waitCounter = escapeDuration == 0 ? escapeDuration : escapeDuration - 1;

                    break;

                // selecting a column
                case TaskStates.ColumnSelect:

                    // TODO
                    // move currentColumnID++; if (currentColumnID >= holeColumns) currentColumnID = 0; to here and remove everywhere else?

                    // get whether current cell contains mole
                    bool containsMole = currentMoleIndex == holeColumns * currentRowID + currentColumnID;

                    // during computer help, create help click vector
                    if (taskMode == 1) {

                        // create empty click vector, length equal to the amount of samples a column is selected, all default to no-click (false)
                        helpClickVector = new List<bool>(new bool[columnSelectDelay]);

                        // create negative help vector
                        if (!containsMole) {

                            // temp vars to hold amount of clicks and no-clicks
                            int amountHelpClicks = 0;
                            int amountHelpNoClicks = 0;

                            // determine amount of help clicks and help no-clicks
                            amountHelpNoClicks = (int)Math.Floor(columnSelectDelay * (negHelpPercentage / 100.0));
                            amountHelpClicks = columnSelectDelay - amountHelpNoClicks;

                            //logger.Error("helpClicks: " + amountHelpClicks + "helpNoClicks" + amountHelpNoClicks);

                            // adjust the help click vector by inserting the amount of clicks needed 
                            for (int i = Math.Max(0, amountHelpNoClicks); i < columnSelectDelay; i++) helpClickVector[i] = true;

                            // shuffle the vector to create new semi-random, semi-evenly divided vector
                            shuffleHelpVector(false);

                        } else {

                            // create linearly increasing help vector
                            Random rng = new Random();
                            for (int i = 0; i < columnSelectDelay; i++) {

                                // create increasing threshold, to level of helppercentage
                                double thresh = ((double)i / (double)(columnSelectDelay-1)) * (double)posHelpPercentage;

                                // create random number between 1 and 100 
                                int p = rng.Next(1, 100);

                                //logger.Info("Threhsold: " + thresh + " and p: " + p);

                                // if number is smaller than threshold, set help to true
                                if (p < thresh) helpClickVector[i] = true;
                               
                            }

                        }
                        
                        // debug
                        //String helpClStr = "";
                        //for(int i = 0; i < helpClickVector.Count; i++) {
                        //    helpClStr = helpClStr + " " + helpClickVector[i].ToString();
                        //}

                        //logger.Info(helpClStr);
                    }

                    // select cell
                    view.selectCell(currentRowID, currentColumnID, false);
                    
                    // set waitcounter
                    waitCounter = columnSelectDelay == 0 ? columnSelectDelay : columnSelectDelay - 1;

                    break;

                // column was selected
                case TaskStates.ColumnSelected:
			        
			        // select cell and highlight
			        view.selectCell(currentRowID, currentColumnID, true);

                    // log cell click event
                    //if (currentMoleIndex == holeColumns * currentRowID + currentColumnID)   Data.logEvent(2, "CellClick", "1");
                    //else                                                                    Data.logEvent(2, "CellClick", "0");

                    // set wait time before advancing
                    waitCounter = columnSelectedDelay == 0 ? columnSelectedDelay : columnSelectedDelay - 1;

                    break;

                // show end text
                case TaskStates.EndText:

                    // wait 2s before screen goes blank
                    int endTime = (int)(MainThread.getPipelineSamplesPerSecond() * 3.0);
                    waitCounter = endTime == 0 ? endTime : endTime - 1;

                    break;
	        }

        }

        // Stop the task
        private void stopTask() {
            if (view == null)   return;

            // print (escape) score to console
            logger.Info("Score: " + score + " Escape score: " + scoreEscape);

            // If taskmode is in dynamic mode, reset dynamic paramters to original value
            if (taskMode == 2) {

                // give feedback on final value of dynamic parameter
                if (localParam != null) logger.Info("Final value " + param + ": " + Extensions.arrayToString(localParam) );

                // reset filter with original parameter values (not needed for param 5 because this does not involve updating filters)
                if (dynamicParameter != 5)  MainThread.configureRunningFilter(filter, originalParameterSet);

                // reset vars related to dynamic taskmode
                firstUpdate = true;
                localParam = null;
                currentCorrect = 0;

            }

            // reset the score, and positive and negative list
            score = 0;
            scoreEscape = 0;
            posAndNegs.Clear();

            // Set state to Wait
            setState(TaskStates.Wait);

            // if there is no fixed target sequence, generate new targetlist
            if (fixedTrialSequence.Length == 0) {

		        // generate new targetlist
		        generateTrialSequence();

	        }

        }

        private void generateTrialSequence() {

            // create trial sequence array with <numTrials> and temporary lists for moles and escapes
            List<int> molesSequence = new List<int>(new int[numberOfMoles]);
            List<int> escapeSequence = new List<int>(new int[numberOfEscapes]);
            trialSequencePositions = new List<int>(new int[numberOfMoles+numberOfEscapes]);

            // create random moles sequence using minimal and maximal mole distance settings. Sequence contains cell number, ie at which cell the mole will appear
            for (int i = 0; i < numberOfMoles; i++) {
                if (i==0)   molesSequence[i] = rand.Next(1, holeColumns - 1);                                                       // first mole can be placed on any cell, except the first
                else        molesSequence[i] = (molesSequence[i-1] + rand.Next(minMoleDistance, maxMoleDistance)) % holeColumns;    // consecutive moles need to be placed according to minimal and maximal distance to previous mole
            }

            // create random escape cue sequence by placing escapes between moles using minimal interval setting. Sequence contains indices of trialSequence at which escapes will be presented
            int molesLeft = numberOfMoles;                                                  // amount of moles 'left' that can be placed between previous and new escape
            int escapesToPlace = numberOfEscapes;                                           // amount of escapes that still need to be placed between moles 
            int molesNeeded = (escapesToPlace - 1) * escapeInterval;                        // minimum amount of moles needed to place the remaining escapes
            int interval = 0;                                                               // temp variable, holds the distance between consecutive escapes

            // cycle through amount of requested escapes and place each between the moles, ie determine the index (the order) of the escape in the cue sequence
            for (int i = 0; i < numberOfEscapes; i++) {

                if (i == 0) {escapeSequence[i] = rand.Next(0, molesLeft - molesNeeded); }   // first mole can be placed from index 0 to whatever surplus of moles there is
                else {                                                                      // consecutive moles are placed at least the required interval apart, and at most the surplus of moles apart; and not exceeding the length of the cue sequence (ie numberOfMoles + numberOfEscapes)
                    interval = rand.Next(escapeInterval, molesLeft - molesNeeded);
                    escapeSequence[i] = Math.Min(escapeSequence[i-1] + interval + 1, (numberOfMoles + numberOfEscapes)-1);
                }

                // update variables
                escapesToPlace--;
                molesLeft = molesLeft - interval;
                molesNeeded = (escapesToPlace - 1) * escapeInterval;
            }

            // combine moles and escape sequences into cue sequence. First insert -1 at the indices at which escapes are presented,
            for (int i = 0; i < escapeSequence.Count; i++) { trialSequencePositions[escapeSequence[i]] = -1; }

            // then insert cell numbers of moles at indices of cue sequence at which no escape is presented
            int m = 0;                                      // counter for molesequence
            for (int i = 0; i < trialSequencePositions.Count; i++) {
                if (trialSequencePositions[i] == 0) {
                    trialSequencePositions[i] = molesSequence[m];
                    m++;
                }

                //logger.Info(trialSequencePositions[i]);

            }
        }

        private void shuffleHelpVector(bool increasing) {

            //
            Random rng = new Random();
            int n = helpClickVector.Count;
            int N = n;

            // loop through elements of list
            while (n > 1) {
                n--;

                // added increasing option: if increasing is true, then chances of skipping the swap decrease linearly, starting with 1, meaning the last element is never swapped. Because the elements are ordered, this means the chances of helping increase linearly
                int s = rng.Next(n, N);
                if (!increasing || s < N - 1) {
                    int k = rng.Next(n + 1);
                    bool value = helpClickVector[k];
                    helpClickVector[k] = helpClickVector[n];
                    helpClickVector[n] = value;
                }
            }
        }

        private void setCueAndState(int index) {

            //logger.Info(index);

	        // set mole index to variable
	        currentMoleIndex = index;

	        // hide moles
	        for(int i = 0; i < holes.Count; i++) {
		        if (holes[i].type == MoleCell.CellType.Mole)
			        holes[i].type = MoleCell.CellType.Hole;
	        }

            // if index is -1, place escape, otherwise place mole at given index
            if (currentMoleIndex == -1) {
                view.setEscape(true);
                setState(TaskStates.EscapeTrial);
            } else {
                holes[currentMoleIndex].type = MoleCell.CellType.Mole;
                setState(TaskStates.ColumnSelect);
            }
        }


        ////////////////////////////////////////////////
        //  Child application entry points (start, process, stop)
        ////////////////////////////////////////////////

        public void AppChild_start(Parameters parentParameters) {
            
            // entry point can only be used if initialized as child application
            if (!childApplication) {
                logger.Error("Using child entry point while the task was not initialized as child application task, check parameters used to call the task constructor");
                return;
            }

            // create a new parameter object and define this task's parameters
            Parameters newParameters = new Parameters(CLASS_NAME + "_child", Parameters.ParamSetTypes.Application);
            defineParameters(ref newParameters);

            // transfer some parameters from the parent
            newParameters.setValue("WindowRedrawFreqMax", parentParameters.getValue<int>("WindowRedrawFreqMax"));
            newParameters.setValue("WindowWidth", parentParameters.getValue<int>("WindowWidth"));
            newParameters.setValue("WindowHeight", parentParameters.getValue<int>("WindowHeight"));
            newParameters.setValue("WindowLeft", parentParameters.getValue<int>("WindowLeft"));
            newParameters.setValue("WindowTop", parentParameters.getValue<int>("WindowTop"));

            // set child task standard settings
            inputFormat.numChannels = 1;
            //allowExit = true;                  // child task, allow exit
            newParameters.setValue("WindowBackgroundColor", "0;0;0");
            newParameters.setValue("TaskMode", 2);
            newParameters.setValue("DynamicParameter", 4);
            newParameters.setValue("Stepsize", 0.1);
            newParameters.setValue("StopOrUpdateAfterCorrect", 0);
            newParameters.setValue("TaskFirstRunStartDelay", "2s");
            newParameters.setValue("TaskStartDelay", "2s");
            newParameters.setValue("CountdownTime", "3s");
            newParameters.setValue("TaskInputChannel", 1);
            newParameters.setValue("ColumnSelectDelay", "3s");
            newParameters.setValue("ColumnSelectedDelay", "3s");
            newParameters.setValue("NumberOfMoles", 8);
            newParameters.setValue("MinimalMoleDistance", 3);
            newParameters.setValue("MaximalMoleDistance", 5);
            newParameters.setValue("NumberOfEscapes", 0);
            newParameters.setValue("ShowScore", true);
            newParameters.setValue("ShowEscapeScoreSeperate", false);       
            newParameters.setValue("ScoringType", 1);
            newParameters.setValue("PositiveHelpPercentage", 5);
            newParameters.setValue("NegativeHelpPercentage", 10);
            newParameters.setValue("EscapeInterval", 2);
            newParameters.setValue("EscapeDuration", "3s");
            newParameters.setValue("TrialSequence", "");


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
            childApplicationRunning = true;

        }

        public void AppChild_stop() {
            
            // entry point can only be used if initialized as child application
            if (!childApplication) {
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
		        if (!umpMenuTaskSuspended)		process(input);

	        }

        }

        public void AppChild_resume() {

            // lock for thread safety
            lock (lockView) {

                // initialize the view
                initializeView();

            }
	
	        // resume the task
	        resumeTask();

	        // flag task as no longer suspended
	        umpMenuTaskSuspended = false;

        }

        public void AppChild_suspend() {

            // flag task as suspended
            umpMenuTaskSuspended = true;

            // pauze the task
            pauseTask();

            // lock for thread safety and destroy the scene
            lock (lockView) {
                destroyView();
            }

        }

    }

}
