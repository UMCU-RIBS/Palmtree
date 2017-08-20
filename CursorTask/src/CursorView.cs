using NLog;
using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UNP.Views;

namespace CursorTask {

    class CursorView : OpenTKView, IView {
    //class CursorView : SharpGLView, IView {
        
        private const int taskBoundaryLineWidth = 2;
        private const int targetWidth = 20;

        private static Logger logger = LogManager.GetLogger("CursorView");                        // the logger object for the view

        public enum ColorStates {
            Neutral,
            Hit,
            Miss
        };

        private bool showBoundary = false;
        private int boundarySize = 0;
        private float boundaryX = 0;
        private float boundaryY = 0;


        private int cursorRadius = 40;                                   // the cursor radius
        private bool showCursor = false;                                    // show the cursor
        private bool moveCursor = false;                                    // move the cursor
        private int cursorX = 0;                                        // the x position of the middle of the cursor
        private int cursorY = 0;                                        // the y position of the middle of the cursor
        private double cursorSpeedTotalTrialTime = 0;                    // cursorspeed in total time per trial
        private double cursorSpeed = 0;									// cursorspeed in pixels per second



        private int showCountDown = -1;                                                 // whether the countdown should be shown (-1 = off, 1..3 = count)
        private bool showFixation = false;                                              // whether the fixation should be shown
        private long score = -1;									                        // the score that is being shown (-1 = do not show score)

        private glFreeTypeFont scoreFont = new glFreeTypeFont();
        private glFreeTypeFont countdownFont = new glFreeTypeFont();
        private glFreeTypeFont fixationFont = new glFreeTypeFont();

        // general UNP variables
        private bool showConnectionLost = false;
        private int connectionLostTexture = 0;
        


        public CursorView() : base(120, 0, 0, 640, 480, true) {
            
        }

        public CursorView(int updateFrequency, int x, int y, int width, int height, bool border) : base(updateFrequency, x, y, width, height, border) {
            
        }

        ///////////////////////
        /// task functions
        //////////////////////



        ///////////////////////
        /// openGL load and draw functions
        //////////////////////


        protected override void load() {


            // initialize the countdown and fixation fonts
            countdownFont.init(this, "fonts\\ariblk.ttf", (uint)(getContentHeight() / 7), "1234567890");
            fixationFont.init(this, "fonts\\ariblk.ttf", (uint)(getContentHeight() / 10), "+");

            // initialize the score font
            scoreFont.init(this, "fonts\\ariblk.ttf", (uint)(getContentHeight() / 30), "Score: 0123456789");

            // TODO: 
            /*
            // check if openGL can handle the line width
            GLfloat lineWidthRange[2] = { 0.0f, 0.0f };
            glGetFloatv(GL_LINE_WIDTH_RANGE, lineWidthRange);
            if (taskBoundaryLineWidth > lineWidthRange[1]) {

                // message about this limitation
                std::stringstream ss;
                ss << "A line width of " << taskBoundaryLineWidth << " has been set, openGL can only draw lines width a maximum width of " << lineWidthRange[1];
                MessageBox(0, ss.str().c_str(), "Warning", MB_OK | MB_ICONEXCLAMATION);

            }
            */
            

            // the dimensions and locations for a square task area in the middle of the window
            if (getContentWidth() > getContentHeight()) {
                boundarySize = getContentHeight() - taskBoundaryLineWidth;
            } else {
                boundarySize = getContentWidth() - taskBoundaryLineWidth;
            }
            boundaryX = (getContentWidth() - boundarySize) / 2;
            boundaryY = (getContentHeight() - boundarySize) / 2;

            // check and/or set the cursorspeed
            if (boundarySize == 0 || cursorSpeedTotalTrialTime == 0)    cursorSpeed = 0;
            else                                                        cursorSpeed = ((float)boundarySize - taskBoundaryLineWidth - targetWidth - (cursorRadius * 2)) / cursorSpeedTotalTrialTime;
            


        }

        protected override void unload() {

            /*
            // clear the fonts
            scoreFont.clean();
            fixationFont.clean();
            countdownFont.clean();
            */

        }

        protected override void resize(int width, int height) {
            

        }

        protected override void update(double secondsElapsed) {
            /*
	        if (moveCursor) {
		
		        cursorX += cursorSpeed * secondsElapsed;


		        // check if the cursor is at Y with the target
		        if (isTargetHit()) {
			        // cursor should be hitting target

			        // stop before target
			        if (cursorX + cursorRadius >= boundaryX + boundarySize - (taskBoundaryLineWidth / 2) - targetWidth)
				        cursorX = boundaryX + boundarySize - (taskBoundaryLineWidth / 2) - targetWidth - cursorRadius;

		        } else {
			        // cursor is above or below target

			        // stop before boundary
			        if (cursorX + cursorRadius >= boundaryX + boundarySize - (taskBoundaryLineWidth / 2))
				        cursorX = boundaryX + boundarySize - (taskBoundaryLineWidth / 2) - cursorRadius;

		        }
			
	        }
            */

        }

        protected override void render() {

            /*
	        // check if fixation should be shown
	        if (showFixation) {

		        // set the fixation to white
		        glColor3f(1, 1, 1);

		        // set the text count
		        int fixationTextWidth = glfwFreeType::getLineWidth(fixationFont, "+");
		        glfwFreeType::printLine(fixationFont, (int)((mWindowWidth - fixationTextWidth) / 2), (int)((mWindowHeight - fixationFont.h) / 2), "+");

	        }

	        if (showCountDown != 0) {

		        // set the countdown to white
		        glColor3f(1, 1, 1);

		        // set the text count
		        int countTextWidth = glfwFreeType::getLineWidth(countdownFont, "%i", showCountDown);
		        glfwFreeType::printLine(countdownFont, (int)((mWindowWidth - countTextWidth) / 2), (int)((mWindowHeight - countdownFont.h) / 2), "%i", showCountDown);


	        }

	        // show task boundary as rectangle
	        if (taskBoundaryLineWidth > 0 && showBoundary) {

		        this->drawRectangle(boundaryX, 
							        boundaryY, 
							        boundaryX + boundarySize,
							        boundaryY + boundarySize, 
							        taskBoundaryLineWidth, 
							        0.0, 1.0, 1.0);

	        }

	        // draw the target
	        if (showTarget) {

		        // set the target color
		        if (targetColorState == Hit)
			        glColor3f(targetHitColorR, targetHitColorG, targetHitColorB);
		        else if (targetColorState == Miss)
			        glColor3f(targetMissColorR, targetMissColorG, targetMissColorB);
		        else
			        glColor3f(targetNeutralColorR, targetNeutralColorG, targetNeutralColorB);

		        // set no texture
		        glBindTexture(GL_TEXTURE_2D, NULL);

		        // draw the block
		        glBegin(GL_TRIANGLES);

			        // vertex 0
			        glVertex3f( boundaryX + boundarySize - (taskBoundaryLineWidth / 2),				
						        targetY + targetHeight,
						        0.0f);

			        glVertex3f( boundaryX + boundarySize - (taskBoundaryLineWidth / 2),				
						        targetY,				
						        0.0f);
			
			        glVertex3f( boundaryX + boundarySize - targetWidth - (taskBoundaryLineWidth / 2),	
						        targetY,				
						        0.0f);

			        //vertex 1
			        glVertex3f( boundaryX + boundarySize - targetWidth - (taskBoundaryLineWidth / 2),	
						        targetY + targetHeight,	
						        0.0f);

			        glVertex3f( boundaryX + boundarySize - (taskBoundaryLineWidth / 2),				
						        targetY + targetHeight,
						        0.0f);

			        glVertex3f( boundaryX + boundarySize - targetWidth - (taskBoundaryLineWidth / 2),	
						        targetY,				
						        0.0f);

		        glEnd();

		        
		        //this->drawRectangle(, 
				//			        targetY, 
				//			        boundaryX + boundarySize + 5,
				//			        targetY + targetHeight, 
				//			        taskBoundaryLineWidth, 
				//			        1.0, 1.0, 1.0);
							        

	        }

	        // draw the cursor
	        if (showCursor) {

		        // set the cursor color
		        if (cursorColorState == Hit)
			        glColor3f(cursorHitColorR, cursorHitColorG, cursorHitColorB);
		        else if (targetColorState == Miss)
			        glColor3f(cursorMissColorR, cursorMissColorG, cursorMissColorB);
		        else
			        glColor3f(cursorNeutralColorR, cursorNeutralColorG, cursorNeutralColorB);

		        // set no texture
		        glBindTexture(GL_TEXTURE_2D, NULL);

		        // cursor polyfgon
		        glBegin(GL_POLYGON);
			        for(double i = 0; i < 2 * PI; i += PI / 24)
 				        glVertex3f(cos(i) * cursorRadius + cursorX, sin(i) * cursorRadius + cursorY, 0.0);
		        glEnd();

	        }

	        // write the score text
	        if (score > -1) {
		        glColor3f(1, 1, 1);
		        glfwFreeType::printLine(scoreFont, mWindowWidth - scoreFont.h * 9, 0, "Score: %i", score);
	        }
            */
            
        }




        /*
        int PongSceneThread::getCursorX() {
	        return cursorX;
        }

        int PongSceneThread::getCursorY() {
	        return cursorY;
        }

        // set the cursor's x position to a normalized valued (0 = left, 1 = right, also takes cursor radius into account)
        void PongSceneThread::setCursorNormX(float x, bool accountForTarget) {
	        if (x < 0) x = 0;
	        if (x > 1) x = 1;

	        if (accountForTarget)
		        cursorX = boundaryX + (taskBoundaryLineWidth / 2) + cursorRadius + (int)((boundarySize - taskBoundaryLineWidth - targetWidth - (cursorRadius * 2)) * x);
	        else	
		        cursorX = boundaryX + (taskBoundaryLineWidth / 2) + cursorRadius + (int)((boundarySize - taskBoundaryLineWidth - (cursorRadius * 2)) * x);
	
        }

        // set the cursor's y position to a normalized valued (0 = bottom, 1 = top, also takes cursor radius into account)
        void PongSceneThread::setCursorNormY(float y) {
	        if (y < 0) y = 0;
	        if (y > 1) y = 1;
	        cursorY = boundaryY + boundarySize - (taskBoundaryLineWidth / 2) - cursorRadius - (int)((boundarySize - taskBoundaryLineWidth - (cursorRadius * 2)) * y);
        }

        void PongSceneThread::centerCursorY() {
	        cursorY = (mWindowHeight - cursorRadius) / 2;
        }

        void PongSceneThread::setCursorVisible(bool show) {
	        showCursor = show;
        }

        void PongSceneThread::setCursorSpeed(float TotalTrialTime) {
	        cursorSpeedTotalTrialTime = TotalTrialTime;
	        if (boundarySize == 0 || cursorSpeedTotalTrialTime == 0)	cursorSpeed = 0;
	        else														cursorSpeed = ((float)boundarySize - taskBoundaryLineWidth - targetWidth - (cursorRadius * 2)) / cursorSpeedTotalTrialTime;
        }

        void PongSceneThread::setCursorMoving(bool move) {
	        moveCursor = move;
        }

        // set the cursor size radius as a percentage of the screen height
        void PongSceneThread::setCursorSizePerc(float perc) {
	        setCursorSize((int)(mWindowHeight / 100.0 * perc));
        }

        void PongSceneThread::setCursorSize(int radius) {
	        if (radius * 2 > mWindowHeight)		radius = mWindowHeight / 2;
	        cursorRadius = radius;
        }

        void PongSceneThread::setCursorColor(ColorStates state) {
	        cursorColorState = state;
        }

        void PongSceneThread::setCursorNeutralColor(GLfloat red, GLfloat green, GLfloat blue) {
	        cursorNeutralColorR = red;
	        cursorNeutralColorG = green;
	        cursorNeutralColorB = blue;
        }

        void PongSceneThread::setCursorHitColor(GLfloat red, GLfloat green, GLfloat blue) {
	        cursorHitColorR = red;
	        cursorHitColorG = green;
	        cursorHitColorB = blue;
        }

        void PongSceneThread::setCursorMissColor(GLfloat red, GLfloat green, GLfloat blue) {
	        cursorMissColorR = red;
	        cursorMissColorG = green;
	        cursorMissColorB = blue;
        }

        bool PongSceneThread::isCursorAtEnd(bool accountForTarget) {

	        if (accountForTarget)
		        return (cursorX + cursorRadius >= boundaryX + boundarySize - (taskBoundaryLineWidth / 2) - targetWidth);
	        else	
		        return (cursorX + cursorRadius >= boundaryX + boundarySize - (taskBoundaryLineWidth / 2));
	
        }

        void PongSceneThread::setCountDown(int count) {
	        showCountDown = count;
        }

        void PongSceneThread::setFixation(bool fix) {
	        showFixation = fix;
        }

        void PongSceneThread::setScore(signed long newScore) {
	        score = newScore;
        }

        void PongSceneThread::setBoundaryVisible(bool show) {
	        showBoundary = show;
        }

        void PongSceneThread::setTargetVisible(bool show) {
	        showTarget = show;
        }

        void PongSceneThread::setTarget(int posYInPerc, int heightInPerc) {
	        // calculate the target height and y position (based on percentages)
	        targetHeight = (boundarySize - taskBoundaryLineWidth) * (heightInPerc / 100.0);
	        targetY = boundaryY + (taskBoundaryLineWidth / 2) + (boundarySize - taskBoundaryLineWidth) * (posYInPerc / 100.0) - targetHeight / 2.0;
        }

        void PongSceneThread::setTargetColor(ColorStates state) {
	        targetColorState = state;
        }

        void PongSceneThread::setTargetNeutralColor(GLfloat red, GLfloat green, GLfloat blue) {
	        targetNeutralColorR = red;
	        targetNeutralColorG = green;
	        targetNeutralColorB = blue;
        }

        void PongSceneThread::setTargetHitColor(GLfloat red, GLfloat green, GLfloat blue) {
	        targetHitColorR = red;
	        targetHitColorG = green;
	        targetHitColorB = blue;
        }

        void PongSceneThread::setTargetMissColor(GLfloat red, GLfloat green, GLfloat blue) {
	        targetMissColorR = red;
	        targetMissColorG = green;
	        targetMissColorB = blue;
        }


        bool PongSceneThread::isTargetHit() {
	        return (cursorY > targetY && cursorY < targetY + targetHeight);
        }

        bool PongSceneThread::resourcesLoaded() {
	        return (sceneInitialized);
        }
        */


    }

}
