using NLog;
using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using UNP.Core.Helpers;
using UNP.Views;

namespace FollowTask {

    public class FollowView : OpenTKView, IView {
    //public class FollowView : SharpGLView, IView {

        private const double PI = 3.1415926535897932384626433832795;
        public const int noBlock = -1;

        private static Logger logger = LogManager.GetLogger("FollowView");                        // the logger object for the view

        private Object textureLock = new Object();                                      // threadsafety lock for texture events
	    
		private byte doLoadTextures = 0;								                // load textures in the loop thread (flag to check for new textures. Numeric because while textures are being loaded, the thread can make multiple function calls to load)
		private List<string> blockTexturesToLoad = new List<string>(0);                 // array with block textures filepath that should be loaded (from the loop thread)
		private List<int> blockTextures = new List<int>(0);	                            // array with possible block textures
		private List<FollowBlock> mBlocks = new List<FollowBlock>(0);	                // block objects
		private bool mBlocksMove = false;								                // enable/disable block movement
		private float blockSpeed = 0;									                // the speed of the movement of the block (in pixels per second)
		private bool showBlocks = false;								                // show the blocks
		private int mCurrentBlock = noBlock;							                // the current block which is in line with X of the cursor (so the middle)
		private bool mCursorInCurrentBlock = false;						                // hold whether the cursor is inside of the current block
		
		private int cursorRadius = 40;									                // the cursor radius
		private bool showCursor = false;								                // show the cursor
		private int cursorX = 0;										                // the x position of the middle of the cursor
		private int cursorY = 0;										                // the y position of the middle of the cursor
        private RGBColorFloat cursorHitColor = new RGBColorFloat(0.8f, 0.8f, 0f);       // cursor color when hitting
        private RGBColorFloat cursorMissColor = new RGBColorFloat(0.8f, 0f, 0f);        // cursor color when missing
        private RGBColorFloat cursorEscapeColor = new RGBColorFloat(0.8f, 0f, 0.8f);    // cursor color when escape sequence
		private int cursorColorSetting = 0;							                    // determine the cursor color if (not) hitting (0 = manual false, 1 = manual true, 2 = automatic by mCursorInCurrentBlock)

		private int showCountDown = -1;									                // whether the countdown should be shown (-1 = off, 1..3 = count)
		private bool showFixation = false;								                // whether the fixation should be shown
		private long score = -1;									                        // the score that is being shown (-1 = do not show score)

        private glFreeTypeFont scoreFont = new glFreeTypeFont();
        private glFreeTypeFont countdownFont = new glFreeTypeFont();
        private glFreeTypeFont fixationFont = new glFreeTypeFont();

        // general UNP variables
        private bool showConnectionLost = false;
        private int connectionLostTexture = 0;
        private glFreeTypeFont textFont = new glFreeTypeFont();
        private string showText = "";
        private int showTextWidth = 0;


        public FollowView() : base(60, 0, 0, 640, 480, true) {
            
        }

        public FollowView(int updateFrequency, int x, int y, int width, int height, bool border) : base(updateFrequency, x, y, width, height, border) {
            
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

	        // initialize the countdown, text and fixation fonts
            countdownFont.init(this, "fonts\\ariblk.ttf", (uint)(getContentHeight() / 7), "1234567890");
            fixationFont.init(this, "fonts\\ariblk.ttf", (uint)(getContentHeight() / 10), "+");

	        // initialize the score font
            scoreFont.init(this, "fonts\\ariblk.ttf", (uint)(getContentHeight() / 30), "Score: 0123456789");

            // lock for textures events (thread safety)
            lock(textureLock) {

                // load the connection lost texture
                connectionLostTexture = (int)loadImage("images\\nosignal.png");

            }

        }

        protected override void unload() {

            // clear the text font
            textFont.clean();

	        // clear the fonts
	        scoreFont.clean();
	        fixationFont.clean();
	        countdownFont.clean();

            // lock for textures events (thread safety)
            lock(textureLock) {

                // clear the no signal texture
                glDeleteTexture(connectionLostTexture);

	            // reset all loading variables
	            doLoadTextures = 0;
	            blockTexturesToLoad.Clear();

	            // clear all block information
	            for (int i = 0; i < (int)mBlocks.Count(); ++i)  mBlocks[i].mTexture = 0;
	            mBlocks.Clear();

	            // clear all textures
	            for (int i = 0; i < (int)blockTextures.Count(); ++i)
                    glDeleteTexture(blockTextures[i]);
	            blockTextures.Clear();

            }
        }

        protected override void resize(int width, int height) {
            

        }

        protected override void update(double secondsElapsed) {

	        // check if the blocks should move
	        if (mBlocksMove) {

		        // loop through the blocks
		        bool isInBlock = false;
		        for (int i = 0; i < mBlocks.Count(); ++i) {

			        // set the new block position
			        mBlocks[i].mX = mBlocks[i].mX + blockSpeed * (float)secondsElapsed;

			        // check which block is current (in line with the X cursor position)
			        if (!isInBlock && cursorX >= mBlocks[i].mX && cursorX <= mBlocks[i].mX + mBlocks[i].mWidth) {

				        // set as current block
				        mCurrentBlock = i;

				        // check whether the cursor is in the current block
				        mCursorInCurrentBlock = (cursorY >= mBlocks[i].mY && cursorY <= mBlocks[i].mY + mBlocks[i].mHeight);

				        // set the inblock flag
				        isInBlock = true;

			        }

		        }

		        // if no block was hit, set the current block to no block
		        if (!isInBlock)		mCurrentBlock = noBlock;

            }
        }

        protected override void render() {

            // load textures (if needed; done here because it should be from this thread/context)
            if (doLoadTextures > 0)     loadTextures();

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

	        // draw the blocks
	        if (showBlocks) {

		        // set the blockcolor to white
		        glColor3(1f, 1f, 1f);

		        // loop through the blocks
		        for (int i = 0; i < mBlocks.Count(); ++i) {

			        // skip block which are out of display
			        if (mBlocks[i].mX + mBlocks[i].mWidth < 0)    	continue;
			        if (mBlocks[i].mX > getContentWidth())				continue;

			        // bind the texture, also if it is 0
			        // (could use glEnable(GL_TEXTURE_2D) and glDisable(GL_TEXTURE_2D), but binding to zero for untextured block can be used as well)
                    glBindTexture2D(mBlocks[i].mTexture);
                    
			        // draw the block
                    glBeginTriangles();
	
				        // vertex 0
				        glTexCoord2(1.0f, 1.0f);
				        glVertex3( mBlocks[i].mX + mBlocks[i].mWidth,	mBlocks[i].mY + mBlocks[i].mHeight,    	0.0f);

				        glTexCoord2(1.0f, 0.0f);
				        glVertex3( mBlocks[i].mX + mBlocks[i].mWidth,	mBlocks[i].mY,							0.0f);
			
				        glTexCoord2(0.0f, 0.0f);
				        glVertex3( mBlocks[i].mX,						mBlocks[i].mY,							0.0f);

				        //vertex 1
				        glTexCoord2(0.0f, 1.0f);
				        glVertex3( mBlocks[i].mX,						mBlocks[i].mY + mBlocks[i].mHeight,	0.0f);

				        glTexCoord2(1.0f, 1.0f);
				        glVertex3( mBlocks[i].mX + mBlocks[i].mWidth,	mBlocks[i].mY + mBlocks[i].mHeight,	0.0f);

				        glTexCoord2(0.0f, 0.0f);
				        glVertex3( mBlocks[i].mX,						mBlocks[i].mY,							0.0f);

			        glEnd();

		        }

	        }
            
	        // draw the cursor
	        if (showCursor) {
                
		        // set the cursor color, no texture
		        if (cursorColorSetting == 2)																	// manual escape
			        glColor3(cursorEscapeColor.getRed(), cursorEscapeColor.getGreen(), cursorEscapeColor.getBlue());
		        else if ((cursorColorSetting == 1) || (cursorColorSetting == 3 && mCursorInCurrentBlock))		// manual hit or automatic
			        glColor3(cursorHitColor.getRed(), cursorHitColor.getGreen(), cursorHitColor.getBlue());
		        else																							// other (manual miss)
			        glColor3(cursorMissColor.getRed(), cursorMissColor.getGreen(), cursorMissColor.getBlue());
                glBindTexture2D(0);

		        // cursor polygon
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




        private void loadTextures() {
            
            // lock for textures events (thread safety)
            lock(textureLock) {
            
	            // clear all the texture references in the block array
	            for (int i = 0; i < mBlocks.Count(); ++i)
                    mBlocks[i].mTexture = 0;
                
	            // delete existing textures, clear the array and resize to fit the new ones
	            for (int i = 0; i < blockTextures.Count(); ++i)
                    glDeleteTexture(blockTextures[i]);
                blockTextures.Clear();
                blockTextures = new List<int>(new int[blockTexturesToLoad.Count()]);

	            // load the new block textures (if possible)
	            for (int i = 0; i < blockTexturesToLoad.Count(); ++i) {
		
		            // initialy set to 0 (also reserved by openGL as a no texture pointer)
		            blockTextures[i] = 0;
                    
		            // if no valid filepath, continue to next
                    if (String.IsNullOrEmpty(blockTexturesToLoad[i]))  continue;
                    
		            // load the file to texture
		            // (GLFW3 does not support image loading anymore, since there are
		            //  better libraries to do that, so we have to have our own function)
                    blockTextures[i] = (int)loadImage(blockTexturesToLoad[i]);
		
	            }

	            // clear the texture to be loaded array
	            blockTexturesToLoad.Clear();

	            // the textures were loaded for at least this call
	            doLoadTextures--;

            }

        }

        public void initBlockSequence(List<int> inTargetSequence, List<List<float>> inTargets) {
            
            // wait until the textures are loaded before continuing
            while (doLoadTextures > 0) Thread.Sleep(50);
            
            // lock for textures events (thread safety)
            lock(textureLock) {
	            
                // 
                int i = 0;

	            // clear all block sequence information
	            for (i = 0; i < mBlocks.Count(); ++i)   mBlocks[i].mTexture = 0;
	            mBlocks.Clear();

	            // set the block variables nothing
	            mCurrentBlock = noBlock;
	            mCursorInCurrentBlock = false;

	            // loop through the targets in the sequence
	            float startX = 0;
	            for (i = 0; i < inTargetSequence.Count(); ++i) {

		            // calculate the block height and y position (based on percentages)
		            float height = getContentHeight() * (inTargets[1][inTargetSequence[i]] / 100.0f);
		            float y = getContentHeight() * (inTargets[0][inTargetSequence[i]] / 100.0f) - height / 2.0f;

		            // calculate the block width (based on sec)
		            float widthSec = inTargets[2][inTargetSequence[i]];
		            float widthPixels = (float)getContentWidth() / ((float)getContentWidth() / blockSpeed) * widthSec;

		            // set the start position
		            startX -= widthPixels;

		            // create a block object
		            FollowBlock block = new FollowBlock(startX, y, widthPixels, height);
                    
		            // set the block texture (initialized to 0 = no texture)
		            if (inTargetSequence[i] < (int)blockTextures.Count())
			            block.mTexture = blockTextures[inTargetSequence[i]];

		            // add block for display
		            mBlocks.Add(block);

	            }
            }
        }

        public void setBlocksMove(bool move) {
	        mBlocksMove = move;
        }

        public void setBlocksVisible(bool show) {
	        showBlocks = show;
        }

        public void setBlockSpeed(float speed) {
	        blockSpeed = speed;
        }

        public int getCurrentBlock() {
	        return mCurrentBlock;
        }

        public bool getCursorInCurrentBlock() {
	        return mCursorInCurrentBlock;
        }

        public int getCursorY() {
	        return cursorY;
        }

        // This function does not actually load the textures but instead
        // tells the thread to load them. Only the loop thread (which in this case
        // is the openGL context can use glGenTexture to succesfully create textures).
        // This function is called from a different thread so, the textures cannot be loaded
        // from here but will instead be loaded from the loop
        public void initBlockTextures(List<string> inTargetTextures) {
            
            // wait while textures are still being loaded (thread safety)
            lock(textureLock) {
                
	            // clear the array and size it
	            blockTexturesToLoad.Clear();
                blockTexturesToLoad = new List<string>(new string[inTargetTextures.Count()]);

	            // loop through the textures
	            for (int i = 0; i < (int)inTargetTextures.Count(); ++i) {
		
		            // set empty string initially
		            blockTexturesToLoad[i] = "";

		            // check the file
		            string texturePath = inTargetTextures[i];
                    
		            if (String.IsNullOrEmpty(texturePath))		continue;
                    if (!File.Exists(texturePath)) {
                        logger.Error("Could not find target texture file (" + texturePath + "), will be empty (white)");
                        continue;
                    }

		            // set the path
		            blockTexturesToLoad[i] = texturePath;

	            }

	            // allow the textures to be loaded
	            doLoadTextures++;

            }

        }

        public void centerCursor() {
	        cursorX = (getContentWidth() - cursorRadius) / 2;
	        cursorY = (getContentHeight() - cursorRadius) / 2;
        }

        public void setCursorX(int x) {
	        cursorX = x;
        }

        // set the cursor's y position to a normalized valued (0 = bottom, 1 = top, also takes cursor radius into account)
        public void setCursorNormY(double y) {
	        if (y < 0) y = 0;
	        if (y > 1) y = 1;
	        cursorY = getContentHeight() - cursorRadius - (int)((getContentHeight() - cursorRadius * 2) * y);
        }

        public void setCursorY(int y) {
	        if (y < cursorRadius)					y = cursorRadius;
	        if (y > getContentHeight() - cursorRadius)	    y = getContentHeight() - cursorRadius;
	        cursorY = y;
        }

        public void setCursorVisible(bool show) {
	        showCursor = show;
        }

        public void setCursorHitColor(float red, float green, float blue) {
            cursorHitColor = new RGBColorFloat(red, blue, green);
        }

        public void setCursorHitColor(RGBColorFloat color) {
            cursorHitColor = color;
        }

        public void setCursorMissColor(float red, float green, float blue) {
            cursorMissColor = new RGBColorFloat(red, blue, green);
        }
        
        public void setCursorMissColor(RGBColorFloat color) {
            cursorMissColor = color;
        }

        public void setCursorEscapeColor(float red, float green, float blue) {
            cursorEscapeColor = new RGBColorFloat(red, blue, green);
        }
        
        public void setCursorEscapeColor(RGBColorFloat color) {
            cursorEscapeColor = color;
        }

        // set the cursor size radius as a percentage of the screen height
        public void setCursorSizePerc(double perc) {
	        setCursorSize((int)(getContentHeight() / 100.0 * perc));
        }

        public void setCursorSize(int radius) {
	        if (radius * 2 > getContentHeight())		radius = getContentHeight() / 2;
	        cursorRadius = radius;
        }

        // set whether the cursor is hitting (0 = manual miss, 1 = manual hit, 2 = manual escape, 3 = automatic by mCursorInCurrentBlock)
        public void setCursorColorSetting(int colorSetting) {
	        cursorColorSetting = colorSetting;
        }

        public void setCountDown(int count) {
	        showCountDown = count;
        }

        public void setFixation(bool fix) {
	        showFixation = fix;
        }

        public void setScore(long newScore) {
	        score = newScore;
        }

        public bool resourcesLoaded() {
            return (isStarted() && doLoadTextures == 0);
        }

        public float[] getBlockPositions() {

            float[] blockPositions = new float[mBlocks.Count];
	        for (int i = 0; i < mBlocks.Count; ++i)     blockPositions[i] = mBlocks[i].mX;
            return blockPositions;

        }

        public void setBlockPositions(float[] blockPositions) {
	
	        // set stored block positions
	        for (int i = 0; i < mBlocks.Count; ++i)     mBlocks[i].mX = blockPositions[i];

        }


    }

}
