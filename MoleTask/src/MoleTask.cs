using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using UNP;
using UNP.Applications;
using UNP.Core.Helpers;
using UNP.Core.Params;

namespace MoleTask {

    public class MoleTask : IApplication {

		private enum TaskStates:int {
			Wait,
			CountDown,
			RowSelect,
			RowSelected,
			ColumnSelect,
			ColumnSelected,
			EndText
		};

        private static Logger logger = LogManager.GetLogger("MoleTask");                        // the logger object for the view
        private static Parameters parameters = ParameterManager.GetParameters("MoleTask", Parameters.ParamSetTypes.Application);
        
        Random rand = new Random(Guid.NewGuid().GetHashCode());

        private MoleView mSceneThread = null;
        private Object lockView = new Object();                         // threadsafety lock for all event on the view
        private bool mTaskPauzed = false;								// flag to hold whether the task is pauzed (view will remain active, e.g. connection lost)

        private bool mUNPMenuTask = false;								// flag whether the task is started by the UNPMenu
        private bool mUNPMenuTaskStop = false;							// flag to hold whether the task should is stop (setting this to false will notify the UNPMenu that the task should stop)
        private bool mUNPMenuTaskSuspended = false;						// flag to hold whether the task is suspended (view will be destroyed/re-initiated)

        private int mConnectionSoundTimer = 0;							// counter to play a sound when the connection is lost
        private bool mConnectionLost = false;							// flag to hold whether the connection is lost
        private bool mConnectionWasLost = false;						// flag to hold whether the connection has been lost (should be reset after being re-connected)


        // task input parameters
        private int mWindowRedrawFreqMax = 0;
        private bool mWindowed = false;
        private int mWindowWidth = 0;
        private int mWindowHeight = 0;
        private int mWindowLeft = 0;
        private int mWindowTop = 0;
        private int mFullscreenMonitor = 0;

        private int mTaskInputChannel = 0;											// input channel
        private int mTaskFirstRunStartDelay = 0;                                    // the first run start delay in sample blocks
        private int mTaskStartDelay = 0;                                            // the run start delay in sample blocks
        private int mWaitCounter = 0;
        private int mRowSelectDelay = 0;
        private int mRowSelectedDelay = 0;
        private int mColumnSelectDelay = 0;
        private int mColumnSelectedDelay = 0;
        private int configHoleRows = 0;
        private int configHoleColumns = 0;

        private List<int> fixedTargetSequence = new List<int>(0);					// the target sequence (input parameter)


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



        public MoleTask() {

            // define the parameters
            //Parameters.addParameter("Test", "0", "1", "1");


        }

        public bool configure(ref SampleFormat input) {


            // debug, now using UNPMenu settings


            // set the task as being standalone
            mUNPMenuTask = false;

            // transfer the window settings
            mWindowRedrawFreqMax = 60;
            mWindowed = true;
            mWindowWidth = 800;
            mWindowHeight = 600;
            mWindowLeft = 0;
            mWindowTop = 0;
            mFullscreenMonitor = 0;

            // set the UNP task standard settings
            mTaskInputChannel = 1;
            mTaskFirstRunStartDelay = 4;
            mTaskStartDelay = 4;
            mRowSelectDelay = 12;
            mRowSelectedDelay = 4;
            mColumnSelectDelay = 12;
            mColumnSelectedDelay = 4;
            configHoleRows = 4;
            configHoleColumns = 4;

            mAllowExit = false;


            return true;
        }

        public void initialize() {
                                
            // lock for thread safety
            lock(lockView) {

                // check the scene (thread) already exists, stop and clear the old one.
                destroyScene();

                // create the view
                mSceneThread = new MoleView(mWindowRedrawFreqMax, mWindowLeft, mWindowTop, mWindowWidth, mWindowHeight, false);

                // set the scene background color
                //RGBColor backgroundColor = RGBColor(Parameter("WindowBackgroundColor"));
                // TODO: set background color


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

                // initialize the holes for the scene
                mSceneThread.initGridPositions(holes, holeRows, holeColumns, 10);	

	            // start the scene thread
	            mSceneThread.start();

	            // wait till the resources are loaded or a maximum amount of 30 seconds (30.000 / 50 = 600)
	            int waitCounter = 600;
	            while (!mSceneThread.resourcesLoaded() && waitCounter > 0) {
		            Thread.Sleep(50);
		            waitCounter--;
	            }

                // check if a target sequence is set
	            if (fixedTargetSequence.Count == 0) {
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

        public void start() {

            // lock for thread safety
            lock(lockView) {

                if (mSceneThread == null)   return;

	            // reset the score
	            score = 0;

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

            /*
            mConnectionLost = (State("ConnectionLost") == 1);
            */

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

				            // reduce the countdown timer
				            mCountdownCounter--;
				
					        if (mCountdownCounter >10)			mSceneThread.setCountDown(3);
					        else if (mCountdownCounter > 5)		mSceneThread.setCountDown(2);
					        else if (mCountdownCounter > 0)		mSceneThread.setCountDown(1);

			            } else {
				            // done counting down

				            // hide the countdown counter
				            mSceneThread.setCountDown(0);

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
				            if (mRowID == 0 && mColumnID == 2 && mAllowExit) {
					            // check if the task is run from the UNPMenu

                                /*
					            #ifdef UNPMENU
						
						            if (mUNPMenuTask)	UNP_Stop();

					            #else

						            // suspend BCI2000, this will also call stopTask()
						            if (!mUNPMenuTask)	State( "Running" ) = false;

					            #endif
                                */

				            }


				            // Check if mole is selected
				            if ( mMoleIndex == holeColumns * mRowID + mColumnID) {
					            // hit

					            // add one to the score and display
					            score++;
					            mSceneThread.setScore(score);

					            // go to next target in the sequence and set mole
					            mTargetIndex++;

					            // check whether at the end of targetsequence
					            if(mTargetIndex == mTargetSequence.Count()) {

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

			            } else
				            mWaitCounter--;

			            break;

		            case TaskStates.EndText:
			            // end text

			            if (mWaitCounter == 0) {
				            
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

            // lock for thread safety
            lock(lockView) {

                destroyScene();

            }

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
                    mSceneThread.setCountDown(0);

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
			        if (mCountdownCounter > 10)			mSceneThread.setCountDown(3);
			        else if (mCountdownCounter > 5)		mSceneThread.setCountDown(2);
			        else if (mCountdownCounter > 0)		mSceneThread.setCountDown(1);
			        else								mSceneThread.setCountDown(0);

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

	        if (fixedTargetSequence.Count == 0) {

		        // Generate targetlist
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


    }
}
