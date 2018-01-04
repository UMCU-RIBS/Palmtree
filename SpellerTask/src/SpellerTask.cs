/**
 * The SpellerTask class
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
using System.Collections.Generic;
using System.Threading;
using UNP.Applications;
using UNP.Core;
using UNP.Core.Helpers;
using UNP.Core.Params;
using UNP.Core.DataIO;
using System.Collections.Specialized;

namespace SpellerTask {

    /// <summary>
    /// The <c>SpellerTask</c> class.
    /// 
    /// ...
    /// </summary>
    public class SpellerTask : IApplication, IApplicationUNP {

        // fundamentals
        private const int CLASS_VERSION = 0;
        private const string CLASS_NAME = "SpellerTask";
        private const string CONNECTION_LOST_SOUND = "sounds\\focuson.wav";
        private static Logger logger = LogManager.GetLogger(CLASS_NAME);                        // the logger object for the view
        private static Parameters parameters = null;
        private int inputChannels = 0;

        // status
        private bool unpMenuTask = false;								        // flag whether the task is created by the UNPMenu
        private bool unpMenuTaskRunning = false;						        // flag to hold whether the task should is running (setting this to false is also used to notify the UNPMenu that the task is finished)
        private bool unpMenuTaskSuspended = false;						        // flag to hold whether the task is suspended (view will be destroyed/re-initiated)
        private bool taskPaused = false;                                        // flag to hold whether the task is pauzed (view will remain active, e.g. connection lost)
        private bool connectionLost = false;							        // flag to hold whether the connection is lost
        private bool connectionWasLost = false;						            // flag to hold whether the connection has been lost (should be reset after being re-connected)
        private TaskStates afterWait = TaskStates.CountDown;                    // the task state to go to after a wait
        private TaskStates taskState = TaskStates.Wait;
        private TaskStates previousTaskState = TaskStates.Wait;

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
        private int taskFirstRunStartDelay = 0;                                 // the first run start delay in sample blocks
        private int countdownTime = 0;                                          // the time the countdown takes in sample blocks
        private int rowSelectDelay = 0;
        private int rowSelectedDelay = 0;
        private int columnSelectDelay = 0;
        private int columnSelectedDelay = 0;
        private List<SpellerCell> holes = new List<SpellerCell>(0);
        private bool allowExit = false;
        private int holeRows = 0;
        private int holeColumns = 0;
        private bool ToTopOnIncorrectRow = false;                               // whether to return to top row when an incorrect row is selected.
        private bool showScore = false;                                         // whether or not to show score
        private string[][] inputArray = null;
        private string backspaceCode = "";
        private string[] inputs = null;
        private List<string> cues = new List<string>(0);
        private bool backspacePresent = false;                                  // whether or not a backspace button is present in the input options
        private int isi = 0;                                                    // inter stimulus interval, in samples
        private int cueType = 0;

        // task specific variables
        private int waitCounter = 0;
        private int rowID = 0;
        private int columnID = 0;
        private int backSpacesNeeded = 0;
        private string currentTarget = null;
        private int currentTargetIndex = 0;                                     // the index of the current target in the currently presented word
        private bool wordSpelled = false;
        private int correctClicks = 0;
        private int totalClicks = 0;
        string waitText = "";
        private int countdownCounter = 0;					                    // the countdown timer
        private int score = 0;						                            // the score of the user correctly responding to the cues
        private int rowLoopCounter = 0;
        private int cueCounter = 0;

        private enum TaskStates : int {
            Wait,
            CountDown,
            RowSelect,
            RowSelected,
            ColumnSelect,
            ColumnSelected,
            EndText
        };

        public SpellerTask() : this(false) { }
        public SpellerTask(bool UNPMenuTask) {

            // transfer the UNP menu task flag
            unpMenuTask = UNPMenuTask;
            
            // check if the task is standalone (not unp menu)
            if (!unpMenuTask) {

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

            parameters.addParameter<int>(
                "TaskFirstRunStartDelay",
                "Amount of time before the task starts (on the first run of the task)",
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
                "Words or questions that are presented.",
                "", "", "Aap,Noot,Mies", new string[] { "Cues"});

            parameters.addParameter<string[][]>(
                "Input",
                "Specifies which letters will be available to spell, and in what configuration. Use the defined backspace code to create a backspace key and an underscore '_' as space.",
                "", "", "Q;W;E;R;T;Y");

            parameters.addParameter<string>(
                "BackspaceCode",
                "When cues are words, a code can be given here that can be used in the Input matrix to create a backspace key.",
                "", "", "BS");

            parameters.addParameter<bool>(
                "ToTopOnIncorrectRow",
                "If selected, the cursor returns to the top and starts selecting rows again whenever an incorrect row is selected.",
                "0");

            parameters.addParameter<bool>(
                "showScore",
                "If selected, show score.",
                "0");

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
            if (taskInputChannel > inputChannels) {
                logger.Error("Input should come from channel " + taskInputChannel + ", however only " + inputChannels + " channels are coming in");
                return false;
            }

            // retrieve the task delays 
            taskFirstRunStartDelay = newParameters.getValueInSamples("TaskFirstRunStartDelay");
            if (taskFirstRunStartDelay < 0) {
                logger.Error("Start delays cannot be less than 0");
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

            // retrieve cues         
            string[][] cuesMatrix = newParameters.getValue<string[][]>("Cues");

            // check if there are more columns than desired
            if (cuesMatrix.Length != 1) {
                logger.Error("Cues matrix must contain exactly one column.");
                return false;
            }

            // retrieve back space code
            if (string.IsNullOrEmpty(newParameters.getValue<string>("BackspaceCode"))) {
                logger.Warn("No backspace code given, defaulting to 'BACK'.");
                backspaceCode = "BACK";
            } else {
                backspaceCode = newParameters.getValue<string>("BackspaceCode");
            }


            // check if there are cues defined, if so, transfer to cue array
            if (cuesMatrix[0].Length < 1) {
                logger.Error("No cues defined.");
                return false;
            } else {
                cues.Clear();                       // reset cues list before adding new cues
                for(int cue = 0; cue < cuesMatrix[0].Length; cue++) {
                    if (cuesMatrix[0][cue] != " ") {
                        cues.Add(cuesMatrix[0][cue]);
                    } else {
                        logger.Warn("Skipped cue " + (cue + 1) + " because cue was empty.");
                    }
                }
                if (cues.Count == 0) {
                    logger.Error("All cues are empty.");
                    return false;
                }
            }

            // retrieve matrix with input options
            inputArray = parameters.getValue<string[][]>("Input");

            // if matrix is defined, retrieve amount of rows and columns and cell contents
            if (inputArray[0].Length >= 0 && inputArray.Length > 0) {

                //
                holeRows = inputArray[0].Length;
                holeColumns = inputArray.Length;

                if (holeColumns > 30 || holeRows > 30) {
                    logger.Error("The number of columns or rows cannot exceed 30.");
                    return false;
                }

                inputs = new string[holeRows * holeColumns];

                for(int row = 0; row < holeRows; row++) {
                    for(int col = 0; col < holeColumns; col++) {
                        inputs[(holeColumns * row) + col] = inputArray[col][row];
                    }
                }

            } else {
                logger.Error("Input matrix is not defined.");
                return false;
            }

            // retrieve the ISI
            isi = newParameters.getValueInSamples("InterStimulusInterval");
            if (isi < 0) {
                logger.Error("Inter stimulus interval cannot be less than 0");
                return false;
            }

            // retrieve selection delays and settings
            ToTopOnIncorrectRow = newParameters.getValue<bool>("ToTopOnIncorrectRow");
            showScore = newParameters.getValue<bool>("showScore");
            rowSelectDelay = newParameters.getValueInSamples("RowSelectDelay");
            rowSelectedDelay = newParameters.getValueInSamples("RowSelectedDelay");
            columnSelectDelay = newParameters.getValueInSamples("ColumnSelectDelay");
            columnSelectedDelay = newParameters.getValueInSamples("ColumnSelectedDelay");
            if (rowSelectDelay < 1 || rowSelectedDelay < 1 || columnSelectDelay < 1 || columnSelectedDelay < 1) {
                logger.Error("The 'RowSelectDelay', 'RowSelectedDelay', 'ColumnSelectDelay', 'ColumnSelectedDelay' parameters should not be less than 1");
                return false;
            }

            // return success
            return true;

        }

        public void initialize() {
                                
            // lock for thread safety
            lock(lockView) {

                // create extra row for exit if needed
                if (allowExit) holeRows++;

                // calculate the cell holes for the task
                int numHoles = holeRows * holeColumns;

                // create the array of cells for the task
                holes = new List<SpellerCell>(0);

                // reset counter and backspace present flag
                int counter = 0;
                backspacePresent = false;

                // fill cell array
                for (int i = 0; i < numHoles; i++) {
                    if (i == 0 && allowExit)                    holes.Add(new SpellerCell(0, 0, 0, 0, SpellerCell.CellType.Exit, ""));
                    else if (i < holeColumns && allowExit)      holes.Add(new SpellerCell(0, 0, 0, 0, SpellerCell.CellType.Empty, ""));
                    else {
                        if (inputs[counter] == backspaceCode) {
                            holes.Add(new SpellerCell(0, 0, 0, 0, SpellerCell.CellType.Backspace, inputs[counter]));
                            backspacePresent = true;
                        } else if (string.IsNullOrWhiteSpace(inputs[counter])) {
                            holes.Add(new SpellerCell(0, 0, 0, 0, SpellerCell.CellType.Empty, inputs[counter]));
                        } else {
                            holes.Add(new SpellerCell(0, 0, 0, 0, SpellerCell.CellType.Input, inputs[counter]));
                        }
                        counter++;

                    }
                }

                // check the view (thread) already exists, stop and clear the old one.
                destroyView();

                // initialize the view
                initializeView();
            }
        }

        private void initializeView() {

            // create the view
            view = new SpellerView(windowRedrawFreqMax, windowLeft, windowTop, windowWidth, windowHeight, false);
            view.setBackgroundColor(windowBackgroundColor.getRed(), windowBackgroundColor.getGreen(), windowBackgroundColor.getBlue());

            // set task specific display attributes 
            view.setFixation(false);                                            // hide the fixation
            view.setCountDown(-1);                                              // hide the countdown                                                                    
            view.setLongestCue(longestCue());                                   // get longest cue and transfer to view class
            view.setShowScore(showScore);                                       // wheteher to show the score or not

            // initialize the holes for the scene
            view.initGridPositions(holes, holeRows, holeColumns, 10);

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

            // lock for thread safety
            lock(lockView) {

                // if no view exists, exit
                if (view == null)   return;

                // log event task is started, including type of cues used
                if (cueType == 0)           Data.logEvent(2, "TaskStart", CLASS_NAME + ";Words");
                else if (cueType == 1)      Data.logEvent(2, "TaskStart", CLASS_NAME + ";Questions");
                else                        Data.logEvent(2, "TaskStart", CLASS_NAME + ";Unknown");

                // init vars: reset score, initialize countdown and wait timers and set fixation
                score = 0;
                cueCounter = 0;             
                currentTargetIndex = 0;
                correctClicks = 0;
                totalClicks = 0;
                backSpacesNeeded = 0;
                wordSpelled = false;
                countdownCounter = countdownTime;
                waitCounter = taskFirstRunStartDelay;
                view.setFixation(true);
                waitText = "";

                if (cueType == 0)
                    currentTarget = cues[cueCounter].Substring(currentTargetIndex, 1);
                
                // set state to wait and countdown after that
                setState(TaskStates.Wait);
                afterWait = TaskStates.CountDown;
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
            // TODO connectionLost as state instead as seperate bool?
            connectionLost = Globals.getValue<bool>("ConnectionLost");

            // process input
            process(input[taskInputChannel - 1]);

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

                // TODO: taskpaused as state instead of seperate bool?
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

				            // Show hole grid, score, and cue
				            view.gridVisible(true);
                            view.setScore(score);
                            view.setCueText(cues[cueCounter]);

                            // log event countdown is started
                            Data.logEvent(2, "TrialStart ", CLASS_NAME);

                            // begin task
                            setState(TaskStates.RowSelect);
			            }

			            break;

                    // highlighting a row
		            case TaskStates.RowSelect:

                        // if clicked, select row, otherwise continue
			            if (click)
                            setState(TaskStates.RowSelected);
                        else {

				            if(waitCounter == 0) {

                                // Advance to next row and wrap around
                                rowID++;
					            if(rowID >= holeRows)		rowID = 0;

					            // select the row in the scene
					            view.selectRow(rowID, false);

                                // check whether selected row contains target, if using words as cues
                                if (cueType == 0) {
                                    if (targetInRow(rowID))     Data.logEvent(2, "TargetRow", rowID.ToString());
                                    else                        Data.logEvent(2, "NonTargetRow", rowID.ToString());
                                } else                          Data.logEvent(2, "Row", rowID.ToString());

                                // reset the timer
                                waitCounter = rowSelectDelay;

				            } else  waitCounter--;
			            }

			            break;

                    // row was selected
                    case TaskStates.RowSelected:

                        // wait duration of delay, after that proceed to selecting row or columns
                        if (waitCounter == 0) {

                            // if incorrect row is selected and parameter to return to top row is true, return to top row
                            if (!targetInRow(rowID) && ToTopOnIncorrectRow) {
                                totalClicks++;                          // update total number of clicks
                                setState(TaskStates.RowSelect);         // return to selecting rows from top
                            } else setState(TaskStates.ColumnSelect);

                        } else waitCounter--;

			            break;

                    // highlighting a column
                    case TaskStates.ColumnSelect:
                        
                        // if clicked
                        if (click)  setState(TaskStates.ColumnSelected);
                        else {
                            
                            // if time to highlight column has passed
                            if (waitCounter == 0) {

                                // Advance to next row and wrap around
                                columnID++;

                                // if the end of row has been reached
					            if(columnID >= holeColumns) {
                                    
                                    // reset column id
						            columnID = 0;

						            // increase how often we have looped through row
						            rowLoopCounter++;
					            }

                                // get selected cell
                                SpellerCell activeCell = holes[holeColumns * rowID + columnID];

                                // log event that column is highlighted, and the type and content of the cell
                                Data.logEvent(2, activeCell.cellType.ToString() + "Column", columnID.ToString() + ";" + activeCell.content);

                                // check if there has been looped more than two times in the row with exit button
                                if (rowLoopCounter > 1 && rowID == 0 && allowExit) {
						
						            // start from the top
						            setState(TaskStates.RowSelect);

					            } else {
						            // select the cell in the scene
						            view.selectCell(rowID, columnID, false);
						
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
                            SpellerCell activeCell = holes[holeColumns * rowID + columnID];

                            // store that a cell is clicked
                            totalClicks++;
                            
                            // if we are using words as cues
                            if (cueType == 0) {

                                // debug
                                logger.Debug("Current target: " + currentTarget + " at index: " + currentTargetIndex);

                                // check whether backspace, input, empty or exit was clicked
                                // if empty, do nothing except for counting it as a correct click in case parameter 'ToTopOnIncorrectRow'is not active, because in this case it is otherwise not possible to get back to the top row without making an additional wrong click, 
                                // otherwise either exit, or update input text and next target and check if this results in a spelled word
                                if (activeCell.cellType == SpellerCell.CellType.Backspace) {
                                    view.updateInputText("", true);
                                    updateTarget(activeCell.content);

                                } else if (activeCell.cellType == SpellerCell.CellType.Input) {
                                    view.updateInputText(activeCell.content, false);
                                    updateTarget(activeCell.content);

                                } else if(activeCell.cellType == SpellerCell.CellType.Empty && !ToTopOnIncorrectRow) {
                                    correctClicks++;

                                } else if (activeCell.cellType == SpellerCell.CellType.Exit && allowExit) {

                                    // log event task is stopped
                                    Data.logEvent(2, "TaskStop", CLASS_NAME + ";user");

                                    // stop the task
                                    // this will also call stop(), and as a result stopTask()
                                    if (unpMenuTask)        UNP_stop();
                                    else                    MainThread.stop(false);

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
                                    view.setScore(score);

                                    // reset input text
                                    view.resetInputText();

                                    // update cue counter and go to wait state. Set state after that based on whether all cues have been shown
                                    cueCounter++;
                                    if (cueCounter == cues.Count) {
                                        afterWait = TaskStates.EndText;

                                    } else {

                                        // set new cue and target, and reset cue-dependent variables
                                        view.setCueText(cues[cueCounter]);
                                        currentTargetIndex = 0;
                                        currentTarget = cues[cueCounter].Substring(currentTargetIndex, 1);
                                        wordSpelled = false;
                                        correctClicks = 0;
                                        totalClicks = 0;
                                        backSpacesNeeded = 0;

                                        // start again
                                        afterWait = TaskStates.RowSelect;

                                    }

                                    // wait the interstimulus interval before presenting next stimulus
                                    waitCounter = isi;
                                    setState(TaskStates.Wait);

                                } else {
                                    setState(TaskStates.RowSelect);

                                }

                                // debug
                                logger.Debug("Clicked on cell type: " + activeCell.cellType);
                                logger.Debug("New target: " + currentTarget + " at index: " + currentTargetIndex);

                                // if we are using questions as cues
                            } else {

                                // check whether input, backspace, or exit was clicked; if empty or backspace, do nothing, otherwise either exit or store input and proceed to next question
                                if (activeCell.cellType == SpellerCell.CellType.Input) {

                                    // update view
                                    view.updateInputText(activeCell.content, false);
                                    
                                    // log event question answered
                                    Data.logEvent(2, "QuestionAnswered", cues[cueCounter] + ";" + activeCell.content);

                                    // add one to the score and display
                                    score++;
                                    view.setScore(score);

                                    // reset input text
                                    view.resetInputText();

                                    // update cue counter, if all cues have been shown, show end text
                                    cueCounter++;
                                    if (cueCounter == cues.Count) {
                                        setState(TaskStates.EndText);

                                    } else {

                                        // set new cue and wait the interstimulus interval before presenting next cue
                                        view.setCueText(cues[cueCounter]);
                                        waitCounter = isi;
                                        afterWait = TaskStates.RowSelect;
                                        setState(TaskStates.Wait);

                                    }

                                } else if (activeCell.cellType == SpellerCell.CellType.Exit && allowExit) {

                                    // log event task is stopped
                                    Data.logEvent(2, "TaskStop", CLASS_NAME + ";user");

                                    // stop the task
                                    // this will also call stop(), and as a result stopTask()
                                    if (unpMenuTask)        UNP_stop();
                                    else                    MainThread.stop(false);

                                }

                                // debug
                                logger.Debug("Clicked on cell type: " + activeCell.cellType);
                            }
                            
                        } else waitCounter--;

			            break;

		            case TaskStates.EndText:

			            if (waitCounter == 0) {

                            // don't show text
                            view.setCueText("");

                            // log event task is stopped
                            Data.logEvent(2, "TaskStop", CLASS_NAME + ";end");

                            // stop the run
                            // this will also call stop(), and as a result stopTask()
                            if (unpMenuTask)        UNP_stop();
                            else                    MainThread.stop(false);

                        } else  waitCounter--;

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

            //
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
            view.gridVisible(false);
            view.setScore(-1);
        }

        // resume the task
        private void resumeTask() {

            //
            if (view == null)   return;

            // log event task is resumed
            Data.logEvent(2, "TaskResume", CLASS_NAME);

            // show the grid and the score
            if (previousTaskState == TaskStates.RowSelect || previousTaskState == TaskStates.RowSelected || previousTaskState == TaskStates.ColumnSelect || previousTaskState == TaskStates.ColumnSelected) {
			
			    // show the grid
			    view.gridVisible(true);

			    // show the score
			    view.setScore(score);
		    }
	    
	        // set the previous gamestate
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

                case TaskStates.RowSelect:

                    // show cue, score and grid
                    view.setScore(score);
                    view.gridVisible(true);
                    view.setCueText(cues[cueCounter]);

                    // reset the row and columns positions
                    rowID = 0;
			        columnID = 0;

			        // select row
			        view.selectRow(rowID, false);

                    // check whether selected row contains target and throw event, checking for targets if used cues are words
                    if (cueType == 0) {

                        Data.logEvent(2, targetInRow(rowID) ? "TargetRow" : "NonTargetRow", rowID.ToString());

                    } else {

                        Data.logEvent(2, "Row", rowID.ToString());

                    }

                    // 
                    waitCounter = rowSelectDelay;

			        break;

                // row was selected 
                case TaskStates.RowSelected:

                    // highlight row
                    view.selectRow(rowID, true);

                    // if we are using words as cues, check if target is in current row and throw event accordingly, if using questions as cues (no target), throw generic event
                    if (cueType == 0) {

                        // if the incorrect row is selected and the setting is to return to the top row, set state to selecting rows again, otherwise, start selecting cells in this row
                        Data.logEvent(2, "RowClick", targetInRow(rowID) ? "1" : "0");

                    } else {

                        Data.logEvent(2, "RowClick", "");

                    }

                    // set waitcounter
                    waitCounter = rowSelectedDelay;

			        break;

                // highlighting columns
                case TaskStates.ColumnSelect:
	
			        // reset the column position
			        columnID = 0;

                    // get selected cell
                    SpellerCell activeCell = holes[holeColumns * rowID + columnID];

                    // log event that column is highlighted, and the type and content of the cell
                    Data.logEvent(2, activeCell.cellType.ToString() + "Column", columnID.ToString() + ";" + activeCell.content);

                    // select cell
                    view.selectCell(rowID, columnID, false);

                    // reset how often there was looped in this row
                    rowLoopCounter = 0;

                    // 
			        waitCounter = columnSelectDelay;

			        break;

                // cell is selected
                case TaskStates.ColumnSelected:

                    // get selected cell
                    SpellerCell selectedCell = holes[holeColumns * rowID + columnID];

                    // log event that column is selected, checking for targets if used cues are words
                    if (cueType == 0) {
                        if (selectedCell.content == currentTarget)  Data.logEvent(2, "CellClick" + selectedCell.cellType.ToString(), "1");
                        else                                        Data.logEvent(2, "CellClick" + selectedCell.cellType.ToString(), "0");
                    } else                                          Data.logEvent(2, "CellClick" + selectedCell.cellType.ToString(), "");

                    // select cell and highlight
                    view.selectCell(rowID, columnID, true);

                    // 
			        waitCounter = columnSelectedDelay;

			        break;

                case TaskStates.EndText:

                    // remove cue and countdown
                    view.setCountDown(-1);
                    view.setCueText("");
			
			        // hide hole grid
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


        // update the current target, based on the user input
        private void updateTarget(string input) {

            // get current cue
            string cue = cues[cueCounter];

            // if correctly selected current target, select next element in cue as target, if we are not at end of cue.
            if (input == currentTarget) {

                // store that correct input was made
                correctClicks++;

                // if a backspace is correctly selected, decrease the amount of neede backspaces if an input other than backspace is correctly provided, increase currentIndexTarget
                if (input == backspaceCode)     backSpacesNeeded--;  
                else                            currentTargetIndex++;

                // if no more backspaces are needed, determine next target based on cue, otherwise new target is backspace if the current input was not a backspace
                if (backSpacesNeeded == 0) {

                    if (currentTargetIndex == cue.Length) {
                        wordSpelled = true;                                         // if reached end of cue, word is spelled
                        currentTarget = null;                                       // set target to null

                    } else {
                        currentTarget = cue.Substring(currentTargetIndex, 1);       // if not at end of cue, select next target in cue

                    }

                } else {
                    currentTarget = backspaceCode;

                }

            } else if (backspacePresent & input != backspaceCode) {
                currentTarget = backspaceCode;                              // if incorrectly selected, if a backspace is avaialable for the user, this becomes the next target. If no backspace is available, do nothing, target stays the same. 
                backSpacesNeeded++;                                         // increase the amount of backspaces needed

            }
        }


        // check if target is in current row
        private bool targetInRow(int rowID) {
            
            bool inRow = false;
            for (int cellId = holeColumns * rowID; cellId < (holeColumns * (rowID + 1)); cellId++) {
                string cellContent = holes[cellId].content;
                if (cellContent == currentTarget) inRow = true;
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
        //  UNP entry points (start, process, stop)
        ////////////////////////////////////////////////

        public void UNP_start(Parameters parentParameters) {
            
            // UNP entry point can only be used if initialized as UNPMenu
            if (!unpMenuTask) {
                logger.Error("Using UNP entry point while the task was not initialized as UNPMenu task, check parameters used to call the task constructor");
                return;
            }


            // create a new parameter object and define this task's parameters
            Parameters newParameters = new Parameters("FollowTask", Parameters.ParamSetTypes.Application);
            defineParameters(ref newParameters);

            // transfer some parameters from the parent
            newParameters.setValue("WindowRedrawFreqMax", parentParameters.getValue<int>("WindowRedrawFreqMax"));
            newParameters.setValue("WindowWidth", parentParameters.getValue<int>("WindowWidth"));
            newParameters.setValue("WindowHeight", parentParameters.getValue<int>("WindowHeight"));
            newParameters.setValue("WindowLeft", parentParameters.getValue<int>("WindowLeft"));
            newParameters.setValue("WindowTop", parentParameters.getValue<int>("WindowTop"));

            // set UNP task standard settings
            inputChannels = 1;
            allowExit = true;                  // UNPMenu task, allow exit
            newParameters.setValue("WindowBackgroundColor", "0;0;0");
            newParameters.setValue("CountdownTime", "3s");
            newParameters.setValue("TaskInputChannel", 1);
            newParameters.setValue("TaskFirstRunStartDelay", "2s");
            newParameters.setValue("TaskStartDelay", "2s");
            newParameters.setValue("HoleRows", 4);
            newParameters.setValue("HoleColumns", 4);
            newParameters.setValue("RowSelectDelay", 12.0);
            newParameters.setValue("RowSelectedDelay", 5.0);
            newParameters.setValue("ColumnSelectDelay", 12.0);
            newParameters.setValue("ColumnSelectedDelay", 5.0);
            newParameters.setValue("NumberTargets", 10);
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
                
		        // process the input (if the task is not suspended)
		        if (!unpMenuTaskSuspended)		process(input);

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
            pauseTask();

            // lock for thread safety and destroy the scene
            lock (lockView) {
                destroyView();
            }

        }

    }

}