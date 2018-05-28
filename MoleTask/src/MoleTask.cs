/**
 * The MoleTask class
 * 
 * ...
 * 
 * 
 * Copyright (C) 2017:  RIBS group (Nick Ramsey Lab), University Medical Center Utrecht (The Netherlands) & external contributors
 * Concept:             UNP Team                    (neuroprothese@umcutrecht.nl)
 * Author(s):           Max van den Boom            (info@maxvandenboom.nl)
 * 
 * Adapted from:        Meron Vermaas              (m.vermaas-2@umcutrecht.nl)
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

namespace MoleTask {

    /// <summary>
    /// The <c>MoleTask</c> class.
    /// 
    /// ...
    /// </summary>
    public class MoleTask : IApplication, IApplicationUNP {

		private enum TaskStates:int {
			Wait,
			CountDown,
			RowSelect,
			RowSelected,
			ColumnSelect,
			ColumnSelected,
			EndText
		};

        private const int CLASS_VERSION = 2;
        private const string CLASS_NAME = "MoleTask";
        private const string CONNECTION_LOST_SOUND = "sounds\\connectionLost.wav";

        private static Logger logger = LogManager.GetLogger(CLASS_NAME);            // the logger object for the view
        private static Parameters parameters = null;
        
        private int inputChannels = 0;
        private MoleView view = null;

        private Random rand = new Random(Guid.NewGuid().GetHashCode());
        private Object lockView = new Object();                                     // threadsafety lock for all event on the view
        private bool taskPauzed = false;								            // flag to hold whether the task is pauzed (view will remain active, e.g. connection lost)

        private bool unpMenuTask = false;								            // flag whether the task is created by the UNPMenu
        private bool unpMenuTaskRunning = false;						            // flag to hold whether the task should is running (setting this to false is also used to notify the UNPMenu that the task is finished)
        private bool umpMenuTaskSuspended = false;						            // flag to hold whether the task is suspended (view will be destroyed/re-initiated)

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
        

        private int mTaskInputChannel = 1;											// input channel
        private int mTaskFirstRunStartDelay = 0;                                    // the first run start delay in sample blocks
        private int mTaskStartDelay = 0;                                            // the run start delay in sample blocks
        private int mCountdownTime = 0;                                             // the time the countdown takes in sample blocks

        private int mWaitCounter = 0;
        private int mRowSelectDelay = 0;
        private int mRowSelectedDelay = 0;
        private int mColumnSelectDelay = 0;
        private int mColumnSelectedDelay = 0;
        private int configHoleRows = 0;
        private int configHoleColumns = 0;
        private int[] fixedTrialSequence = new int[0];					            // target sequence (input parameter)


        // task (active) variables
        private List<MoleCell> holes = new List<MoleCell>(0);
        private TaskStates taskState = TaskStates.Wait;
        private TaskStates previousTaskState = TaskStates.Wait;

        private bool allowExit = false;
        private int holeRows = 0;
        private int holeColumns = 0;
        private int currentRowID = 0;
        private int currentColumnID = 0;
        private int numberOfTrials = 1;
        private List<int> trialSequence = new List<int>(0);		                    // the trial sequence being used in the task (can either be given by input or generated)
        private int currentMoleIndex = -1;							                // specify the position of the mole (grid index)
        private int currentTrialIndex = 0;						                    // specify the position in the random sequence of trials
        private int countdownCounter = 0;					                        // the countdown timer
        private int score = 0;						                                // the score of the user hitting a mole
        private int rowLoopCounter = 0;


        public MoleTask() : this(false) { }
        public MoleTask(bool UNPMenuTask) {

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
                "HoleRows",
                "Number of rows in the whack a mole grid",
                "1", "30", "6");

            parameters.addParameter<int>(
                "HoleColumns",
                "Number of columns in the whack a mole grid",
                "1", "30", "8");

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

            parameters.addParameter<int>(
                "NumberOfTrials",
                "Number of trials",
                "1", "", "10");

            parameters.addParameter<int[]>(
                "TrialSequence",
                "Fixed sequence in which targets should be presented (leave empty for random). \nNote. the 'NumberOfTrials' parameter will be overwritten with the amount of values entered here",
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

            // retrieve the number of rows and columns in the grid
            configHoleRows = newParameters.getValue<int>("HoleRows");
            configHoleColumns = newParameters.getValue<int>("HoleColumns");
            if (configHoleColumns > 30 || configHoleColumns <= 0 || configHoleRows > 30 || configHoleRows <= 0) {
                logger.Error("The number of columns or rows cannot exceed 30 or be below 1");
                return false;
            }

            // retrieve selection delays
            mRowSelectDelay = newParameters.getValueInSamples("RowSelectDelay");
            mRowSelectedDelay = newParameters.getValueInSamples("RowSelectedDelay");
            mColumnSelectDelay = newParameters.getValueInSamples("ColumnSelectDelay");
            mColumnSelectedDelay = newParameters.getValueInSamples("ColumnSelectedDelay");
            if (mRowSelectDelay < 1 || mRowSelectedDelay < 1 || mColumnSelectDelay < 1 || mColumnSelectedDelay < 1) {
                logger.Error("The 'RowSelectDelay', 'RowSelectedDelay', 'ColumnSelectDelay', 'ColumnSelectedDelay' parameters should not be less than 1");
                return false;
            }

            // retrieve the number of targets
            numberOfTrials = newParameters.getValue<int>("NumberOfTrials");
            if (numberOfTrials < 1) {
                logger.Error("Minimum of 1 target is required");
                return false;
            }

            // retrieve (fixed) trial sequence
            fixedTrialSequence = newParameters.getValue<int[]>("TrialSequence");
            if (fixedTrialSequence.Length > 0) {
                int numHoles = configHoleRows * configHoleColumns;
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

            // return success
            return true;

        }

        public void initialize() {
                                
            // lock for thread safety
            lock(lockView) {

                // extra empty first column and row
                holeColumns = configHoleColumns + 1;
                holeRows = configHoleRows + 1;

                // calculate the cell holes for the task
                int numHoles = holeRows * holeColumns;

                // create the array of cells for the task
                holes = new List<MoleCell>(0);
                for (int i = 0; i < numHoles; i++) {
                    if ((i % holeColumns == 0 || i <= holeColumns) && (i != 2 || !allowExit))
                        holes.Add(new MoleCell(0, 0, 0, 0, MoleCell.CellType.Empty));
                    else if (i == 2 && allowExit)
                        holes.Add(new MoleCell(0, 0, 0, 0, MoleCell.CellType.Exit));
                    else
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
		            if (trialSequence.Count != 0)		trialSequence.Clear();
                
		            // transfer the fixed trial sequence
                    trialSequence = new List<int>(fixedTrialSequence);

	            }
	            

            }
        }

        private void initializeView() {

            // create the view
            view = new MoleView(mWindowRedrawFreqMax, mWindowLeft, mWindowTop, mWindowWidth, mWindowHeight, false);
            view.setBackgroundColor(mWindowBackgroundColor.getRed(), mWindowBackgroundColor.getGreen(), mWindowBackgroundColor.getBlue());

            // set task specific display attributes 
            view.setFixation(false);                                            // hide the fixation
            view.setCountDown(-1);                                              // hide the countdown

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

            // check if the task is standalone (not unp menu)
            if (!unpMenuTask) {
                
                // store the generated sequence in the output parameter xml
                Data.adjustXML(CLASS_NAME, "TrialSequence", string.Join(" ", trialSequence));
                
            }

            // lock for thread safety
            lock(lockView) {

                if (view == null)   return;

                // log event task is started
                Data.logEvent(2, "TaskStart", CLASS_NAME);

                // reset the score
                score = 0;

	            // reset countdown to the countdown time
	            countdownCounter = mCountdownTime;

	            if(mTaskStartDelay != 0 || mTaskFirstRunStartDelay != 0) {
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
                


	            // check if there is a click
	            bool click = (input == 1);

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

				            // Begin at first trial and set the mole at the right position
				            currentTrialIndex = 0;
				            setMole(trialSequence[currentTrialIndex]);

				            // Show hole grid
				            view.setGrid(true);
                            view.setScore(score);

                            // log event countdown is started
                            Data.logEvent(2, "TrialStart ", CLASS_NAME);

                            // begin task
                            setState(TaskStates.RowSelect);
			            }

			            break;

                    // highlighting a row
		            case TaskStates.RowSelect:

			            if (click) {
                            // click



                            setState(TaskStates.RowSelected);



			            } else {
				            // no click

				            if(mWaitCounter == 0) {

                                

                                // Advance to next row and wrap around
                                currentRowID++;
					            if(currentRowID >= holeRows)		currentRowID = 0;

					            // select the row in the scene
					            view.selectRow(currentRowID, false);

                                // log event that row is highlighted, and whether the row is empty (no mole), blank (no mole and no pile of dirt), or contains a mole
                                if (currentRowID == 0) {
                                    Data.logEvent(2, "BlankRow ", currentRowID.ToString());

                                } else if (currentRowID * holeColumns < currentMoleIndex && (currentRowID + 1) * holeColumns > currentMoleIndex) {
                                    Data.logEvent(2, "MoleRow ", currentRowID.ToString());

                                } else {
                                    Data.logEvent(2, "EmptyRow ", currentRowID.ToString());

                                }

                                // reset the timer
                                mWaitCounter = mRowSelectDelay;

				            } else
					            mWaitCounter--;

			            }

			            break;

		            case TaskStates.RowSelected:
			            // row was selected (highlighted)

			            if(mWaitCounter == 0) {

				            // Start selecting columns from the top if it is the right row
				            // OR start selecting columns if it is the first row with exit button
				            if(currentRowID == (int)Math.Floor(((double)currentMoleIndex / holeColumns)) || ( currentRowID == 0 && allowExit))
					            setState(TaskStates.ColumnSelect);
				            else
					            setState(TaskStates.RowSelect);
			            } else
				            mWaitCounter--;

			            break;

                    // highlighting a column
                    case TaskStates.ColumnSelect:
                        
                        // if clicked
                        if (click) {
                            setState(TaskStates.ColumnSelected);

			            } else {
                            
                            // if time to highlight column has passed
                            if (mWaitCounter == 0) {

                                // Advance to next row and wrap around
                                currentColumnID++;

                                // if the end of row has been reached
					            if(currentColumnID >= holeColumns) {
                                    
                                    // reset column id
						            currentColumnID = 0;

						            // increase how often we have looped through row
						            rowLoopCounter++;
					            }

                                // log event that column is highlighted, and whether the column is empty (no mole), blank (no mole and no pile of dirt), or contains a mole
                                if (currentColumnID == 0 || currentRowID == 0) Data.logEvent(2, "BlankColumn ", currentColumnID.ToString());
                                else if (currentMoleIndex == holeColumns * currentRowID + currentColumnID) Data.logEvent(2, "MoleColumn ", currentColumnID.ToString());
                                else Data.logEvent(2, "EmptyColumn ", currentColumnID.ToString());

                                // check if there has been looped more than two times in the row with exit button
                                if (rowLoopCounter > 1 && currentRowID == 0) {
						
						            // start from the top
						            setState(TaskStates.RowSelect);

					            } else {
						            // select the cell in the scene
						            view.selectCell(currentRowID, currentColumnID, false);
						
						            // reset the timer
						            mWaitCounter = mColumnSelectDelay;
					            }

				            } else 
					            mWaitCounter--;

			            }

			            break;

		            case TaskStates.ColumnSelected:
			            // column was selected

			            if(mWaitCounter == 0) {

                            // check if exit was selected
                            if (allowExit && currentRowID == 0 && currentColumnID == 2) {
                                // exit was allowed and selected

                                // log event task is stopped
                                Data.logEvent(2, "TaskStop", CLASS_NAME + ";user");

                                // stop the task
                                // this will also call stop(), and as a result stopTask()
                                if (unpMenuTask)        UNP_stop();
                                else                    MainThread.stop(false);

                            } else {
                                // exit was not allowed nor selected

                                // Check if mole is selected
                                if (currentMoleIndex == holeColumns * currentRowID + currentColumnID) {
                                    // hit

                                    // add one to the score and display
                                    score++;
                                    view.setScore(score);

                                    // go to next trial in the sequence and set mole
                                    currentTrialIndex++;

                                    // check whether at the end of trial sequence
                                    if (currentTrialIndex == trialSequence.Count) {

                                        // show end text
                                        setState(TaskStates.EndText);

                                    } else {

                                        // set mole at next location
                                        setMole(trialSequence[currentTrialIndex]);

                                        // Start again selecting rows from the top
                                        setState(TaskStates.RowSelect);

                                    }

                                } else {
                                    // no hit

                                    // Start again selecting rows from the top
                                    setState(TaskStates.RowSelect);

                                }

                            }

			            } else
				            mWaitCounter--;

			            break;

		            case TaskStates.EndText:
			            // end text

			            if (mWaitCounter == 0) {

                            // log event task is stopped
                            Data.logEvent(2, "TaskStop", CLASS_NAME + ";end");

                            // stop the task
                            // this will also call stop(), and as a result stopTask()
                            if (unpMenuTask)        UNP_stop();
                            else                    MainThread.stop(false);

                        } else
				            mWaitCounter--;

			            break;

	            }

                /*
	            #ifndef UNPMENU
	            // log the states
	            State("Log_State").AsUnsigned() = taskState;						// save the state
	            State("Log_HitScore").AsUnsigned() = score;						// save the hitscore
	            State("Log_Row").AsUnsigned() = mRowID;								// save the rowID
	            State("Log_Column").AsUnsigned() = mColumnID;						// save the columnID
	            State("Log_MoleIndex_s").AsSigned() = mMoleIndex;					// save the mole index
	            State("Log_Input").AsUnsigned() = (click);							// save the input
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

                // destroy the view
                destroyView();

            }

            // destroy/empty more task variables


        }


        // pauzes the task
        private void pauzeTask() {
	        if (view == null)   return;

            // log event task is paused
            Data.logEvent(2, "TaskPause", CLASS_NAME);

            // set task as pauzed
            taskPauzed = true;

	        // store the previous state
	        previousTaskState = taskState;
			
            // hide everything
            view.setFixation(false);
            view.setCountDown(-1);
            view.setGrid(false);
            view.setScore(-1);

        }

        // resumes the task
        private void resumeTask() {
            if (view == null)   return;

            // log event task is resumed
            Data.logEvent(2, "TaskResume", CLASS_NAME);

            // show the grid and set the mole
            if (previousTaskState == TaskStates.RowSelect || previousTaskState == TaskStates.RowSelected || previousTaskState == TaskStates.ColumnSelect || previousTaskState == TaskStates.ColumnSelected) {
			
			    // show the grid and set the mole
			    view.setGrid(true);
			    setMole(trialSequence[currentTrialIndex]);

			    // show the score
			    view.setScore(score);

		    }
	    
	        // set the previous gamestate
	        setState(previousTaskState);

	        // set task as not longer pauzed
	        taskPauzed = false;

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
			        // starting, pauzed or waiting
                    
			        // hide text if present
			        view.setText("");

			        // hide the fixation and countdown
			        view.setFixation(false);
                    view.setCountDown(-1);

			        // Hide countdown, selection, mole and score
                    view.selectRow(-1, false);
			        setMole(-1);
                    view.setScore(-1);
                    view.setGrid(false);

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

			        // reset the row and columns positions
			        currentRowID = 0;
			        currentColumnID = 0;

			        // select row
			        view.selectRow(currentRowID, false);

                    // log event that row is highlighted, and whether the row is empty (no mole), blank (no mole and no pile of dirt), or contains a mole
                    if (currentRowID == 0) Data.logEvent(2, "BlankRow ", currentRowID.ToString());
                    else if (currentRowID * holeColumns < currentMoleIndex && (currentRowID + 1) * holeColumns > currentMoleIndex) Data.logEvent(2, "MoleRow ", currentRowID.ToString());
                    else Data.logEvent(2, "EmptyRow ", currentRowID.ToString());

                    // 
                    mWaitCounter = mRowSelectDelay;

			        break;

                // row was selected 
                case TaskStates.RowSelected:

                    // select row and highlight
                    view.selectRow(currentRowID, true);

                    // row has been clicked. Check whether it was on a row that contains a mole or not
                    if (currentRowID * holeColumns < currentMoleIndex && (currentRowID + 1) * holeColumns > currentMoleIndex) Data.logEvent(2, "RowClick ", "1");
                    else Data.logEvent(2, "RowClick ", "0");

                    // 
                    mWaitCounter = mRowSelectedDelay;

			        break;

                case TaskStates.ColumnSelect:
			        // selecting a column
			
			        // reset the column position
			        currentColumnID = 0;

			        // select cell
			        view.selectCell(currentRowID, currentColumnID, false);

                    // log event that column is highlighted, and whether the column is empty(no mole), blank(no mole and no pile of dirt), or contains a mole
                    if (currentColumnID == 0 || currentRowID == 0) Data.logEvent(2, "BlankColumn ", currentColumnID.ToString());
                    else if (currentMoleIndex == holeColumns * currentRowID + currentColumnID) Data.logEvent(2, "MoleColumn ", currentColumnID.ToString());
                    else Data.logEvent(2, "EmptyColumn ", currentColumnID.ToString());

                    // reset how often there was looped in this row
                    rowLoopCounter = 0;

                    // 
			        mWaitCounter = mColumnSelectDelay;

			        break;


                case TaskStates.ColumnSelected:
			        // column was selected

			        // select cell and highlight
			        view.selectCell(currentRowID, currentColumnID, true);

                    // log cell click event
                    if (currentMoleIndex == holeColumns * currentRowID + currentColumnID) Data.logEvent(2, "CellClick", "1");
                    else                                                Data.logEvent(2, "CellClick", "0");

                    // set wait time before advancing
                    mWaitCounter = mColumnSelectedDelay;

			        break;

                case TaskStates.EndText:
			        // show text
			
			        // hide hole grid
			        view.setGrid(false);

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

            // Set state to Wait
            setState(TaskStates.Wait);

            // check if there is no fixed target sequence
	        if (fixedTrialSequence.Length == 0) {

		        // generate new targetlist
		        generateTrialSequence();

	        }

        }

        private void generateTrialSequence() {
		
	        // clear the targets
	        if (trialSequence.Count != 0)		trialSequence.Clear();

            // create trial sequence array with <numTrials>
            trialSequence = new List<int>(new int[numberOfTrials]);

	        // create a random sequence
	        int i = 0;
	        List<int> numberSet = new List<int>(0); 
	        while(i < numberOfTrials) {

		        // check if the numberset is empty
		        if (numberSet.Count == 0) {

			        // recreate the numberset with possible positions
                    numberSet = new List<int>(new int[((holeRows-1) * (holeColumns-1))]);
			        for (int j = 0; j < numberSet.Count; j++)
				        numberSet[j] = holeColumns + (int)Math.Floor((double)(j / (holeColumns - 1)) * ( (holeColumns - 1) + 1 ) + 1 + (j % (holeColumns - 1)));
                    
			        // shuffle the set (and, if needed, reshuffle until the conditions are met, which are: not on the same spot, ...)
                    numberSet.Shuffle();
			        while(i > 0 && numberSet.Count != 1 && trialSequence[i - 1] == numberSet[(numberSet.Count - 1)])
				        numberSet.Shuffle();

		        }

		        // use the last number in the numberset and remove the last number from the numberset
		        trialSequence[i] = numberSet[(numberSet.Count - 1)];
                numberSet.RemoveAt(numberSet.Count - 1);

		        // go to the next position
		        i++;

	        }

        }

        private void setMole(int index) {
	
	        // set mole index to variable
	        currentMoleIndex = index;

	        // hide moles
	        for(int i = 0; i < holes.Count; i++) {
		        if (holes[i].type == MoleCell.CellType.Mole)
			        holes[i].type = MoleCell.CellType.Hole;
	        }

	        // set mole (if not -1)
	        if(currentMoleIndex != -1)
		        holes[currentMoleIndex].type = MoleCell.CellType.Mole;

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
            newParameters.setValue("NumberOfTrials", 10);
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
		        if (!umpMenuTaskSuspended)		process(input);

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
	        umpMenuTaskSuspended = false;

        }

        public void UNP_suspend() {

            // flag task as suspended
            umpMenuTaskSuspended = true;

            // pauze the task
            pauzeTask();

            // lock for thread safety and destroy the scene
            lock (lockView) {
                destroyView();
            }

        }

    }

}
