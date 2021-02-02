/**
 * The FollowView class
 * 
 * ...
 * 
 * 
 * Copyright (C) 2017:  RIBS group (Nick Ramsey Lab), University Medical Center Utrecht (The Netherlands) & external contributors
 * Author(s):           Max van den Boom            (info@maxvandenboom.nl)
 * 
 * This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software
 * Foundation, either version 3 of the License, or (at your option) any later version. This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for
 * more details. You should have received a copy of the GNU General Public License along with this program. If not, see <https://www.gnu.org/licenses/>.
 */
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Palmtree.Core;
using Palmtree.Core.Helpers;
using Palmtree.Views;

namespace FollowTask {

    /// <summary>
    /// The <c>FollowView</c> class.
    /// 
    /// ...
    /// </summary>
    public class FollowView : OpenTKView, IView {
    //public class FollowView : SharpGLView, IView {

        public const int noBlock = -1;

        private static Logger logger = LogManager.GetLogger("FollowView");              // the logger object for the view

        private Object textureLock = new Object();                                      // threadsafety lock for texture events
	    
		private byte doLoadTextures = 0;								                // load textures in the loop thread (flag to check for new textures. Numeric because while textures are being loaded, the thread can make multiple function calls to load)
		private List<string> blockTexturesToLoad = new List<string>(0);                 // array with block textures filepath that should be loaded (from the loop thread)
		private List<int> blockTextures = new List<int>(0);	                            // array with possible block textures
		private List<FollowBlock> blocks = new List<FollowBlock>(0);	                // block objects
		private bool blocksMove = false;								                // enable/disable block movement
		private float blockSpeed = 0;									                // the speed of the movement of the block (in pixels per second)
		private bool showBlocks = false;								                // show the blocks
		private int currentBlock = noBlock;							                    // the current block which is in line with X of the cursor (so the middle)
		private bool cursorInCurrentBlock = false;						                // hold whether the cursor is inside of the current block
		
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
		private long score = -1;									                    // the score that is being shown (-1 = do not show score)

        private glFreeTypeFont scoreFont = new glFreeTypeFont();
        private glFreeTypeFont countdownFont = new glFreeTypeFont();
        private glFreeTypeFont fixationFont = new glFreeTypeFont();

        // general Palmtree variables
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
	            for (int i = 0; i < (int)blocks.Count; ++i)  blocks[i].texture = 0;
	            blocks.Clear();

	            // clear all textures
	            for (int i = 0; i < (int)blockTextures.Count; ++i)
                    glDeleteTexture(blockTextures[i]);
	            blockTextures.Clear();

            }
        }

        protected override void resize(int width, int height) {
            

        }

        protected override void update(double secondsElapsed) {

	        // check if the blocks should move
	        if (blocksMove) {

		        // loop through the blocks
		        bool isInBlock = false;
		        for (int i = 0; i < blocks.Count; ++i) {

			        // set the new block position
			        blocks[i].x = blocks[i].x + blockSpeed * (float)secondsElapsed;

			        // check which block is current (in line with the X cursor position)
			        if (!isInBlock && cursorX >= blocks[i].x && cursorX <= blocks[i].x + blocks[i].width) {

				        // set as current block
				        currentBlock = i;

				        // check whether the cursor is in the current block
				        cursorInCurrentBlock = (cursorY >= blocks[i].y && cursorY <= blocks[i].y + blocks[i].height);

				        // set the inblock flag
				        isInBlock = true;

			        }

		        }

		        // if no block was hit, set the current block to no block
		        if (!isInBlock)		currentBlock = noBlock;

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
		        for (int i = 0; i < blocks.Count; ++i) {

			        // skip block which are out of display
			        if (blocks[i].x + blocks[i].width < 0)    	continue;
			        if (blocks[i].x > getContentWidth())				continue;

			        // bind the texture, also if it is 0
			        // (could use glEnable(GL_TEXTURE_2D) and glDisable(GL_TEXTURE_2D), but binding to zero for untextured block can be used as well)
                    glBindTexture2D(blocks[i].texture);
                    
			        // draw the block
                    glBeginTriangles();
	
				        // vertex 0
				        glTexCoord2(1.0f, 1.0f);
				        glVertex3( blocks[i].x + blocks[i].width,	blocks[i].y + blocks[i].height,    	0.0f);

				        glTexCoord2(1.0f, 0.0f);
				        glVertex3( blocks[i].x + blocks[i].width,	blocks[i].y,							0.0f);
			
				        glTexCoord2(0.0f, 0.0f);
				        glVertex3( blocks[i].x,						blocks[i].y,							0.0f);

				        //vertex 1
				        glTexCoord2(0.0f, 1.0f);
				        glVertex3( blocks[i].x,						blocks[i].y + blocks[i].height,	0.0f);

				        glTexCoord2(1.0f, 1.0f);
				        glVertex3( blocks[i].x + blocks[i].width,	blocks[i].y + blocks[i].height,	0.0f);

				        glTexCoord2(0.0f, 0.0f);
				        glVertex3( blocks[i].x,						blocks[i].y,							0.0f);

			        glEnd();

		        }

	        }
            
	        // draw the cursor
	        if (showCursor) {
                
		        // set the cursor color, no texture
		        if (cursorColorSetting == 2)																	// manual escape
			        glColor3(cursorEscapeColor.getRed(), cursorEscapeColor.getGreen(), cursorEscapeColor.getBlue());
		        else if ((cursorColorSetting == 1) || (cursorColorSetting == 3 && cursorInCurrentBlock))		// manual hit or automatic
			        glColor3(cursorHitColor.getRed(), cursorHitColor.getGreen(), cursorHitColor.getBlue());
		        else																							// other (manual miss)
			        glColor3(cursorMissColor.getRed(), cursorMissColor.getGreen(), cursorMissColor.getBlue());
                glBindTexture2D(0);

		        // cursor polygon
                glBeginPolygon();
			        for(double i = 0; i < 2 * Math.PI; i += Math.PI / 24)
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




        private void loadTextures() {
            
            // lock for textures events (thread safety)
            lock(textureLock) {
            
	            // clear all the texture references in the block array
	            for (int i = 0; i < blocks.Count; ++i)
                    blocks[i].texture = 0;
                
	            // delete existing textures, clear the array and resize to fit the new ones
	            for (int i = 0; i < blockTextures.Count; ++i)
                    glDeleteTexture(blockTextures[i]);
                blockTextures.Clear();
                blockTextures = new List<int>(new int[blockTexturesToLoad.Count]);

	            // load the new block textures (if possible)
	            for (int i = 0; i < blockTexturesToLoad.Count; ++i) {
		
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

        public void initBlockSequence(List<int> inTrialSequence, List<List<float>> inTargets) {
            
            // wait until the textures are loaded before continuing
            while (doLoadTextures > 0) Thread.Sleep(50);
            
            // lock for textures events (thread safety)
            lock(textureLock) {
	            
                // 
                int i = 0;

	            // clear all block sequence information
	            for (i = 0; i < blocks.Count; ++i)   blocks[i].texture = 0;
	            blocks.Clear();

	            // set the block variables nothing
	            currentBlock = noBlock;
	            cursorInCurrentBlock = false;

	            // loop through the trials in the sequence
	            float startX = 0;
	            for (i = 0; i < inTrialSequence.Count; ++i) {

		            // calculate the block height and y position (based on percentages)
		            float height = getContentHeight() * (inTargets[1][inTrialSequence[i]] / 100.0f);
		            float y = getContentHeight() * (inTargets[0][inTrialSequence[i]] / 100.0f) - height / 2.0f;

		            // calculate the block width (based on sec)
		            float widthSec = inTargets[2][inTrialSequence[i]];
		            float widthPixels = (float)getContentWidth() / ((float)getContentWidth() / blockSpeed) * widthSec;

		            // set the start position
		            startX -= widthPixels;

		            // create a block object
		            FollowBlock block = new FollowBlock(startX, y, widthPixels, height);
                    
		            // set the block texture (initialized to 0 = no texture)
		            if (inTrialSequence[i] < (int)blockTextures.Count)
			            block.texture = blockTextures[inTrialSequence[i]];

		            // add block for display
		            blocks.Add(block);

	            }
            }
        }

        public void setBlocksMove(bool move) {
	        blocksMove = move;
        }

        public void setBlocksVisible(bool show) {
	        showBlocks = show;
        }

        public void setBlockSpeed(float speed) {
	        blockSpeed = speed;
        }

        public int getCurrentBlock() {
	        return currentBlock;
        }

        public bool getCursorInCurrentBlock() {
	        return cursorInCurrentBlock;
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
                blockTexturesToLoad = new List<string>(new string[inTargetTextures.Count]);

	            // loop through the textures
	            for (int i = 0; i < (int)inTargetTextures.Count; ++i) {
		
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

            float[] blockPositions = new float[blocks.Count];
	        for (int i = 0; i < blocks.Count; ++i)     blockPositions[i] = blocks[i].x;
            return blockPositions;

        }

        public void setBlockPositions(float[] blockPositions) {
	
	        // set stored block positions
	        for (int i = 0; i < blocks.Count; ++i)     blocks[i].x = blockPositions[i];

        }


    }

}
