using NLog;
using System;
using UNP.Core;
using UNP.Core.Helpers;
using UNP.Views;

namespace CursorTask {

    class CursorView : OpenTKView, IView {
    //class CursorView : SharpGLView, IView {
        
        private const double PI = 3.1415926535897932384626433832795;
        private const int taskBoundaryLineWidth = 2;
        private const int targetWidth = 20;

        private static Logger logger = LogManager.GetLogger("CursorView");                        // the logger object for the view
        public enum ColorStates {
            Neutral,
            Hit,
            Miss
        };

        private Object textureLock = new Object();                                      // threadsafety lock for texture events

        private bool showBoundary = false;
        private int boundarySize = 0;
        private int boundaryX = 0;
        private int boundaryY = 0;


        private int cursorRadius = 40;                                      // the cursor radius
        private bool showCursor = false;                                    // show the cursor
        private bool moveCursor = false;                                    // move the cursor
        private double cursorX = 0;                                         // the x position of the middle of the cursor
        private double cursorY = 0;                                         // the y position of the middle of the cursor
        private double cursorSpeedTotalTrialTime = 0;                       // cursorspeed in total time per trial
        private double cursorSpeed = 0;                                     // cursorspeed in pixels per second
        private ColorStates cursorColorState = ColorStates.Neutral;
        private float cursorNeutralColorR = 0;                            // cursor color when moving
        private float cursorNeutralColorG = 0;                            // 
        private float cursorNeutralColorB = 0;                            // 
        private float cursorHitColorR = 0;                                // cursor color when target is hit
        private float cursorHitColorG = 0;                                // 
        private float cursorHitColorB = 0;                                //
        private float cursorMissColorR = 0;                               // cursor color when target is missed
        private float cursorMissColorG = 0;                               // 
        private float cursorMissColorB = 0;                               //

        private bool showTarget = false;
        private float targetY = 0;
        private float targetHeight = 0;
        private ColorStates targetColorState = ColorStates.Neutral;
        private float targetNeutralColorR = 0;                            // target color when cursor is moving
        private float targetNeutralColorG = 0;                            // 
        private float targetNeutralColorB = 0;                            //
        private float targetHitColorR = 0;                                // target color when target is hit
        private float targetHitColorG = 0;                                // 
        private float targetHitColorB = 0;                                //
        private float targetMissColorR = 0;                               // cursor color when target is missed
        private float targetMissColorG = 0;                               // 
        private float targetMissColorB = 0;								//

        private int showCountDown = -1;                                                 // whether the countdown should be shown (-1 = off, 1..3 = count)
        private bool showFixation = false;                                              // whether the fixation should be shown
        private int score = -1;									                        // the score that is being shown (-1 = do not show score)

        private glFreeTypeFont scoreFont = new glFreeTypeFont();
        private glFreeTypeFont countdownFont = new glFreeTypeFont();
        private glFreeTypeFont fixationFont = new glFreeTypeFont();

        // general UNP variables
        private bool showConnectionLost = false;
        private int connectionLostTexture = 0;
        private glFreeTypeFont textFont = new glFreeTypeFont();
        private string showText = "";
        private int showTextWidth = 0;


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

            // initialize the text font
            textFont.init(this, "fonts\\ariblk.ttf", (uint)(getContentHeight() / 20), "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ. ");

            // initialize the countdown and fixation fonts
            countdownFont.init(this, "fonts\\ariblk.ttf", (uint)(getContentHeight() / 7), "1234567890");
            fixationFont.init(this, "fonts\\ariblk.ttf", (uint)(getContentHeight() / 10), "+");

            // initialize the score font
            scoreFont.init(this, "fonts\\ariblk.ttf", (uint)(getContentHeight() / 30), "Score: 0123456789");

            // lock for textures events (thread safety)
            lock (textureLock) {

                // load the connection lost texture
                connectionLostTexture = (int)loadImage("images\\nosignal.png");

            }

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

            // clear the text font
            textFont.clean();

            // clear the fonts
            scoreFont.clean();
            fixationFont.clean();
            countdownFont.clean();

            // lock for textures events (thread safety)
            lock (textureLock) {

                // clear the no signal texture
                glDeleteTexture(connectionLostTexture);

            }

        }

        protected override void resize(int width, int height) {
            

        }

        protected override void update(double secondsElapsed) {
            
	        if (moveCursor) {
		
		        cursorX += (cursorSpeed * secondsElapsed);


		        // check if the cursor is at Y with the target
		        if (isTargetHit()) {
			        // cursor should be hitting target

			        // stop before target
			        if (cursorX + cursorRadius >= boundaryX + boundarySize - (taskBoundaryLineWidth / 2) - targetWidth)
				        cursorX = boundaryX + boundarySize - (taskBoundaryLineWidth / 2.0) - targetWidth - cursorRadius;

		        } else {
			        // cursor is above or below target

			        // stop before boundary
			        if (cursorX + cursorRadius >= boundaryX + boundarySize - (taskBoundaryLineWidth / 2))
				        cursorX = boundaryX + boundarySize - (taskBoundaryLineWidth / 2.0) - cursorRadius;

		        }
			
	        }
            

        }

        protected override void render() {

            // check if fixation should be shown
            if (showFixation) {

                // set the fixation to white
                glColor3(1f, 1f, 1f);

                // set the text count
                int fixationTextWidth = fixationFont.getTextWidth("+");
                fixationFont.printLine((int)((getContentWidth() - fixationTextWidth) / 2), (int)((getContentHeight() - fixationFont.height) / 2), "+");

            }

            if (showCountDown >= 0) {

                // set the countdown to white
                glColor3(1f, 1f, 1f);

                // set the text count
                int countTextWidth = countdownFont.getTextWidth(showCountDown.ToString());
                countdownFont.printLine((int)((getContentWidth() - countTextWidth) / 2), (int)((getContentHeight() - countdownFont.height) / 2), showCountDown.ToString());

            }

            // show task boundary as rectangle
            if (taskBoundaryLineWidth > 0 && showBoundary) {

                drawRectangle(  boundaryX, 
							    boundaryY, 
							    boundaryX + boundarySize,
							    boundaryY + boundarySize, 
							    taskBoundaryLineWidth,
							    0f, 1f, 1f);

	        }

	        // draw the target
	        if (showTarget) {

		        // set the target color
		        if (targetColorState == ColorStates.Hit)
			        glColor3(targetHitColorR, targetHitColorG, targetHitColorB);
		        else if (targetColorState == ColorStates.Miss)
			        glColor3(targetMissColorR, targetMissColorG, targetMissColorB);
		        else
			        glColor3(targetNeutralColorR, targetNeutralColorG, targetNeutralColorB);

                // set no texture
                glBindTexture2D(0);

                // draw the block
                glBeginTriangles();

			        // vertex 0
			        glVertex3(  boundaryX + boundarySize - (taskBoundaryLineWidth / 2),				
						        targetY + targetHeight,
						        0.0f);

			        glVertex3(  boundaryX + boundarySize - (taskBoundaryLineWidth / 2),				
						        targetY,				
						        0.0f);
			
			        glVertex3(  boundaryX + boundarySize - targetWidth - (taskBoundaryLineWidth / 2),	
						        targetY,				
						        0.0f);

			        //vertex 1
			        glVertex3(  boundaryX + boundarySize - targetWidth - (taskBoundaryLineWidth / 2),	
						        targetY + targetHeight,	
						        0.0f);

			        glVertex3(  boundaryX + boundarySize - (taskBoundaryLineWidth / 2),				
						        targetY + targetHeight,
						        0.0f);

			        glVertex3(  boundaryX + boundarySize - targetWidth - (taskBoundaryLineWidth / 2),	
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
		        if (cursorColorState == ColorStates.Hit)
			        glColor3(cursorHitColorR, cursorHitColorG, cursorHitColorB);
		        else if (targetColorState == ColorStates.Miss)
			        glColor3(cursorMissColorR, cursorMissColorG, cursorMissColorB);
		        else
			        glColor3(cursorNeutralColorR, cursorNeutralColorG, cursorNeutralColorB);

                // set no texture
                glBindTexture2D(0);

                // cursor polyfgon
                glBeginPolygon();
			        for(double i = 0; i < 2 * PI; i += PI / 24)
 				        glVertex3(Math.Cos(i) * cursorRadius + cursorX, Math.Sin(i) * cursorRadius + cursorY, 0.0);
                glEnd();

	        }

            // write the score text
            if (score > -1) {
                glColor3(1f, 1f, 1f);
                scoreFont.printLine(getContentWidth() - scoreFont.height * 9, 5, ("Score: " + score));
            }

	        // check if text should be shown
	        if (showText.Length != 0) {

		        // set the text to white
		        glColor3(1f, 1f, 1f);
		
		        // print the text
                textFont.printLine((getContentWidth() - showTextWidth) / 2, getContentHeight() / 2, showText);

	        }
            
	        // check if there is no signal
	        if (showConnectionLost) {

                // set white color for drawing
                glColor3(1f, 1f, 1f);

                // print text
                int textWidth = textFont.getTextWidth("Lost connection with device");
		        textFont.printLine((int)((getContentWidth() - textWidth) / 2), (int)((getContentHeight()) / 4), "Lost connection with device");

		        // set texture
                glBindTexture2D(connectionLostTexture);

		        // draw texture
                glBeginTriangles();

			        // vertex 0
			        glTexCoord2(0.0f, 0.0f);
			        glVertex3( (getContentWidth() - 200) / 2,				(getContentHeight() - 200) / 2,	            0.0f);

			        glTexCoord2(1.0f, 0.0f);
			        glVertex3( (getContentWidth() - 200) / 2 + 200,			(getContentHeight() - 200) / 2,		        0.0f);
			
			        glTexCoord2(1.0f, 1.0f);
			        glVertex3( (getContentWidth() - 200) / 2 + 200,			(getContentHeight() - 200) / 2 + 200,       0.0f);

			        //vertex 1
			        glTexCoord2(0.0f, 0.0f);
			        glVertex3( (getContentWidth() - 200) / 2,				(getContentHeight() - 200) / 2,		        0.0f);

			        glTexCoord2(1.0f, 1.0f);
			        glVertex3( (getContentWidth() - 200) / 2 + 200,			(getContentHeight() - 200) / 2 + 200,		0.0f);

			        glTexCoord2(0.0f, 1.0f);
			        glVertex3( (getContentWidth() - 200) / 2,				(getContentHeight() - 200) / 2 + 200,		0.0f);

		        glEnd();

	        }

        }

        protected override void userCloseForm() {

            // pass to the mainthread
            MainThread.eventViewClosed();

        }

        public void setText(string text) {
	
	        // set the text
	        showText = text;

	        // if not empty, determine the width once
            if (!String.IsNullOrEmpty(showText))
                showTextWidth = textFont.getTextWidth(showText);

        }

        public void setConnectionLost(bool connectionLost) {
	        showConnectionLost = connectionLost;
        }




        public double getCursorX() {
	        return cursorX;
        }

        public double getCursorY() {
	        return cursorY;
        }

        // get the cursor's y position as a normalized valued (0 = bottom, 1 = top, also takes cursor radius into account)
        public double getCursorNormY() {
            return (((boundaryY + boundarySize - (taskBoundaryLineWidth / 2) - cursorRadius) - cursorY) / (boundarySize - taskBoundaryLineWidth - (cursorRadius * 2)));
        }

        // set the cursor's x position to a normalized valued (0 = left, 1 = right, also takes cursor radius into account)
        public void setCursorNormX(double x, bool accountForTarget) {
	        if (x < 0) x = 0;
	        if (x > 1) x = 1;

	        if (accountForTarget)
		        cursorX = boundaryX + (taskBoundaryLineWidth / 2) + cursorRadius + (int)((boundarySize - taskBoundaryLineWidth - targetWidth - (cursorRadius * 2)) * x);
	        else	
		        cursorX = boundaryX + (taskBoundaryLineWidth / 2) + cursorRadius + (int)((boundarySize - taskBoundaryLineWidth - (cursorRadius * 2)) * x);
	
        }

        // set the cursor's y position to a normalized valued (0 = bottom, 1 = top, also takes cursor radius into account)
        public void setCursorNormY(double y) {
	        if (y < 0) y = 0;
	        if (y > 1) y = 1;
	        cursorY = boundaryY + boundarySize - (taskBoundaryLineWidth / 2) - cursorRadius - ((boundarySize - taskBoundaryLineWidth - (cursorRadius * 2)) * y);
        }

        public void centerCursorY() {
	        cursorY = (getContentHeight() - cursorRadius) / 2.0;
        }

        public void setCursorVisible(bool show) {
	        showCursor = show;
        }

        public void setCursorSpeed(float TotalTrialTime) {
	        cursorSpeedTotalTrialTime = TotalTrialTime;
	        if (boundarySize == 0 || cursorSpeedTotalTrialTime == 0)	cursorSpeed = 0;
	        else														cursorSpeed = ((double)boundarySize - taskBoundaryLineWidth - targetWidth - (cursorRadius * 2)) / cursorSpeedTotalTrialTime;
        }

        public void setCursorMoving(bool move) {
	        moveCursor = move;
        }

        // set the cursor size radius as a percentage of the bounding box size
        public void setCursorSizePerc(double perc) {
	        setCursorSize((int)(getContentHeight() / 100.0 * perc));
        }

        public void setCursorSize(int radius) {
	        if (radius * 2 > getContentHeight())		radius = getContentHeight() / 2;
	        cursorRadius = radius;
        }

        public void setCursorColor(ColorStates state) {
	        cursorColorState = state;
        }

        public void setCursorNeutralColor(float red, float green, float blue) {
	        cursorNeutralColorR = red;
	        cursorNeutralColorG = green;
	        cursorNeutralColorB = blue;
        }
        public void setCursorNeutralColor(RGBColorFloat color) {
            cursorNeutralColorR = color.getRed();
            cursorNeutralColorG = color.getGreen();
            cursorNeutralColorB = color.getBlue();
        }

        public void setCursorHitColor(float red, float green, float blue) {
	        cursorHitColorR = red;
	        cursorHitColorG = green;
	        cursorHitColorB = blue;
        }
        public void setCursorHitColor(RGBColorFloat color) {
            cursorHitColorR = color.getRed();
            cursorHitColorG = color.getGreen();
            cursorHitColorB = color.getBlue();
        }

        public void setCursorMissColor(float red, float green, float blue) {
	        cursorMissColorR = red;
	        cursorMissColorG = green;
	        cursorMissColorB = blue;
        }
        public void setCursorMissColor(RGBColorFloat color) {
            cursorMissColorR = color.getRed();
            cursorMissColorG = color.getGreen();
            cursorMissColorB = color.getBlue();
        }

        public bool isCursorAtEnd(bool accountForTarget) {

	        if (accountForTarget)
		        return (cursorX + cursorRadius >= boundaryX + boundarySize - (taskBoundaryLineWidth / 2) - targetWidth);
	        else	
		        return (cursorX + cursorRadius >= boundaryX + boundarySize - (taskBoundaryLineWidth / 2));
	
        }

        public void setCountDown(int count) {
	        showCountDown = count;
        }

        public void setFixation(bool fix) {
	        showFixation = fix;
        }

        public void setScore(int newScore) {
	        score = newScore;
        }

        public void setBoundaryVisible(bool show) {
	        showBoundary = show;
        }

        public void setTargetVisible(bool show) {
	        showTarget = show;
        }

        public void setTarget(int posYInPerc, int heightInPerc) {
            // calculate the target height and y position (based on percentages)
            targetHeight = (float)((boundarySize - taskBoundaryLineWidth) * (heightInPerc / 100.0));
	        targetY = (float)(boundaryY + (taskBoundaryLineWidth / 2) + (boundarySize - taskBoundaryLineWidth) * (posYInPerc / 100.0) - targetHeight / 2.0);
        }

        public void setTargetColor(ColorStates state) {
	        targetColorState = state;
        }

        public void setTargetNeutralColor(float red, float green, float blue) {
	        targetNeutralColorR = red;
	        targetNeutralColorG = green;
	        targetNeutralColorB = blue;
        }
        public void setTargetNeutralColor(RGBColorFloat color) {
            targetNeutralColorR = color.getRed();
            targetNeutralColorG = color.getGreen();
            targetNeutralColorB = color.getBlue();
        }

        public void setTargetHitColor(float red, float green, float blue) {
	        targetHitColorR = red;
	        targetHitColorG = green;
	        targetHitColorB = blue;
        }
        public void setTargetHitColor(RGBColorFloat color) {
            targetHitColorR = color.getRed();
            targetHitColorG = color.getGreen();
            targetHitColorB = color.getBlue();
        }

        public void setTargetMissColor(float red, float green, float blue) {
	        targetMissColorR = red;
	        targetMissColorG = green;
	        targetMissColorB = blue;
        }
        public void setTargetMissColor(RGBColorFloat color) {
            targetMissColorR = color.getRed();
            targetMissColorG = color.getGreen();
            targetMissColorB = color.getBlue();
        }

        public bool isTargetHit() {
	        return (cursorY > targetY && cursorY < targetY + targetHeight);
        }

        public bool resourcesLoaded() {
            return (isStarted());
        }


    }

}
