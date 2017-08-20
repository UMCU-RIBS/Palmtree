using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UNP.Applications;
using UNP.Core.Helpers;
using UNP.Core.Params;

namespace CursorTask {

    public class CursorTask : IApplication {

        private const int CLASS_VERSION = 0;
        private const string CLASS_NAME = "CursorTask";

        private static Logger logger = LogManager.GetLogger(CLASS_NAME);                        // the logger object for the view
        private static Parameters parameters = ParameterManager.GetParameters(CLASS_NAME, Parameters.ParamSetTypes.Application);

        private CursorView mSceneThread = null;
        private Object lockView = new Object();                         // threadsafety lock for all event on the view

        public CursorTask() {

            // define the parameters
            parameters.addParameter<bool>(
                "Test",
                "test Description",
                "1");


            /*
             
    
	"Application:Window int Windowed= 1 0 0 1 "
		" // Window or Fullscreen - fullscreen is only applied with two monitors - 0: Fullscreen, 1: Window (enumeration)",

	"Application:Window int FullscreenMonitor= 1 0 0 1 "
		" // Full screen Monitor 0: Monitor_1, 1: Monitor_2 (enumeration)",

	"Application:Window int WindowWidth= 640 640 0 % "
		" // width of application window - fullscreen and 0 will take monitor resolution -",

	"Application:Window int WindowHeight= 480 480 0 % "
		" // height of application window  - fullscreen and 0 will take monitor resolution -",

	"Application:Window int WindowLeft= 0 0 % % "
		" // screen coordinate of application window's left edge",

	"Application:Window int WindowTop= 0 0 % % "
		" // screen coordinate of application window's top edge",

	"Application:Window int WindowBackgroundColor= 0x000000 0 % % "
		" // window's background color",

	"Application:Window int WindowRedrawFreqMax= 0 0 % % "
		" // Maximum display redraw interval in FPS - 0 for as fast as possible -",

    "Application:Task int TrialTime= 4s 0s 0s 20s "
      " // Time per trial to hit the target",

    "Application:Task int TaskInputSignalType= 0 0 0 2 "
      " // Task input signal type: 0: Direct Normalizer%20%280%20to%201%29, 1: Direct Normalizer%20%28%20-1%20to%201%29, 2: Added Normalizer%20%28%20-1%20to%201%29, 3: ..meer_types_meer_beter.. (enumeration)",

    "Application:Task int TaskFirstRunStartDelay= 0 0 0 1000 "
      " // Task start delay on the first run of the task",

    "Application:Task int TaskShowScore= 0 0 0 1 "
      " // Show the score (boolean)",

    "Application:Task int CursorSpeedY= 2s 0s 0s 20s "
      " // Cursorspeed Y multiplication factor",

    "Application:Task int UpdateCursorOnSignal= 0 0 0 1 "
      " // Only update the cursor on incoming signal (boolean)",

	"Application:Targets int NumberTargets= 2 2 0 4000 "
		" // number of targets/trials",

	"Application:Targets intlist TargetSequence= 0 1 % % "
		" // fixed sequence in which targets should be presented (leave empty for random)",

    "Application:Targets int TargetYMode= 1 0 0 3 "
      " // targets y mode 0: Target(matrix) order, 1: random categories, 2:randomize cat without replacement 3:sequential categories with rnd start (enumeration)",

    "Application:Targets int TargetHeightMode= 1 0 0 3 "
      " // targets height mode 0: Target(matrix) order, 1: random categories, 2:random cat without replacement 3:sequential categories with rnd start (enumeration)",

    "Application:Targets matrix Targets= "
		" 3 "						// rows
		" [Y_perc Height_perc] "	// columns
		"  25  50 "
		"  25  50 "
		"  25  50 "
		" // target positions in percentage coordinates",
*/


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

        public bool configure(ref SampleFormat input) {
            return true;
        }

        public void initialize() {
                    
            // lock for thread safety
            lock (lockView) {

                // check the scene (thread) already exists, stop and clear the old one.
                destroyScene();

                //
                mSceneThread = new CursorView(50, 0, 0, 800, 600, false);


                // start the scene thread
                //if (mSceneThread != null) mSceneThread.start();
                mSceneThread.start();

            }

        }

        public void start() {
            
        }

        public void stop() {

        }

        public bool isStarted() {
            return true;
        }

        public void process(double[] input) {

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
                destroyScene();
            }

            // destroy/Cursor more task variables

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

        /*
        // pauzes the task
        void PongFeedbackTask::pauzeTask() {

	        // set task as pauzed
	        mTaskPauzed = true;

	        // store the previous state
	        previousTaskState = taskState;

	        if (mSceneThread != NULL) {

		        // hide everything
		        mSceneThread->setFixation(false);
		        mSceneThread->setCountDown(0);
		        mSceneThread->setCursorVisible(false);
		        mSceneThread->setTargetVisible(false);
		        mSceneThread->setScore(-1);

	        }

        }

        // resumes the task
        void PongFeedbackTask::resumeTask() {

	        // set the previous gamestate
	        setState((TaskStates) previousTaskState);

	        // set task as not longer pauzed
	        mTaskPauzed = false;

        }

        void PongFeedbackTask::destroyScene() {
	
	        // check if a scene thread still exists
	        if (mSceneThread != NULL) {

		        // stop the animation thread
		        mSceneThread->Stop();

		        // wait for the thread to finish (or actually the loop inside the thread to finish)
		        mSceneThread->Wait();
		
	        }

	        // delete the thread
	        delete mSceneThread;
	        mSceneThread = NULL;

        }


        void PongFeedbackTask::setState(TaskStates state) {

	        // Set state
	        taskState = state;

	        switch (state) {
		        case Wait:
			        // starting, pauzed or waiting
			
			        if (mSceneThread != NULL) {
			
				        // hide text if present
				        mSceneThread->setText("");

				        // show the fixation
				        mSceneThread->setFixation(true);

				        // hide the countdown, cursor, target and score
				        mSceneThread->setCountDown(0);
				        mSceneThread->setCursorVisible(false);
				        mSceneThread->setCursorMoving(false);		// only moving is animated
				        mSceneThread->setTargetVisible(false);
				        mSceneThread->setScore(-1);

				        // hide the boundary
				        mSceneThread->setBoundaryVisible(false);

			        }

			        // Set wait counter to startdelay
			        mWaitCounter = mTaskFirstRunStartDelay;

			        break;

		        case CountDown:
			        // countdown when task starts

			        if (mSceneThread != NULL) {
				
				        // hide fixation
				        mSceneThread->setFixation(false);

				        // hide the cursor, target and boundary
				        mSceneThread->setBoundaryVisible(false);
				        mSceneThread->setCursorVisible(false);
				        mSceneThread->setCursorMoving(false);		// only moving is animated
				        mSceneThread->setTargetVisible(false);

				        // set countdown
				        if (mCountdownCounter > 10)			mSceneThread->setCountDown(3);
				        else if (mCountdownCounter > 5)		mSceneThread->setCountDown(2);
				        else if (mCountdownCounter > 0)		mSceneThread->setCountDown(1);
				        else								mSceneThread->setCountDown(0);

			        }

			        break;


		        case Task:
			        // perform the task

			        if (mSceneThread != NULL) {

				        // hide the countdown counter
				        mSceneThread->setCountDown(0);

				        // show the boundary
				        mSceneThread->setBoundaryVisible(true);

				        // set the score for display
				        if (mShowScore)		mSceneThread->setScore(mHitScore);

			        }

			        break;

		        case EndText:
			        // show text
			
			        if (mSceneThread != NULL) {

				        // stop the blocks from moving
				        //mSceneThread->setBlocksMove(false);

				        // hide the boundary, target and cursor
				        mSceneThread->setBoundaryVisible(false);
				        mSceneThread->setCursorVisible(false);
				        mSceneThread->setCursorMoving(false);		// only if moving is animated, do just in case
				        mSceneThread->setTargetVisible(false);

				        // show text
				        mSceneThread->setText("Exit task");
			
			        }

			        // set duration for text to be shown at the end
			        mWaitCounter = 15;

			        break;

	        }
        }


        void PongFeedbackTask::setTrialState(TrialStates state) {

	        // Set state
	        mTrialState = state;

	        switch (mTrialState) {
		        case TrialBeginning:
			        // rest before trial (cursor is hidden)

			        if (mSceneThread != NULL) {

				        // hide the cursor and make the cursor color neutral
				        mSceneThread->setCursorVisible(false);
				        mSceneThread->setCursorColor(PongSceneThread::Neutral);

				        // hide the target and make the target color neutral
				        mSceneThread->setTargetVisible(false);
				        mSceneThread->setTargetColor(PongSceneThread::Neutral);

				        // set the cursor at the beginning
				        mSceneThread->setCursorNormX(0, true);

			        }

			        break;

		        case Trial:
			        // trial (cursor is shown and moving)

			        if (mSceneThread != NULL) {

				        // make the cursor visible and make the cursor color neutral
				        mSceneThread->setCursorVisible(true);
				        mSceneThread->setCursorColor(PongSceneThread::Neutral);

				        // show the target and make the target color neutral
				        mSceneThread->setTargetVisible(true);
				        mSceneThread->setTargetColor(PongSceneThread::Neutral);

				        // check if the cursor movement is animated, if so, set the cursor moving
				        if (!mUpdateCursorOnSignal)
					        mSceneThread->setCursorMoving(true);

			        }

			        break;

		        case TrialEnd:
			        // trial ending (cursor will stay at the end)

			        if (mSceneThread != NULL) {

				        // make the cursor visible
				        mSceneThread->setCursorVisible(true);

				        // show the target
				        mSceneThread->setTargetVisible(true);

				        // check if it was a hit or a miss
				        if (mSceneThread->isTargetHit()) {
					        // hit
					        mSceneThread->setCursorColor(PongSceneThread::Hit);
					        mSceneThread->setTargetColor(PongSceneThread::Hit);
				        } else {
					        // miss
					        mSceneThread->setCursorColor(PongSceneThread::Miss);
					        mSceneThread->setTargetColor(PongSceneThread::Miss);
				        }

				        // check if the cursor movement is animated, if so, stop the cursor from moving
				        if (!mUpdateCursorOnSignal)
					        mSceneThread->setCursorMoving(false);
			
			        }

			        break;

	        }

        }

        // Stop the task
        void PongFeedbackTask::stopTask() {
	
	        // stop any countdowns or fixations
	        if (mSceneThread != NULL) {

		        // hide countdowns or fixations
		        mSceneThread->setFixation(false);
		        mSceneThread->setCountDown(0);

		        // hide the boundary
		        mSceneThread->setBoundaryVisible(false);

		        // hide the target
		        mSceneThread->setTargetVisible(false);

		        // hide the cursor
		        mSceneThread->setCursorVisible(false);

		        // hide the score
		        mSceneThread->setScore(-1);
	
	        }

	        // initialize the target sequence already for a possible next run
	        if (fixedTargetSequence.size() == 0) {

		        // Generate targetlist
		        generateTargetSequence();

	        }

        }

        void PongFeedbackTask::generateTargetSequence() {
	
	        // clear the targets
	        if (mTargetSequence.size() != 0)		mTargetSequence.clear();

	        // create targetsequence array with <NumberTargets>
	        mTargetSequence.resize(mNumberTargets);

	        // randomize using system time
	        srand(time(0));

	        // put the row indices of each distinct value (from the rows in the matrix) in an array
	        // (this is used for the modes which are set to randomization)
	        std::vector<int> catY_unique(0);
	        std::vector<std::vector<int>> catY(0);
	        std::vector<int> catHeight_unique(0);
	        std::vector<std::vector<int>> catHeight (0);
	        for (unsigned int i = 0; i < mTargets[0].size(); ++i) {
		
		        // get the values for the row
		        int valueY = (int)mTargets[0][i];
		        int valueHeight = (int)mTargets[1][i];
		
		        // store the unique values and indices
		        int j = 0;
		        for (j = 0; j < (int)catY_unique.size(); ++j)
			        if (catY_unique[j] == valueY)	break;
		        if (j == (int)catY_unique.size()) {
			        catY_unique.push_back(valueY);						// store the unique value at index j
			        catY.push_back(std::vector<int>(0));				// store the targets row index in the vector at index j							
		        }
		        catY[j].push_back(i);

		        for (j = 0; j < (int)catHeight_unique.size(); ++j)
			        if (catHeight_unique[j] == valueHeight)	break;
		        if (j == (int)catHeight_unique.size()) {
			        catHeight_unique.push_back(valueHeight);			// store the unique value at index j
			        catHeight.push_back(std::vector<int>(0));			// store the targets row index in the vector at index j							
		        }
		        catHeight[j].push_back(i);

	        }
		
	        // create the arrays to handle the no replace randomization (in case it is needed)
	        std::vector<int> catY_noReplace(0);
	        std::vector<int> catHeight_noReplace(0);

	        // create random start for each categories (in case it is needed)
	        int catY_randStart = rand() % catY.size();
	        int catHeight_randStart = rand() % catHeight.size();

	        bool catY_randStart_Added = false;
	        bool catHeight_randStart_Added = false;

	        // create a target sequence
	        std::vector<int> currentY;
	        std::vector<int> currentHeight;

	        // loop <NumberTargets> times to generate each target
	        unsigned int i = 0;
	        unsigned int generateSafetyCounter = mNumberTargets + 1000;
	        while(i < mNumberTargets) {
			
		        // none been added at the beginning of the loop
		        catY_randStart_Added = false;
		        catHeight_randStart_Added = false;

		        // count the loops and check for generation
		        if (generateSafetyCounter-- == 0) {
			        //bcierr << "Error generating random sequence, the generation rules/parameters (TargetYMode, TargetWidthMode, TargetHeightMode and Target) cause a stalemate" << endl;
			        return;
		        }

			
		        // check Y mode
		        if (mTargetYMode == 0) {			// 0: Target(matrix) order
			
			
		        } else if (mTargetYMode == 1) {	// 1: randomize categories
			        currentY = catY[rand() % catY.size()];

		        } else if (mTargetYMode == 2) {	// 2:random categories without replacement
				
			        if (catY_noReplace.size() == 0) {
				        catY_noReplace.resize(catY.size());
					
				        for (int j = 0; j < (int)catY_noReplace.size(); ++j)	catY_noReplace[j] = j;
					
				        std::random_shuffle ( catY_noReplace.begin(), catY_noReplace.end() );
			        }
			        currentY = catY[catY_noReplace[catY_noReplace.size() - 1]];
			        catY_noReplace.resize(catY_noReplace.size() - 1);

		        } else if (mTargetYMode == 3) {	// 3:sequential categories with rnd start
			
			        currentY = catY[catY_randStart];
			        catY_randStart++;
			        if (catY_randStart == (int)catY.size())		catY_randStart = 0;
			        catY_randStart_Added = true;

		        }

		        // check Height mode
		        if (mTargetHeightMode == 0) {			// 0: Target(matrix) order
			
			
		        } else if (mTargetHeightMode == 1) {	// 1: randomize categories
			        currentHeight = catHeight[rand() % catHeight.size()];

		        } else if (mTargetHeightMode == 2) {	// 2:random categories without replacement
			        if (catHeight_noReplace.size() == 0) {
				        catHeight_noReplace.resize(catHeight.size());
				        for (int j = 0; j < (int)catHeight_noReplace.size(); ++j)	catHeight_noReplace[j] = j;
				        std::random_shuffle ( catHeight_noReplace.begin(), catHeight_noReplace.end() );
			        }
			        currentHeight = catHeight[catHeight_noReplace[catHeight_noReplace.size() - 1]];
			        catHeight_noReplace.resize(catHeight_noReplace.size() - 1);

		        } else if (mTargetHeightMode == 3) {	// 3:sequential categories with rnd start
			        currentHeight = catHeight[catHeight_randStart];
			        catHeight_randStart++;
			        if (catHeight_randStart == (int)catHeight.size())		catHeight_randStart = 0;
			        catHeight_randStart_Added = true;

		        }

		        // find a target all modes agree on
		        std::vector<int> currentTarget(mTargets[0].size());
		        for (int j = 0; j < (int)currentTarget.size(); ++j)	currentTarget[j] = j;
		        int j = 0;
		        while(j < (int)currentTarget.size()) {

			        // clear out all the target indices which are not in the currentY
			        bool found = false;
			        for (int k = 0; k < (int)currentY.size(); ++k) {
				        if (currentTarget[j] == currentY[k]) {
					        found = true;	break;
				        }
			        }
			        if (!found && j < (int)currentTarget.size() && currentTarget.size() != 0) {
				        std::swap(currentTarget[j], currentTarget[currentTarget.size() - 1]);
				        currentTarget.resize(currentTarget.size() - 1);
				        continue;
			        }

			        // clear out all the target indices which are not in the currentHeight
			        found = false;
			        for (int k = 0; k < (int)currentHeight.size(); ++k) {
				        if (currentTarget[j] == currentHeight[k]) {
					        found = true;	break;
				        }
			        }
			        if (!found && currentTarget.size() != 0) {
				        std::swap(currentTarget[j], currentTarget[currentTarget.size() - 1]);
				        currentTarget.resize(currentTarget.size() - 1);
				        continue;
			        }

			        // go to the next element
			        j++;

		        }

		        // check if a (agreeable) target has been found
		        if (currentTarget.size() != 0) {
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
				        if (catY_randStart < 0 )		catY_randStart = (int)catY.size() - 1;
			        }
			        if (catHeight_randStart_Added) {
				        catHeight_randStart--;
				        if (catHeight_randStart < 0 )		catHeight_randStart = (int)catHeight.size() - 1;
			        }

		        }

	        }

        }
        ////////////////////////////////////////////////
        //  UNP entry points (start, process, stop)
        ////////////////////////////////////////////////
        #ifdef UNPMENU
        void PongFeedbackTask::UNP_Start(UNPWindowSettings* windowSettings) {

	        // set the task as being start from the UNPMenu
	        mUNPMenuTask = true;

	        // set the task as not stopped
	        mUNPMenuTaskStop = false;

	        // transfer the window settings
	        mWindowRedrawFreqMax = windowSettings->mWindowRedrawFreqMax;
	        mWindowed = windowSettings->mWindowed;
	        mWindowWidth = windowSettings->mWindowWidth;
	        mWindowHeight = windowSettings->mWindowHeight;
	        mWindowLeft = windowSettings->mWindowLeft;
	        mWindowTop = windowSettings->mWindowTop;
	        mFullscreenMonitor = windowSettings->mFullscreenMonitor;


	        // set the UNP task standard settings
	        mShowScore = 1;
	        mTaskInputSignalType = 1;
	        mTaskFirstRunStartDelay = 5;
	
	        mUpdateCursorOnSignal = true;
	        mTrialTime = 20;				// dependent on 'mUpdateCursorOnSignal', now set to packages

	        mNumberTargets = 2;
	        mTargetYMode = 1;				// random categories
	        mTargetHeightMode = 1;			// random categories
	        mTargets[0].clear();	mTargets[0].resize(2);
	        mTargets[1].clear();	mTargets[1].resize(2);
	        mTargets[0][0] = 25;	mTargets[1][0] = 50;
	        mTargets[0][1] = 75;	mTargets[1][1] = 50;


	        // initialize
	        Initialize();

	        // start the task
	        StartRun();

        }

        bool PongFeedbackTask::UNP_Process(double input, bool connectionLost) {

	        // check if the task should not stop
	        if (!mUNPMenuTaskStop) {

		        // transfer connection lost
		        mConnectionLost = connectionLost;

		        // process the input
		        if (!mUNPMenuTaskSuspended)		Process(input);

	        }

	        // return whether the task should run or stop
	        return mUNPMenuTaskStop;
        }

        void PongFeedbackTask::UNP_Stop() {

	        // stop the task from running
	        StopRun();

	        // destroy the view
	        destroyScene();

	        // flag the task to stop running (UNPMenu will pick this up and as parent stop the task)
	        mUNPMenuTaskStop = true;

        }

        bool PongFeedbackTask::UNP_IsStopped() {

	        // return whether the task has stopped
	        return mUNPMenuTaskStop;

        }

        void PongFeedbackTask::UNP_Suspend() {
	
	        // flag task as suspended
	        mUNPMenuTaskSuspended = true;

	        // pauze the task
	        pauzeTask();


	        // stop the view thread
	        if (mSceneThread != NULL) {
		        mSceneThread->Stop();
		        mSceneThread->Wait();
	        }

        }

        void PongFeedbackTask::UNP_Resume() {

	        // restart the view thread
	        if (mSceneThread != NULL) {

		        mSceneThread->Start();

	        }

	        // allow the thread to start
	        ::Sleep(20);
	
	        // resume the task
	        resumeTask();

	        // flag task as no longer suspended
	        mUNPMenuTaskSuspended = false;

        }

        #endif
        */



    }
}
