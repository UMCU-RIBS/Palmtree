/**
 * SpellerTask class
 * 
 * ...
 * 
 * 
 * Copyright (C) 2024:  RIBS group (Nick Ramsey Lab), University Medical Center Utrecht (The Netherlands) & external contributors
 * Concept:             UNP Team                    (neuroprothese@umcutrecht.nl)
 * Author(s):           Benny van der Vijgh         (benny@vdvijgh.nl)
 *                      Max van den Boom            (info@maxvandenboom.nl)
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
using Palmtree.Core.Params;
using Palmtree.Core.DataIO;
using System.Collections.Specialized;
using System.Speech.Synthesis;

namespace SpellerTask {

    /// <summary>
    /// SpellerTask class
    /// 
    /// ...
    /// </summary>
    public class SpellerTask : IApplication, IApplicationChild {

        private const int CLASS_VERSION = 2;
        private const string CLASS_NAME = "SpellerTask";
        private const string CONNECTION_LOST_SOUND = "sounds\\connectionLost.wav";
        private static Logger logger = LogManager.GetLogger(CLASS_NAME);                        // the logger object for the view
        private static Parameters parameters = null;
        private SamplePackageFormat inputFormat = null;

        // status
        private bool childApplication = false;								    // flag whether the task is running as a child application (true) or standalone (false)
        private bool childApplicationRunning = false;						    // flag to hold whether the application should be or is running (setting this to false is also used to notify the parent application that the task is finished)
        private bool childApplicationSuspended = false;						    // flag to hold whether the task is suspended (view will be destroyed/re-initiated)
        private bool taskPaused = false;                                        // flag to hold whether the task is pauzed (view will remain active, e.g. connection lost)
        private bool connectionLost = false;							        // flag to hold whether the connection is lost
        private bool connectionWasLost = false;						            // flag to hold whether the connection has been lost (should be reset after being re-connected)
        private TaskStates afterWait = TaskStates.CountDown;                    // the task state to go to after a wait
        private TaskStates taskState = TaskStates.Wait;

        // view
        private SpellerView view = null;
        private Object lockView = new Object();                                 // threadsafety lock for all event on the view
        private int windowLeft = 0;
        private int windowTop = 0;
        private int windowWidth = 800;
        private int windowHeight = 600;
        private int windowRedrawFreqMax = 0;
        private RGBColorFloat windowBackgroundColor = new RGBColorFloat(0f, 0f, 0f);

        // input parameter variables
        Random rand = new Random(Guid.NewGuid().GetHashCode());
        private int taskInputChannel = 1;								        // input channel
        private int taskStartDelay = 0;                                         // the start delay in sample blocks
        private int countdownTime = 0;                                          // the time the countdown takes in sample blocks
        private int rowSelectDelay = 0;
        private int rowSelectedDelay = 0;
        private int columnSelectDelay = 0;
        private int columnSelectedDelay = 0;
        private List<SpellerCell> cells = new List<SpellerCell>(0);
        private bool allowExit = false;
        private int numSpellerRows = 0;
        private int numSpellerColumns = 0;
        private bool toTopOnIncorrectRow = false;                               // whether to return to top row when an incorrect row is selected.
        private bool showScore                  = false;                        // whether or not to show score
        private string[][] inputArray           = null;
        private string backspaceCode            = "";
        private string[] inputs                 = null;
        private string[] answers                = null;                         // targets (answers) in case of question mode
        private int cueInputDelay               = 0;                            // time between presentation of cue and inputs, in samples
        private List<string> cues               = new List<string>(0);
        private bool backspacePresent           = false;                        // whether or not a backspace button is present in the input options
        private int interStimInterval           = 0;                            // inter stimulus interval, in samples
        private int cueType                     = 0;
        private int maxRowLoop                  = 0;                            // maximal amount of times a row is looped over before it returns to the top row
        private bool synthesizeSpeech           = false;                        // whether or not to show score

        // task specific variables
        private int waitCounter                 = 0;
        private int currentRowID                = 0;
        private int currentColumnID             = 0;
        private int backSpacesNeeded            = 0;

        private string spellerText = "";                                        // the text that was spelled
        private int currentCharacterIndex        = 0;                           // the index of the character that needs to be spelled
        private string currentCharacter          = null;                        // the "character" that needs to be spelled
        
        private bool wordSpelled                = false;
        private int correctClicks               = 0;
        private int totalClicks                 = 0;
        private string waitText                 = "";
        private int countdownCounter            = 0;			                // the countdown timer
        private int score                       = 0;			                // the score of the user correctly responding to the cues
        private int rowLoopCounter              = 0;
        private int cueCounter                  = 0;
        private SpeechSynthesizer synthesizer   = null;

        private enum TaskStates : int {
            Wait,
            CountDown,
            InitialCue,
            RowSelect,
            RowSelected,
            ColumnSelect,
            ColumnSelected,
            EndText
        };

        public SpellerTask() : this(false) { }
        public SpellerTask(bool childApplication) {

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

            parameters.addParameter<int>(
                "TaskStartDelay",
                "Amount of time before the task (or the countdown) starts",
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
                "CueType",
                "Whether the cues are words that need to be spelled or questions that need to be answered.",
                "0", "1", "0", new string[] { "Words", "Questions" });

            parameters.addParameter<string[][]>(
                "Cues",
                "Words or qumestions that are presented. Matching of the cue is insensitive to casing",
                "", "", "Monkey,Nut,Mies", new string[] { "Cues"});

            parameters.addParameter<double>(
                "CueInputDelay",
                "Amount of time between presentation of cue and inputs.",
                "0", "", "2s");

            parameters.addParameter<string[][]>(
                "Input",
                "Specifies what input will be available, and in what configuration. Use the defined backspace code to create a backspace key and an underscore '_' as space.",
                "", "", ",,,,;,BS,e,o,r;,t,a,s,u;,i,n,l,y;,h,d,f,b;,c,m,p,x;,w,g,k,j;,BS,v,q,z");

            parameters.addParameter<string>(
                "BackspaceCode",
                "When cues are words, a code can be given here that can be used in the Input matrix to create a backspace key.",
                "", "", "BS");

            parameters.addParameter<bool>(
                "ToTopOnIncorrectRow",
                "If selected, the cursor returns to the top and starts selecting rows again whenever an incorrect row is selected.",
                "0");

            parameters.addParameter<bool>(
                "ShowScore",
                "Whether to show the score on the screen",
                "0");

            parameters.addParameter<int>(
                "MaxRowLoops",
                "Maximal number of times (the cells in) a row is looped over before the row is exited and the row highlighting of the rows starts again.",
                "0", "", "2");

            parameters.addParameter<double>(
                "RowSelectDelay",
                "Amount of time before continuing to next row",
                "0", "", "3s");

            parameters.addParameter<double>(
                "RowSelectedDelay",
                "Amount of time to wait after selecting a row",
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
                "InterStimulusInterval",
                "Amount of time in seconds between presenting cues.",
                "0", "", "1s");

            parameters.addParameter<bool>(
                "SynthesizeSpeech",
                "Whether to synthesize speech",
                "0");

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
            
            // configured as stand-alone, disallow exit
            allowExit = false;

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

            // retrieve the task delay
            taskStartDelay = newParameters.getValueInSamples("TaskStartDelay");
            if (taskStartDelay < 0) {
                logger.Error("Start delay cannot be less than 0");
                return false;
            }

            // retrieve the countdown time
            countdownTime = newParameters.getValueInSamples("CountdownTime");
            if (countdownTime < 0) {
                logger.Error("Countdown time cannot be less than 0");
                return false;
            }

            // retrieve the cuetype
            cueType = parameters.getValue<int>("CueType");

            // retrieve and check backspace code
            if (string.IsNullOrEmpty(newParameters.getValue<string>("BackspaceCode"))) {
                logger.Warn("No backspace code given, defaulting to 'BACK'.");
                backspaceCode = "BACK";
            } else {
                backspaceCode = newParameters.getValue<string>("BackspaceCode");
            }

            // retrieve and check the cues
            string[][] cuesMatrix = newParameters.getValue<string[][]>("Cues");
            if (cuesMatrix.Length != 1) {
                logger.Error("Cues matrix must contain exactly one column.");
                return false;
            } else if (cuesMatrix[0].Length < 1) {
                logger.Error("No cues defined.");
                return false;
            } else {
                cues.Clear();                       // reset cues list before adding new cues
                for(int cue = 0; cue < cuesMatrix[0].Length; cue++) {
                    if (cuesMatrix[0][cue].Trim().Length != 0) {
                        cues.Add(cuesMatrix[0][cue]);
                    } else {
                        logger.Warn("Skipped cue " + (cue + 1) + " because cue was empty.");
                    }
                }
                if (cues.Count == 0) {
                    logger.Error("No cues were set.");
                    return false;
                }

            }

            // retrieve time between cues and inputs
            cueInputDelay = newParameters.getValueInSamples("CueInputDelay");
            if (cueInputDelay < 0) {
                logger.Error("Cue input delay cannot be less than 0");
                return false;
            }

            // retrieve matrix with input options
            inputArray = parameters.getValue<string[][]>("Input");
            if (inputArray.Length > 0 && inputArray[0].Length >= 0) {

                //
                numSpellerRows = inputArray[0].Length;
                numSpellerColumns = inputArray.Length;

                if (numSpellerColumns > 30 || numSpellerRows > 30) {
                    logger.Error("The number of columns or rows cannot exceed 30.");
                    return false;
                }

                inputs = new string[numSpellerRows * numSpellerColumns];
                for(int row = 0; row < numSpellerRows; row++) {
                    for(int col = 0; col < numSpellerColumns; col++) {
                        inputs[(numSpellerColumns * row) + col] = inputArray[col][row];
                    }
                }

            } else {
                logger.Error("Input matrix is not defined.");
                return false;
            }

            // check if all the cues can be spelled using the available characters
            if (cueType == 0) {

                // list the available characters
                string allChars = "";
                for (int i = 0; i < inputs.Length; i++) {
                    if (!inputs[i].Equals(backspaceCode))
                        allChars += inputs[i];
                }
                allChars = allChars.ToLower();

                // check cues
                for (int i = 0; i < cues.Count; i++) {
                    string lCue = cues[i].ToLower();
                    for (int j = 0; j < lCue.Length; j++) {
                        if (!allChars.Contains(lCue.Substring(j, 1).ToLower())) {
                            logger.Warn("The letter '" + lCue.Substring(j, 1) + "' of cue '" + cues[i] + "' is missing from the available characters");
                        }
                    }
                }

            }

            // retrieve the ISI
            interStimInterval = newParameters.getValueInSamples("InterStimulusInterval");
            if (interStimInterval < 0) {
                logger.Error("Inter stimulus interval cannot be less than 0");
                return false;
            }

            // retrieve selection delays and settings
            maxRowLoop = newParameters.getValue<int>("MaxRowLoops");
            rowSelectDelay = newParameters.getValueInSamples("RowSelectDelay");
            rowSelectedDelay = newParameters.getValueInSamples("RowSelectedDelay");
            columnSelectDelay = newParameters.getValueInSamples("ColumnSelectDelay");
            columnSelectedDelay = newParameters.getValueInSamples("ColumnSelectedDelay");
            if (maxRowLoop < 1 || rowSelectDelay < 1 || rowSelectedDelay < 1 || columnSelectDelay < 1 || columnSelectedDelay < 1) {
                logger.Error("The 'MaxRowLoops', 'RowSelectDelay', 'RowSelectedDelay', 'ColumnSelectDelay', 'ColumnSelectedDelay' parameters should not be less than 1");
                return false;
            }

            // 
            toTopOnIncorrectRow = newParameters.getValue<bool>("ToTopOnIncorrectRow");
            showScore = newParameters.getValue<bool>("ShowScore");
            synthesizeSpeech = newParameters.getValue<bool>("SynthesizeSpeech");

            // return success
            return true;

        }

        public bool initialize() {
                                
            // lock for thread safety
            lock(lockView) {

                // create extra row for exit if needed
                if (allowExit) numSpellerRows++;

                // calculate the cells for the task
                int numCells = numSpellerRows * numSpellerColumns;

                // create the array of cells for the task
                cells = new List<SpellerCell>(0);

                // reset counter and backspace present flag
                int counter = 0;
                backspacePresent = false;

                // fill cell array
                for (int i = 0; i < numCells; i++) {
                    if (i == 0 && allowExit)                    cells.Add(new SpellerCell(0, 0, 0, 0, SpellerCell.CellType.Exit, ""));
                    else if (i < numSpellerColumns && allowExit)      cells.Add(new SpellerCell(0, 0, 0, 0, SpellerCell.CellType.Empty, ""));
                    else {
                        if (inputs[counter] == backspaceCode) {
                            cells.Add(new SpellerCell(0, 0, 0, 0, SpellerCell.CellType.Backspace, inputs[counter]));
                            backspacePresent = true;
                        } else if (string.IsNullOrWhiteSpace(inputs[counter])) {
                            cells.Add(new SpellerCell(0, 0, 0, 0, SpellerCell.CellType.Empty, inputs[counter]));
                        } else {
                            cells.Add(new SpellerCell(0, 0, 0, 0, SpellerCell.CellType.Input, inputs[counter]));
                        }
                        counter++;

                    }
                }

                // check the view (thread) already exists, stop and clear the old one.
                destroyView();

                // initialize the view
                initializeView();
            }

            // return success
            return true;

        }

        private void initializeView() {

            // create the view
            view = new SpellerView(windowRedrawFreqMax, windowLeft, windowTop, windowWidth, windowHeight, false);
            view.setBackgroundColor(windowBackgroundColor.getRed(), windowBackgroundColor.getGreen(), windowBackgroundColor.getBlue());

            // set task specific display attributes 
            view.setFixation(false);                                            // hide the fixation
            view.setCountDown(-1);                                              // hide the countdown                        
            view.setLongestCue(longestCue());                                   // get longest cue and transfer to view class

            // initialize the cells for the scene
            view.initGridPositions(cells, numSpellerRows, numSpellerColumns, 10);

            // start the scene thread
            view.start();

            // wait till the resources are loaded or a maximum amount of 30 seconds (30.000 / 50 = 600)
            // (resourcesLoaded also includes whether GL is loaded)
            int waitCounter = 600;
            while (!view.resourcesLoaded() && waitCounter > 0) {
                Thread.Sleep(50);
                waitCounter--;
            }

            //set up a speech synthesizer object
            if (synthesizeSpeech) {
                synthesizer = new SpeechSynthesizer();
                synthesizer.Volume = 100;  // 0...100
                synthesizer.Rate = 0;     // -10...10
                synthesizer.SetOutputToDefaultAudioDevice();
                synthesizer.SpeakAsync("Auditory speller");
            }

        }

        public void start() {

            // lock for thread safety
            lock(lockView) {

                // if no view exists, exit
                if (view == null)   return;

                // log event task is started, including type of cues used
                if (cueType == 0)           Data.logEvent(2, "TaskStart", CLASS_NAME + ";Words");
                else if (cueType == 1)      Data.logEvent(2, "TaskStart", CLASS_NAME + ";Questions");
                else                        Data.logEvent(2, "TaskStart", CLASS_NAME + ";Unknown");

                // clear view texts
                view.setInstructionText("");
                view.setCueText("");
                view.setInputText("");

                // initialize task variables
                score = 0;
                cueCounter = 0;             
                currentCharacterIndex = 0;
                correctClicks = 0;
                totalClicks = 0;
                backSpacesNeeded = 0;
                wordSpelled = false;
                waitText = "";
                spellerText = "";
                                    
                // 
                if (cueType == 0)
                    currentCharacter = cues[cueCounter].Substring(currentCharacterIndex, 1).ToLower();

	            // reset countdown to the countdown time
	            countdownCounter = countdownTime;

                // 
	            if (taskStartDelay != 0) {
		            // wait

		            // set state to wait
                    waitCounter = taskStartDelay;
                    afterWait = TaskStates.CountDown;
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

            // stop the task
            lock (lockView) {
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
            // TODO connectionLost as state instead as seperate bool?
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
                        // just discovered it was lost

                        // set the connection as was lost (this also will make sure the lines in this block willl only run once)
                        connectionWasLost = true;

                        // pauze the task
                        pauseTask();

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
	            if (taskPaused)		    return;
                
	            // check if there is a click
	            bool click = (input == 1);

	            // use the task state
	            switch (taskState) {

		            case TaskStates.Wait:
			
			            if(waitCounter == 0) {

                            // reset wait Text and set the state to the state needed after wait
                            waitText = "";
                            view.setInstructionText(waitText);
                            setState(afterWait);

			            } else
				            waitCounter--;

			            break;

		            case TaskStates.CountDown:

                        // check if the task is counting down
                        if (countdownCounter > 0) {

                            // display the countdown
                            view.setCountDown((int)Math.Floor((countdownCounter - 1) / MainThread.getPipelineSamplesPerSecond()) + 1);

                            // reduce the countdown timer
                            countdownCounter--;

                        } else {

				            // hide the countdown counter
				            view.setCountDown(-1);

				            // Show character grid, score, and cue
				            view.gridVisible(true);
                            if (showScore)      view.setScore(score);
                            view.setCueText(cues[cueCounter]);

                            // log event countdown is started
                            Data.logEvent(2, "TrialStart ", CLASS_NAME);

                            // begin task
                            setState(TaskStates.InitialCue);
			            }

			            break;


                    case TaskStates.InitialCue:

                        if (waitCounter > 0)    waitCounter--;
                        else
                            setState(TaskStates.RowSelect);    

                        break;

                    // highlighting a row
		            case TaskStates.RowSelect:

                        // if clicked, select row, otherwise continue
			            if (click)
                            setState(TaskStates.RowSelected);

                        else {

				            if (waitCounter == 0) {
                                
                                // Advance to next row and wrap around
                                currentRowID++;
					            if(currentRowID >= numSpellerRows)		currentRowID = 0;
					            
                                // select the row in the scene
                                view.selectRow(currentRowID, false);

                                // synthesize row
                                if (synthesizeSpeech && synthesizer != null) {

                                    // try to find the first non-empty option in the row
                                    string cellContent = "";
                                    for (int i = numSpellerColumns * currentRowID; i < numSpellerColumns * (currentRowID + 1); i++) {
                                        if (cells[i].cellType != SpellerCell.CellType.Empty) {
                                            cellContent = cells[i].content;
                                            break;
                                        }
                                    }

                                    // synthesize
                                    if (string.IsNullOrEmpty(cellContent))
                                        synthesizer.SpeakAsync("Empty Row");
                                    else
                                        synthesizer.SpeakAsync("Row " + cellContent);
                                    
                                }

                                // check whether selected row contains target, if using words as cues
                                if (cueType == 0) {
                                    if (targetInRow(currentRowID))     Data.logEvent(2, "TargetRow", currentRowID.ToString());
                                    else                        Data.logEvent(2, "NonTargetRow", currentRowID.ToString());
                                } else                          Data.logEvent(2, "Row", currentRowID.ToString());

                                // reset the timer
                                waitCounter = rowSelectDelay;

				            } else  waitCounter--;
			            }

			            break;

                    // row was selected
                    case TaskStates.RowSelected:

                        // wait duration of delay, after that proceed to selecting row or columns
                        if (waitCounter == 0) {
                            
                            // synthesize speech
                            if (synthesizeSpeech && synthesizer != null) {

                              // try to find the first non-empty option in the row
                                string cellContent = "";
                                for (int i = numSpellerColumns * currentRowID; i < numSpellerColumns * (currentRowID + 1); i++) {
                                    if (cells[i].cellType != SpellerCell.CellType.Empty) {
                                        cellContent = cells[i].content;
                                        break;
                                    }
                                }

                                // synthesize cell
                                if (string.IsNullOrEmpty(cellContent))
                                        synthesizer.SpeakAsync("Empty Row");
                                else
                                    synthesizer.SpeakAsync("Row " + cellContent);

                            }

                            // update total number of clicks
                            totalClicks++;

                            // if row contains target, update number of correct clicks
                            if (targetInRow(currentRowID)) correctClicks++;

                            // if incorrect row is selected and parameter to return to top row is true, return to highlighting rows from top (do not do so in questionmode when no answers are provided). Otherwise, start highlighting columns 
                            if (!targetInRow(currentRowID) && toTopOnIncorrectRow && !(cueType == 1 && answers==null) )    setState(TaskStates.RowSelect);                         
                            else                                                                                    setState(TaskStates.ColumnSelect);

                        } else waitCounter--;

			            break;

                    // highlighting a column
                    case TaskStates.ColumnSelect:
                        
                        // if clicked, go to ColumnSelected state, else,remain in this state
                        if (click)  setState(TaskStates.ColumnSelected);
                        else {
                            
                            // if time to highlight column has passed
                            if (waitCounter == 0) {

                                // Advance to next row and wrap around
                                currentColumnID++;
                                
                                // if the end of row has been reached
                                if (currentColumnID >= numSpellerColumns) {
                                    
                                    // reset column id
						            currentColumnID = 0;

						            // increase how often we have looped through row
						            rowLoopCounter++;
					            }

                                // synthesize the cell
                                if (synthesizeSpeech && synthesizer != null) {
                                    if (cells[numSpellerColumns * currentRowID + currentColumnID].cellType == SpellerCell.CellType.Empty)
                                        synthesizer.SpeakAsync("Empty Cell");
                                    else
                                        synthesizer.SpeakAsync(cells[numSpellerColumns * currentRowID + currentColumnID].content);
                                }

                                // check if there has been looped more than the defined maximal times
                                if (rowLoopCounter >= maxRowLoop) {
						
						            // start from the top
						            setState(TaskStates.RowSelect);

					            } else {

                                    // get selected cell
                                    SpellerCell activeCell = cells[numSpellerColumns * currentRowID + currentColumnID];

                                    // log event that column is highlighted, and the type and content of the cell
                                    if (cueType == 0) {                                        
                                        
                                        if (activeCell.content.ToLower() == currentCharacter)   Data.logEvent(2, activeCell.cellType.ToString() + "Column", currentColumnID.ToString() + ";" + activeCell.content + ";" + "1");
                                        else                                                    Data.logEvent(2, activeCell.cellType.ToString() + "Column", currentColumnID.ToString() + ";" + activeCell.content + ";" + "0");

                                    } else      
                                        Data.logEvent(2, activeCell.cellType.ToString() + "Column", currentColumnID.ToString() + ";" + activeCell.content);

                                    // select the cell in the scene
                                    view.selectCell(currentRowID, currentColumnID, false);
						
						            // reset the timer
						            waitCounter = columnSelectDelay;
					            }

				            } else 
					            waitCounter--;

			            }

			            break;

                    // column was selected
                    case TaskStates.ColumnSelected:

                        if (waitCounter == 0) {
                            
                            // get clicked cell
                            SpellerCell activeCell = cells[numSpellerColumns * currentRowID + currentColumnID];
                            
                            // synthesize the cell content
                            if (synthesizeSpeech && synthesizer != null) {
                                if (activeCell.cellType == SpellerCell.CellType.Empty)
                                    synthesizer.SpeakAsync("Empty Cell");
                                else
                                    synthesizer.SpeakAsync(activeCell.content);
                            }

                            // store that a cell is clicked
                            totalClicks++;
                            
                            // if we are using words as cues
                            if (cueType == 0) {

                                // debug
                                logger.Debug("Current target: " + currentCharacter + " at index: " + currentCharacterIndex);

                                // check whether backspace, input, empty or exit was clicked
                                // if empty, do nothing except for counting it as a correct click in case target is not in the current row, because in this case it is otherwise not possible to get back to the top row without making an additional wrong click, 
                                // otherwise either exit, or update input text and next target and check if this results in a spelled word
                                if (activeCell.cellType == SpellerCell.CellType.Backspace) {
                                    updateInputText("", true);
                                    updateTarget(activeCell.content);

                                } else if (activeCell.cellType == SpellerCell.CellType.Input) {
                                    updateInputText(activeCell.content, false);
                                    updateTarget(activeCell.content);

                                } else if(activeCell.cellType == SpellerCell.CellType.Empty && !targetInRow(currentRowID)) {
                                    correctClicks++;

                                } else if (activeCell.cellType == SpellerCell.CellType.Exit && allowExit) {

                                    // log event task is stopped
                                    Data.logEvent(2, "TaskStop", CLASS_NAME + ";user");

                                    // stop the task
                                    // this will also call stop(), and as a result stopTask()
                                    if (childApplication)       AppChild_stop();
                                    else                        MainThread.stop(false);

                                }

                                // if cue is completed (word is spelled)
                                if (wordSpelled) {

                                    // calculate accuracy
                                    double accuracy = Math.Round(((double)correctClicks / (double)totalClicks) * 100);
                                    waitText = "Accuracy: " + accuracy.ToString() + "%";

                                    // log event cue spelled
                                    Data.logEvent(2, "WordSpelled", cues[cueCounter] + ";" + accuracy);

                                    // add one to the score and display
                                    score++;
                                    if (showScore)      view.setScore(score);

                                    // reset the speller text
                                    spellerText = "";
                                    view.setInputText("");

                                    // update cue counter and go to wait state. Set state after that based on whether all cues have been shown
                                    cueCounter++;
                                    if (cueCounter == cues.Count) {
                                        afterWait = TaskStates.EndText;

                                    } else {

                                        // set new cue and target, and reset cue-dependent variables
                                        view.setCueText(cues[cueCounter]);
                                        currentCharacterIndex = 0;
                                        currentCharacter = cues[cueCounter].Substring(currentCharacterIndex, 1).ToLower();
                                        wordSpelled = false;
                                        correctClicks = 0;
                                        totalClicks = 0;
                                        backSpacesNeeded = 0;

                                        // start again
                                        afterWait = TaskStates.InitialCue;

                                    }

                                    // wait the interstimulus interval before presenting next stimulus
                                    waitCounter = interStimInterval;
                                    setState(TaskStates.Wait);

                                } else {
                                    setState(TaskStates.RowSelect);

                                }

                                // debug
                                logger.Debug("Clicked on cell type: " + activeCell.cellType);
                                logger.Debug("New target: " + currentCharacter + " at index: " + currentCharacterIndex);
                            
                            } else {
                                // if we are using questions as cues

                                // check type of cell that was clicked. If input type, store input and proceed to next question; if exit, exit application. Other tpyes, return to highlighting rows.
                                if (activeCell.cellType == SpellerCell.CellType.Input) {

                                    // update view
                                    updateInputText(activeCell.content, false);

                                    // log event question answered
                                    Data.logEvent(2, "QuestionAnswered", cues[cueCounter] + ";" + activeCell.content);

                                    // add one to the score and display
                                    score++;
                                    if (showScore)      view.setScore(score);

                                    // reset the speller text
                                    spellerText = "";
                                    view.setInputText("");

                                    // update cue counter, if all cues have been shown, show end text
                                    cueCounter++;
                                    if (cueCounter == cues.Count) {
                                        setState(TaskStates.EndText);

                                    } else {

                                        // set new cue and wait the interstimulus interval before presenting next cue
                                        view.setCueText(cues[cueCounter]);
                                        waitCounter = interStimInterval;
                                        afterWait = TaskStates.InitialCue;
                                        setState(TaskStates.Wait);

                                    }

                                } else if (activeCell.cellType == SpellerCell.CellType.Exit && allowExit) {

                                    // log event task is stopped
                                    Data.logEvent(2, "TaskStop", CLASS_NAME + ";user");

                                    // stop the task
                                    // this will also call stop(), and as a result stopTask()
                                    if (childApplication)   AppChild_stop();
                                    else                    MainThread.stop(false);

                                } else {
                                    
                                    // if other type than input or exit, start highlighting rows again 
                                    setState(TaskStates.RowSelect);
                                }

                                // debug
                                logger.Debug("Clicked on cell type: " + activeCell.cellType);
                            }
                            
                        } else 
                            waitCounter--;

			            break;

		            case TaskStates.EndText:

			            if (waitCounter == 0) {

                            // don't show text
                            view.setCueText("");

                            // log event task is stopped
                            Data.logEvent(2, "TaskStop", CLASS_NAME + ";end");

                            // stop the run
                            // this will also call stop(), and as a result stopTask()
                            if (childApplication)       AppChild_stop();
                            else                        MainThread.stop(false);

                        } else  
                            waitCounter--;

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

            }
        }


        // pauses the task
        private void pauseTask() {
	        if (view == null)   return;

            // log event task is paused
            Data.logEvent(2, "TaskPause", CLASS_NAME);

            // set task as pauzed
            taskPaused = true;
            
            // hide everything
            view.setFixation(false);
            view.setCountDown(-1);
            view.gridVisible(false);
            view.setScore(-1);
            view.setCueText("");
            view.setInputText("");

        }

        // resume the task
        private void resumeTask() {
            if (view == null)   return;

            // log event task is resumed
            Data.logEvent(2, "TaskResume", CLASS_NAME);

            // in task
            if (taskState == TaskStates.InitialCue || taskState == TaskStates.RowSelect || taskState == TaskStates.RowSelected || taskState == TaskStates.ColumnSelect || taskState == TaskStates.ColumnSelected) {

                // show the cue again
                view.setCueText(cues[cueCounter]);

                if (taskState == TaskStates.RowSelect || taskState == TaskStates.RowSelected || taskState == TaskStates.ColumnSelect || taskState == TaskStates.ColumnSelected) {
			
			        // show the speller elements
			        view.gridVisible(true);
                    view.setInputText(spellerText);
			        if (showScore)      view.setScore(score);
                }

		    }
            

            // store the exact position of the "cursor"
            // (setState will reset the position)
	        int resumeRowID = currentRowID;
			int resumeColumnID = currentColumnID;
            int resumeRowLoopCounter = rowLoopCounter;

	        // set the previous state
	        setState(taskState);

            // restore the exact position of the "cursor" (after setState)
            currentRowID = resumeRowID;
			currentColumnID = resumeColumnID;
            rowLoopCounter = resumeRowLoopCounter;

            // highlight the row or cell in the view
            if (taskState == TaskStates.RowSelect)      view.selectRow(currentRowID, false);
            if (taskState == TaskStates.RowSelected)    view.selectRow(currentRowID, true);
            if (taskState == TaskStates.ColumnSelect)   view.selectCell(currentRowID, currentColumnID, false);
            if (taskState == TaskStates.ColumnSelected) view.selectCell(currentRowID, currentColumnID, true);

	        // set task as not longer pauzed
	        taskPaused = false;

        }


        private void destroyView() {

            // dispose and release the synthesizer
            if (synthesizer != null) {
                synthesizer.Dispose();
                synthesizer = null;
            }
             
	        // stop (waits until finished) and release the view thread
	        if (view != null) {
                view.stop();
                view = null;
	        }

        }


        private void setState(TaskStates state) {

	        // Set state
	        taskState = state;

	        switch (state) {
                case TaskStates.Wait:

                    // log event
                    Data.logEvent(2, "WaitPresented ", waitCounter.ToString());

                    // hide all visual elements
                    view.setInstructionText(waitText);
                    view.setCueText("");
			        view.setFixation(false);
                    view.setCountDown(-1);
                    view.selectRow(-1, false);
                    view.setScore(-1);
                    view.gridVisible(false);

			        break;

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

                // intitial presentation of cue
                case TaskStates.InitialCue:

                    // set waitcounter to delay between cue and input, show cue, but not inputs
                    waitCounter = cueInputDelay;
                    view.gridVisible(false);
                    view.setCueText(cues[cueCounter]);

                    break;

                case TaskStates.RowSelect:

                    // show cue, score and grid
                    view.setScore(score);
                    view.gridVisible(true);
                    view.setCueText(cues[cueCounter]);

                    // reset the row and columns positions
                    currentRowID = 0;
			        currentColumnID = 0;

                    // check whether selected row contains target and throw event, checking for targets if used cues are words
                    if (cueType == 0) {
                        Data.logEvent(2, targetInRow(currentRowID) ? "TargetRow" : "NonTargetRow", currentRowID.ToString());
                    } else {
                        Data.logEvent(2, "Row", currentRowID.ToString());
                    }

                    // if there is only one row, directly go to state where columns can be selected, otherwise highlight current row and set waitcounter
                    if (numSpellerRows == 1) {
                        setState(TaskStates.ColumnSelect);
                    } else {
                        view.selectRow(currentRowID, false);
                        waitCounter = rowSelectDelay;
                    }

			        break;

                // row was selected 
                case TaskStates.RowSelected:

                    // highlight row
                    view.selectRow(currentRowID, true);

                    // if we are using words as cues, check if target is in current row and throw event accordingly, if using questions as cues (no target), throw generic event
                    if (cueType == 0) {

                        // if the incorrect row is selected and the setting is to return to the top row, set state to selecting rows again, otherwise, start selecting cells in this row
                        Data.logEvent(2, "RowClick", targetInRow(currentRowID) ? "1" : "0");

                    } else {

                        Data.logEvent(2, "RowClick", "");

                    }

                    // set waitcounter
                    waitCounter = rowSelectedDelay;

			        break;

                // highlighting columns
                case TaskStates.ColumnSelect:
	
			        // reset the column position
			        currentColumnID = 0;

                    // get selected cell
                    SpellerCell activeCell = cells[numSpellerColumns * currentRowID + currentColumnID];

                    // log event that column is highlighted, and the type and content of the cell
                    Data.logEvent(2, activeCell.cellType.ToString() + "Column", currentColumnID.ToString() + ";" + activeCell.content);

                    // select cell
                    view.selectCell(currentRowID, currentColumnID, false);

                    // reset how often there was looped in this row
                    rowLoopCounter = 0;

                    // 
			        waitCounter = columnSelectDelay;

			        break;

                // cell is selected
                case TaskStates.ColumnSelected:

                    // get selected cell
                    SpellerCell selectedCell = cells[numSpellerColumns * currentRowID + currentColumnID];

                    // log event that column is selected, checking for targets if used cues are words
                    if (cueType == 0) {
                        if (selectedCell.content.ToLower() == currentCharacter)     Data.logEvent(2, "CellClick" + selectedCell.cellType.ToString(), "1");
                        else                                                        Data.logEvent(2, "CellClick" + selectedCell.cellType.ToString(), "0");
                    } else                                          
                        Data.logEvent(2, "CellClick" + selectedCell.cellType.ToString(), "");

                    // select cell and highlight
                    view.selectCell(currentRowID, currentColumnID, true);

                    // 
			        waitCounter = columnSelectedDelay;

			        break;

                case TaskStates.EndText:

                    // remove cue, countdown and input text
                    view.setCountDown(-1);
                    view.setCueText("");
                    view.setInputText("");

                    // hide character grid
                    view.gridVisible(false);

			        // show text
				    view.setInstructionText("Done");

                    // set duration for text to be shown at the end (3s)
                    waitCounter = (int)(MainThread.getPipelineSamplesPerSecond() * 3.0);

                    break;
	        }

        }

        // Stop the task
        private void stopTask() {
            if (view == null) return;
            
            // Set state to Wait
            setState(TaskStates.EndText);

        }



        public void updateInputText(string text, bool backspace) {

            // if backspace was pressed and there are characters to remove, remove last character; if no backspace is pressed, add inputted character at the end of input text
            if (backspace & spellerText.Length > 0)
                spellerText = spellerText.Remove(spellerText.Length - 1);
            else
                spellerText = spellerText + text;

            view.setInputText(spellerText);

        }



        // update the current cue, based on the user input
        private void updateTarget(string input) {

            // get current cue
            string cue = cues[cueCounter];

            // if correctly selected current target, select next element in cue as target, if we are not at end of cue.
            if (input.ToLower() == currentCharacter) {

                // store that correct input was made
                correctClicks++;

                // if a backspace is correctly selected, decrease the amount of neede backspaces if an input other than backspace is correctly provided, increase currentIndexTarget
                if (input == backspaceCode)     backSpacesNeeded--;  
                else                            currentCharacterIndex++;

                // if no more backspaces are needed, determine next target based on cue, otherwise new target is backspace if the current input was not a backspace
                if (backSpacesNeeded == 0) {

                    if (currentCharacterIndex == cue.Length) {
                        wordSpelled = true;                                                         // if end of the trial-cue was reached, word is spelled
                        currentCharacter = null;                                                    // set target to null
                    } else {
                        currentCharacter = cue.Substring(currentCharacterIndex, 1).ToLower();       // if not at end of cue, select next target in cue
                    }

                } else {
                    currentCharacter = backspaceCode.ToLower();
                }

            } else if (backspacePresent & input != backspaceCode) {
                currentCharacter = backspaceCode.ToLower();                 // if incorrectly selected, if a backspace is avaialable for the user, this becomes the next target. If no backspace is available, do nothing, target stays the same. 
                backSpacesNeeded++;                                         // increase the amount of backspaces needed

            }
        }


        // check if target is in current row
        private bool targetInRow(int rowID) {
            bool inRow = false;
            for (int cellId = numSpellerColumns * rowID; cellId < (numSpellerColumns * (rowID + 1)); cellId++) {
                string cellContent = cells[cellId].content;
                if (cellContent.ToLower() == currentCharacter)  inRow = true;
            }
            return inRow;
        }

        // return length of longest cue, relevant for constructing view
        private int longestCue() {

            int cueLength = -1;

            if (cues != null)
                for (int cue = 0; cue < cues.Count; cue++) {
                    cueLength = Math.Max(cueLength, cues[cue].Length);
                }

            return cueLength;
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
            allowExit = true;                  // child task, allow exit
            newParameters.setValue("WindowBackgroundColor", "0;0;0");
            newParameters.setValue("TaskStartDelay", "2s");
            newParameters.setValue("CountdownTime", "3s");
            newParameters.setValue("TaskInputChannel", 1);
            newParameters.setValue("RowSelectDelay", 12.0);
            newParameters.setValue("RowSelectedDelay", 5.0);
            newParameters.setValue("ColumnSelectDelay", 12.0);
            newParameters.setValue("ColumnSelectedDelay", 5.0);
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
		        if (!childApplicationSuspended)		process(input);

	        }

        }

        public void AppChild_resume() {

            // lock for thread safety
            lock (lockView) {

                // initialize the view
                initializeView();

	            // resume the task
	            resumeTask();

            }
	
	        // flag task as no longer suspended
	        childApplicationSuspended = false;

        }

        public void AppChild_suspend() {

            // flag task as suspended
            childApplicationSuspended = true;

            // lock for thread safety and destroy the scene
            lock (lockView) {

                // pauze the task
                pauseTask();

                // destroy the scene
                destroyView();

            }

        }

    }

}