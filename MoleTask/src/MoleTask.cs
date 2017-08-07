using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

using UNP.Applications;
using UNP.Core;
using UNP.Core.Helpers;
using UNP.Core.Params;

namespace MoleTask {

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

        private const int CLASS_VERSION = 0;
        private const string CLASS_NAME = "MoleTask";

        private static Logger logger = LogManager.GetLogger(CLASS_NAME);                        // the logger object for the view
        private static Parameters parameters = null;
        
        private int inputChannels = 0;
        private MoleView mSceneThread = null;

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
        private int[] fixedTargetSequence = new int[0];					            // target sequence (input parameter)


        // task (active) variables
        private List<MoleCell> holes = new List<MoleCell>(0);
        private TaskStates taskState = TaskStates.Wait;
        private TaskStates previousTaskState = TaskStates.Wait;

        private bool mAllowExit = false;
        private int holeRows = 0;
        private int holeColumns = 0;
        private int mRowID = 0;
        private int mColumnID = 0;
        private int numTargets = 1;
        private List<int> mTargetSequence = new List<int>(0);		// the target sequence being used in the task (can either be given by input or generated)
        private int mMoleIndex = -1;							        // specify the position of the mole (grid index)
        private int mTargetIndex = 0;						        // specify the position in the random sequence of targets
        private int mCountdownCounter = 0;					        // the countdown timer
        private int score = 0;						            // the score of the user hitting a mole
        private int mRowLoopCounter = 0;


        public MoleTask() : this(false) { }
        public MoleTask(bool UNPMenuTask) {

            // transfer the UNP menu task flag
            mUNPMenuTask = UNPMenuTask;
            
            // check if the task is standalone (not unp menu)
            if (!mUNPMenuTask) {

                // create a parameter set for the task
                parameters = ParameterManager.GetParameters("MoleTask", Parameters.ParamSetTypes.Application);

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
                    "NumberTargets",
                    "Number of targets",
                    "1", "", "10");

                parameters.addParameter<int[]>(
                    "TargetSequence",
                    "Fixed sequence in which targets should be presented (leave empty for random). \nNote. the 'NumberTargets' parameter will be overwritten with the amount of values entered here",
                    "0", "", "");

            }

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

            // retrieve the countdown time
            mCountdownTime = parameters.getValueInSamples("CountdownTime");
            if (mCountdownTime < 0) {
                logger.Error("Countdown time cannot be less than 0");
                return false;
            }

            // retrieve the number of rows and columns in the grid
            configHoleRows = parameters.getValue<int>("HoleRows");
            configHoleColumns = parameters.getValue<int>("HoleColumns");
            if (configHoleColumns > 30 || configHoleColumns <= 0 || configHoleRows > 30 || configHoleRows <= 0) {
                logger.Error("The number of columns or rows cannot exceed 30 or be below 1");
                return false;
            }

            // retrieve selection delays
            mRowSelectDelay = parameters.getValueInSamples("RowSelectDelay");
            mRowSelectedDelay = parameters.getValueInSamples("RowSelectedDelay");
            mColumnSelectDelay = parameters.getValueInSamples("ColumnSelectDelay");
            mColumnSelectedDelay = parameters.getValueInSamples("ColumnSelectedDelay");
            if (mRowSelectDelay < 1 || mRowSelectedDelay < 1 || mColumnSelectDelay < 1 || mColumnSelectedDelay < 1) {
                logger.Error("The 'RowSelectDelay', 'RowSelectedDelay', 'ColumnSelectDelay', 'ColumnSelectedDelay' parameters should not be less than 1");
                return false;
            }

            // retrieve the number of targets
            numTargets = parameters.getValue<int>("NumberTargets");
            if (numTargets < 1) {
                logger.Error("Minimum of 1 target is required");
                return false;
            }

            // retrieve (fixed) target sequence
            fixedTargetSequence = parameters.getValue<int[]>("TargetSequence");
            if (fixedTargetSequence.Length > 0) {
                int numHoles = configHoleRows * configHoleColumns;
                for (int i = 0; i < fixedTargetSequence.Length; ++i) {
                    if (fixedTargetSequence[i] < 0) {
                        logger.Error("The TargetSequence parameter contains a target index (" + fixedTargetSequence[i] + ") that is below zero, check the TargetSequence");
                        return false;
                    }
                    if (fixedTargetSequence[i] >= numHoles) {
                        logger.Error("The TargetSequence parameter contains a target index (" + fixedTargetSequence[i] + ") that is out of range, check the HoleRows and HoleColumns parameters. (note that the indexing is 0 based)");
                        return false;
                    }
                    // TODO: check if the mole is not on an empty spot
                }
            }

            // configured as stand-alone, disallow exit
            mAllowExit = false;

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
                    if ((i % holeColumns == 0 || i <= holeColumns) && (i != 2 || !mAllowExit))
                        holes.Add(new MoleCell(0, 0, 0, 0, MoleCell.CellType.Empty));
                    else if (i == 2 && mAllowExit)
                        holes.Add(new MoleCell(0, 0, 0, 0, MoleCell.CellType.Exit));
                    else
                        holes.Add(new MoleCell(0, 0, 0, 0, MoleCell.CellType.Hole));
                }

                // check the scene (thread) already exists, stop and clear the old one.
                destroyScene();

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
		            if (mTargetSequence.Count() != 0)		mTargetSequence.Clear();
                
		            // transfer the targetsequence
                    mTargetSequence = new List<int>(fixedTargetSequence);

	            }
	            

            }
        }

        private void initializeView() {

            // create the view
            mSceneThread = new MoleView(mWindowRedrawFreqMax, mWindowLeft, mWindowTop, mWindowWidth, mWindowHeight, false);
            mSceneThread.setBackgroundColor(mWindowBackgroundColor.getRed(), mWindowBackgroundColor.getGreen(), mWindowBackgroundColor.getBlue());

            // set task specific display attributes 
            mSceneThread.setFixation(false);                                            // hide the fixation
            mSceneThread.setCountDown(-1);                                              // hide the countdown

            // initialize the holes for the scene
            mSceneThread.initGridPositions(holes, holeRows, holeColumns, 10);

            // start the scene thread
            mSceneThread.start();

            // wait till the resources are loaded or a maximum amount of 30 seconds (30.000 / 50 = 600)
            // (resourcesLoaded also includes whether GL is loaded)
            int waitCounter = 600;
            while (!mSceneThread.resourcesLoaded() && waitCounter > 0) {
                Thread.Sleep(50);
                waitCounter--;
            }

        }

        public void start() {

            // lock for thread safety
            lock(lockView) {

                if (mSceneThread == null)   return;

	            // reset the score
	            score = 0;

	            // reset countdown to the countdown time
	            mCountdownCounter = mCountdownTime;

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
			            if (mCountdownCounter > 0) {
                            // still counting down

                            // display the countdown
                            mSceneThread.setCountDown((int)Math.Floor((mCountdownCounter - 1) / MainThread.SamplesPerSecond()) + 1);

                            // reduce the countdown timer
                            mCountdownCounter--;

                        } else {
				            // done counting down

				            // hide the countdown counter
				            mSceneThread.setCountDown(-1);

				            // Begin at first target and set the mole at the right position
				            mTargetIndex = 0;
				            setMole(mTargetSequence[mTargetIndex]);

				            // Show hole grid
				            mSceneThread.showGrid(true);
                            mSceneThread.setScore(score);
			
				            // begin task
				            setState(TaskStates.RowSelect);
			            }

			            break;

		            case TaskStates.RowSelect:
			            // selecting a row

			            // if (click && mRowID != 0) {	// Click is not at the blank row

			            if (click) {
				            // click
			
				            setState(TaskStates.RowSelected);

			            } else {
				            // no click

				            if(mWaitCounter == 0) {

					            // Advance to next row and wrap around
					            mRowID++;
					            if(mRowID >= holeRows)		mRowID = 0;

					            // select the row in the scene
					            mSceneThread.selectRow(mRowID, false);

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
				            if(mRowID == (int)Math.Floor(((double)mMoleIndex / holeColumns)) || ( mRowID == 0 && mAllowExit))
					            setState(TaskStates.ColumnSelect);
				            else
					            setState(TaskStates.RowSelect);
			            } else
				            mWaitCounter--;

			            break;
		
		            case TaskStates.ColumnSelect:
			            // selecting a column

			            // if (click && mColumnID != 0) {		// Click is not at the blank column

			            if (click) {
				            // click
			
				            setState(TaskStates.ColumnSelected);


			            } else {
				            // no click

				            if(mWaitCounter == 0) {

					            // Advance to next row and wrap around
					            mColumnID++;
					            if(mColumnID >= holeColumns) {
						            mColumnID = 0;

						            // increase how often we have looped through row
						            mRowLoopCounter++;
					            }

					            // check if there has been looped more than two times in the row with exit button
					            if (mRowLoopCounter > 1 && mRowID == 0) {
						
						            // start from the top
						            setState(TaskStates.RowSelect);

					            } else {
						            // select the cell in the scene
						            mSceneThread.selectCell(mRowID, mColumnID, false);
						
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
                            if (mAllowExit && mRowID == 0 && mColumnID == 2) {
                                // exit was allowed and selected

                                // check if we are running from the UNPMenu
                                if (mUNPMenuTask) {

                                    // stop the task (UNP)
                                    UNP_stop();

                                } else {

                                    // stop the run, this will also call stopTask()
                                    MainThread.stop();

                                }

                            } else {
                                // exit was not allowed nor selected

                                // Check if mole is selected
                                if (mMoleIndex == holeColumns * mRowID + mColumnID) {
                                    // hit

                                    // add one to the score and display
                                    score++;
                                    mSceneThread.setScore(score);

                                    // go to next target in the sequence and set mole
                                    mTargetIndex++;

                                    // check whether at the end of targetsequence
                                    if (mTargetIndex == mTargetSequence.Count()) {

                                        // show end text
                                        setState(TaskStates.EndText);

                                    } else {

                                        // set mole at next location
                                        setMole(mTargetSequence[mTargetIndex]);

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
                destroyScene();
            }

            // destroy/empty more task variables


        }


        // pauzes the task
        private void pauzeTask() {
	        if (mSceneThread == null)   return;

	        // set task as pauzed
	        mTaskPauzed = true;

	        // store the previous state
	        previousTaskState = taskState;
			
		    // hide the grid and hide the score
		    mSceneThread.showGrid(false);
		    mSceneThread.setScore(-1);
	    
        }

        // resumes the task
        private void resumeTask() {
            if (mSceneThread == null)   return;
	        
		    // show the grid and set the mole
		    if (previousTaskState == TaskStates.RowSelect || previousTaskState == TaskStates.RowSelected || previousTaskState == TaskStates.ColumnSelect || previousTaskState == TaskStates.ColumnSelected) {
			
			    // show the grid and set the mole
			    mSceneThread.showGrid(true);
			    setMole(mTargetSequence[mTargetIndex]);

			    // show the score
			    mSceneThread.setScore(score);

		    }
	    
	        // set the previous gamestate
	        setState(previousTaskState);

	        // set task as not longer pauzed
	        mTaskPauzed = false;

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
                    mSceneThread.setCountDown(-1);

			        // Hide countdown, selection, mole and score
                    mSceneThread.selectRow(-1, false);
			        setMole(-1);
                    mSceneThread.setScore(-1);
                    mSceneThread.showGrid(false);

                    // Set wait counter to startdelay
                    if (mTaskFirstRunStartDelay != 0) {
                        mWaitCounter = mTaskFirstRunStartDelay;
                        mTaskFirstRunStartDelay = 0;
                    } else
			            mWaitCounter = mTaskStartDelay;

			        break;

                case TaskStates.CountDown:
			        // countdown when task starts
			        
			        // hide fixation
			        mSceneThread.setFixation(false);

                    // set countdown
                    if (mCountdownCounter > 0)
                        mSceneThread.setCountDown((int)Math.Floor((mCountdownCounter - 1) / MainThread.SamplesPerSecond()) + 1);
                    else
                        mSceneThread.setCountDown(-1);

                    break;

                case TaskStates.RowSelect:
			        // selecting a row

			        // reset the row and columns positions
			        mRowID = 0;
			        mColumnID = 0;

			        // select row
			        mSceneThread.selectRow(mRowID, false);
			
                    // 
			        mWaitCounter = mRowSelectDelay;

			        break;


                case TaskStates.RowSelected:
			        // row was selected (highlighted)

			        // select row and highlight
			        mSceneThread.selectRow(mRowID, true);

                    // 
			        mWaitCounter = mRowSelectedDelay;

			        break;

                case TaskStates.ColumnSelect:
			        // selecting a column
			
			        // reset the column position
			        mColumnID = 0;

			        // select cell
			        mSceneThread.selectCell(mRowID, mColumnID, false);

			        // reset how often there was looped in this row
			        mRowLoopCounter = 0;

                    // 
			        mWaitCounter = mColumnSelectDelay;

			        break;


                case TaskStates.ColumnSelected:
			        // column was selected

			        // select cell and highlight
			        mSceneThread.selectCell(mRowID, mColumnID, true);

                    // 
			        mWaitCounter = mColumnSelectedDelay;

			        break;

                case TaskStates.EndText:
			        // show text
			
			        // hide hole grid
			        mSceneThread.showGrid(false);

			        // show text
				    mSceneThread.setText("Done");

                    // 
			        mWaitCounter = 15;

			        break;

	        }

        }

        // Stop the task
        private void stopTask() {
            if (mSceneThread == null) return;

	        // Set state to Wait
	        setState(TaskStates.Wait);

            // check if there is no fixed target sequence
	        if (fixedTargetSequence.Length == 0) {

		        // generate new targetlist
		        generateTargetSequence();

	        }

        }

        private void generateTargetSequence() {
		
	        // clear the targets
	        if (mTargetSequence.Count != 0)		mTargetSequence.Clear();

            // create targetsequence array with <numTargets>
            mTargetSequence = new List<int>(new int[numTargets]);

	        // create a random sequence
	        int i = 0;
	        List<int> numberSet = new List<int>(0); 
	        while(i < numTargets) {

		        // check if the numberset is empty
		        if (numberSet.Count == 0) {

			        // recreate the numberset with possible positions
                    numberSet = new List<int>(new int[((holeRows-1) * (holeColumns-1))]);
			        for (int j = 0; j < numberSet.Count; j++)
				        numberSet[j] = holeColumns + (int)Math.Floor((double)(j / (holeColumns - 1)) * ( (holeColumns - 1) + 1 ) + 1 + (j % (holeColumns - 1)));
                    
			        // shuffle the set (and, if needed, reshuffle until the conditions are met, which are: not on the same spot, ...)
                    numberSet.Shuffle();
			        while(i > 0 && numberSet.Count != 1 && mTargetSequence[i - 1] == numberSet[(numberSet.Count - 1)])
				        numberSet.Shuffle();

		        }

		        // use the last number in the numberset and remove the last number from the numberset
		        mTargetSequence[i] = numberSet[(numberSet.Count - 1)];
                numberSet.RemoveAt(numberSet.Count - 1);

		        // go to the next position
		        i++;

	        }

        }

        private void setMole(int index) {
	
	        // set mole index to variable
	        mMoleIndex = index;

	        // hide moles
	        for(int i = 0; i < holes.Count; i++) {
		        if (holes[i].mType == MoleCell.CellType.Mole)
			        holes[i].mType = MoleCell.CellType.Hole;
	        }

	        // set mole (if not -1)
	        if(mMoleIndex != -1)
		        holes[mMoleIndex].mType = MoleCell.CellType.Mole;

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

            // transfer the window settings
            mWindowRedrawFreqMax = parentParameters.getValue<int>("WindowRedrawFreqMax");      // the view update frequency (in maximum fps)
            //mWindowed = true;
            mWindowWidth = parentParameters.getValue<int>("WindowWidth");;
            mWindowHeight = parentParameters.getValue<int>("WindowHeight");;
            mWindowLeft = parentParameters.getValue<int>("WindowLeft");;
            mWindowTop = parentParameters.getValue<int>("WindowTop");;
            //mFullscreenMonitor = 0;

            // set the UNP task standard settings
            mTaskInputChannel = 1;
            mTaskFirstRunStartDelay = 4;
            mTaskStartDelay = 4;
            mRowSelectDelay = 12;
            mRowSelectedDelay = 5;
            mColumnSelectDelay = 12;
            mColumnSelectedDelay = 5;
            configHoleRows = 4;
            configHoleColumns = 4;
            numTargets = 10;

            // UNPMenu task, allow exit
            mAllowExit = true;

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
                
		        // process the input (if the task is not suspended)
		        if (!mUNPMenuTaskSuspended)		process(input);

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
                destroyScene();
            }

        }

    }

}
