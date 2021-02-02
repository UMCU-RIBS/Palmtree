/**
 * The SpellerView class
 * 
 * ...
 * 
 * 
 * Copyright (C) 2017:  RIBS group (Nick Ramsey Lab), University Medical Center Utrecht (The Netherlands) & external contributors
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
using Palmtree.Core;
using Palmtree.Views;

namespace SpellerTask {

    /// <summary>
    /// The <c>SpellerView</c> class.
    /// 
    /// ...
    /// </summary>
    public class SpellerView : OpenTKView, IView {

        // fundamentals
        private static Logger logger = LogManager.GetLogger("SpellerView");             // the logger object for the view
        private Object textureLock = new Object();                                      // threadsafety lock for texture events
        private bool showConnectionLost = false;

        // task specific
        private int score = -1;									                        // the score that is being shown (-1 = do not show score)
        private bool showScore = false;

        // visual elements
		private int exitTexture = 0;
        private int connectionLostTexture = 0;
        private bool showFixation = false;                                              // whether the fixation should be shown
        private int showCountDown = -1;                                                 // whether the countdown should be shown (-1 = off, 1..3 = count)
        private string instructionText = "";                                            // to display instructions
        private int instructionTextWidth = 0;                                           // width of instructions
        private string cueText = "";                                                    // to display cues
        private int cueTextWidth = 0;                                                   // width of cues
        private string inputText = "";                                                  // displaying given input
        private int inputTextWidth = 0;                                                 // width of input text

        // grid
        private List<SpellerCell> taskCells = new List<SpellerCell>(0);	                // SpellerCell objects
		private int selectionX = 0;
		private int selectionY = 0;
		private int selectionWidth = 0;
		private int selectionHeight = 0;
		private int holeSize = 0;
		private int spacing = 0;
		private int holeRows = 0;
		private int holeColumns = 0;
		private int holeOffsetX = 0;
		private int holeOffsetY = 0;
		private bool mSelected = false;
        private bool showGrid = false;

        // fonts
        private glFreeTypeFont scoreFont = new glFreeTypeFont();
		private glFreeTypeFont fixationFont = new glFreeTypeFont();
		private glFreeTypeFont countdownFont = new glFreeTypeFont();
        private glFreeTypeFont textFont = new glFreeTypeFont();
        private glFreeTypeFont inputFont = new glFreeTypeFont();
        private int longestCue = -1;
        private uint textFontSize = 0;
        private uint cueTextY = 0;                                      // standard Y location of cue text

        // font parameters
        private string inputFontFont = "fonts\\ariblk.ttf";
        private uint inputFontheight = 20;
        private string inputFontInputs = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ._ ";


        public SpellerView() : base(120, 0, 0, 640, 480, true) {
            
        }

        public SpellerView(int updateFrequency, int x, int y, int width, int height, bool border) : base(updateFrequency, x, y, width, height, border) {
            
        }

        protected override void load() {
            
            // standard font size
            textFontSize = (uint)(getContentHeight() / 20);
            cueTextY = textFontSize;

            // init textfont to standard dimensions
            textFont.init(this, "fonts\\ariblk.ttf", textFontSize, "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789%?._ ");

            // if we have information about the length of the longest cue, check if standard dimensions allow all cues to be displayed properly. Do so by testing if longest cue, if existing of wide characters ('W'), fits in the view. If too large, adjust font size
            if (longestCue != -1) {
                int longestWidth = textFont.getTextWidth(new String('W', longestCue));
                if (longestWidth > getContentWidth()) {

                    // get proportion of current size by which the font is too small
                    double decreaseFactor = (double)(longestWidth - getContentWidth()) / (double)longestWidth;
                    textFontSize = (uint)Math.Round((double)textFontSize * decreaseFactor);

                    // re-init textFont
                    textFont.init(this, "fonts\\ariblk.ttf", textFontSize, "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789%?._ ");
                }
            }

            // initialize the countdown, text and fixation fonts
            countdownFont.init(this, "fonts\\ariblk.ttf", (uint)(getContentHeight() / 7), "1234567890");
            fixationFont.init(this, "fonts\\ariblk.ttf", (uint)(getContentHeight() / 10), "+");

            // initialize the score font
            scoreFont.init(this, "fonts\\ariblk.ttf", (uint)(getContentHeight() / 30), "Score: 0123456789");

            // initialize the text font
            inputFont.init(this, inputFontFont, (uint)(getContentHeight() / inputFontheight), inputFontInputs);

            // lock for textures events (thread safety)
            lock (textureLock) {

                // load the connection lost texture
                connectionLostTexture = (int)loadImage("images\\nosignal.png");

                // Load textures
                exitTexture = (int)loadImage("images\\exit.png");
            }

        }

        protected override void unload() {

            // clear the fonts
            textFont.clean();
            scoreFont.clean();
            fixationFont.clean();
            countdownFont.clean();

            // lock for textures events (thread safety)
            lock(textureLock) {

                // clear the no signal texture
                glDeleteTexture(connectionLostTexture);

                // clear the task textures
                glDeleteTexture(exitTexture); 
            }

        }

        protected override void resize(int width, int height) {
        }

        protected override void update(double secondsElapsed) {
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

            // TODO, use bool instead of value to determine whether to draw
            // check if countdown should be shown
	        if (showCountDown >= 0) {
                
		        // set the countdown to white
		        glColor3(1f, 1f, 1f);

		        // set the text count
                int countTextWidth = countdownFont.getTextWidth(showCountDown.ToString());
                countdownFont.printLine((int)((getContentWidth() - countTextWidth) / 2), (int)((getContentHeight() - countdownFont.height) / 2), showCountDown.ToString());
	        }
	
	        // Check if we should draw grid
	        if(showGrid) {
                
		        // loop through the holes	
		        for (int i = 0; i < taskCells.Count; i++) {

			        // retrieve hole reference
			        SpellerCell cell = taskCells[i];
                    
                    // if cell is of a type that requires drawing
			        if (cell.cellType == SpellerCell.CellType.Input || cell.cellType == SpellerCell.CellType.Exit || cell.cellType == SpellerCell.CellType.Backspace) {

				        // set white color for drawing
				        glColor3(1f, 1f, 1f);

                        // if cell type is input, draw letter, in case of backspace, draw backapace code, in case of exit, draw exit texture
                        if (cell.cellType == SpellerCell.CellType.Input || cell.cellType == SpellerCell.CellType.Backspace) {

                            // get width of content that goes into cell
                            int contentWidth = inputFont.getTextWidth(cell.content);

                            // if too large fro cell, adjust font size and then reset to old values
                            //if (contentWidth > cell.width) {
                            //    double shrinkFactor = cell.width / contentWidth;
                            //    float originalHeight = inputFont.height;
                            //    inputFont.init(this, inputFontFont, (uint)(originalHeight * shrinkFactor), inputFontInputs);
                            //    inputFont.printLine((int)(cell.x + ((cell.width - inputFont.getTextWidth(cell.content)) / 2)), (int)(cell.y + ((cell.height - inputFont.height) / 2)), cell.content);
                            //    inputFont.init(this, inputFontFont, (uint)originalHeight, inputFontInputs);
                            //} else 
                            inputFont.printLine((int)(cell.x + ((cell.width - inputFont.getTextWidth(cell.content)) / 2)), (int)(cell.y + ((cell.height - inputFont.height) / 2)), cell.content);

                        } else if (cell.cellType == SpellerCell.CellType.Exit) {

                            glBindTexture2D(exitTexture);

                            // draw hole
                            glBeginTriangles();

                            // vertex 0
                            glTexCoord2(1.0f, 1.0f);
                            glVertex3(cell.x + cell.width, cell.y + cell.height, 0.0f);

                            glTexCoord2(1.0f, 0.0f);
                            glVertex3(cell.x + cell.width, cell.y, 0.0f);

                            glTexCoord2(0.0f, 0.0f);
                            glVertex3(cell.x, cell.y, 0.0f);

                            //vertex 1
                            glTexCoord2(0.0f, 1.0f);
                            glVertex3(cell.x, cell.y + cell.height, 0.0f);

                            glTexCoord2(1.0f, 1.0f);
                            glVertex3(cell.x + cell.width, cell.y + cell.height, 0.0f);

                            glTexCoord2(0.0f, 0.0f);
                            glVertex3(cell.x, cell.y, 0.0f);

                            glEnd();
                        }
                    }
		        }

		        // check if the selection should be drawn
		        if (selectionWidth != 0 && selectionHeight != 0 ) {
		
			        // set the color
			        float colorR = 1, colorG = 1, colorB = 0;
			        if (mSelected)			colorG = 0;
                    
			        // draw selection
			        drawRectangle(	selectionX, 
							        selectionY,
							        (selectionX + selectionWidth),
							        (selectionY + selectionHeight),
							        5, 
							        colorR, colorG, colorB );
                    
		        }

	        }

            // TODO, use bool instead of value to determine whether to draw
            // write the score text
            if (score > -1 && showScore) {

		        glColor3(1f, 1f, 1f);
                scoreFont.printLine(getContentWidth() - scoreFont.height * 9, 5, ("Score: " + score));

	        }

            // TODO, use bool instead of value to determine whether to draw
            // check if instructions should be shown
            if (instructionText.Length != 0) {

		        // set the text to white
		        glColor3(1f, 1f, 1f);
		
		        // print the text
                textFont.printLine((getContentWidth() - instructionTextWidth) / 2, getContentHeight() / 2, instructionText);

	        }

            // TODO, use bool instead of value to determine whether to draw
            // check if cues should be shown
            if (cueText.Length != 0) {

                // set the text to white
                glColor3(1f, 1f, 1f);

                // print the text
                textFont.printLine((getContentWidth() - cueTextWidth) / 2, Math.Max(spacing + cueTextY, spacing + textFont.height), cueText);            // show line at minum height from above, in case font is small 
            }

            // TODO, use bool instead of value to determine whether to draw
            // check if input should be shown
            if (inputText.Length != 0) {

                // set the text to white
                glColor3(1f, 1f, 1f);

                // print the text
                textFont.printLine((getContentWidth() - cueTextWidth) / 2, (spacing + textFont.height)*3, inputText);

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

        public void setInstructionText(string text) {
	
	        // set the text
	        instructionText = text;

	        // if not empty, determine the width once
            if (!String.IsNullOrEmpty(instructionText))
                instructionTextWidth = textFont.getTextWidth(instructionText);
        }

        public void setCueText(string text) {

            // set the text
            cueText = text;

            // if not empty, determine the width once
            if (!String.IsNullOrEmpty(cueText)) cueTextWidth = textFont.getTextWidth(cueText);
        }

        public void setShowScore(bool show) {
            showScore = show;
        }

        // update the current input text
        public void updateInputText(string text, bool backspace) {

            // if backspace was pressed and there are characters to remove, remove last character; if no backspace is pressed, add inputted character at the end of input text
            if (backspace & inputText.Length > 0) inputText = inputText.Remove(inputText.Length - 1);
            else { inputText = inputText + text; }

            // if not empty, determine the width once
            if (!String.IsNullOrEmpty(inputText))
                inputTextWidth = textFont.getTextWidth(inputText);
        }

        public void resetInputText() {
            inputText = "";
        }

        // read the current input text
        public string getInputText() {
            return inputText;
        }

        public void setConnectionLost(bool connectionLost) {
	        showConnectionLost = connectionLost;
        }


        public void gridVisible(bool visible) {
	        showGrid = visible;	
        }

        public void selectRow(int rowID, bool selected) {

	        // Check if no row should be visible
	        if (rowID == -1) {

		        selectionX = 0;
		        selectionY = 0;
		        selectionWidth = 0;
		        selectionHeight =0;

	        } else {

		        selectionX = holeOffsetX + spacing / 2;
		        selectionY = holeOffsetY + rowID * (holeSize + spacing)  + spacing / 2;
		        selectionWidth = holeColumns * (holeSize + spacing);
		        selectionHeight = holeSize + spacing; 

		        mSelected = selected;

	        }
		
        }

        public void selectCell(int rowID, int columnID, bool selected) {

	        // Check if no row should be visible
	        if (rowID == -1 || columnID == -1) {

		        selectionX = 0;
		        selectionY = 0;
		        selectionWidth = 0;
		        selectionHeight =0;

	        } else {

		        selectionX = holeOffsetX + columnID * (holeSize + spacing) + spacing / 2;
		        selectionY = holeOffsetY + rowID * (holeSize + spacing) + spacing / 2;

		        selectionWidth = holeSize + spacing;
		        selectionHeight = holeSize + spacing;

		        mSelected = selected;

	        }
	
        }

        public void initGridPositions(List<SpellerCell> cells, int holeRows, int holeColumns, int spacing) {

	        // Store pointer to holes array (for drawing later)
	        taskCells = cells;
	
	        // Store hole parameters for drawing later
	        this.holeRows = holeRows;
	        this.holeColumns = holeColumns;
	        this.spacing = spacing;

            // Calculate maximum possible size of holes, using 0.7 of total height for grid, first 0.3 part of height is reserved for cue and input
            holeOffsetX = 0; holeOffsetY = 0;
	        if  ( getContentWidth() / holeColumns > (0.7 * getContentHeight()) / holeRows )
		        holeSize = (int)Math.Floor((double)(( (0.7 * getContentHeight()) - (spacing * (holeRows + 1)) ) / holeRows));
	        else
		        holeSize = (int)Math.Floor((double)(( getContentWidth() - (spacing * (holeColumns + 1)) ) / holeColumns));

	        // set the x and y offset. x offset is determined by centering the holes around the horizontal center, y offset is determined by the fact that the first 0.3 part of height is reserved for cue and input, so grid starts here
	        holeOffsetX =  (getContentWidth() - holeColumns * holeSize - spacing * (holeColumns+1)) / 2;
            //holeOffsetY =  getContentHeight() - holeRows * holeSize - spacing * (holeRows+1);
            holeOffsetY = (int)(0.3 * getContentHeight());

            // Loop through the holes
            for (int i = 0; i < cells.Count; i++) {

		        // calculate the row and column index (0 based)
		        int row = (int)Math.Floor((double)(i / holeColumns));
		        int column = i - (row * holeColumns);

		        // retrieve the reference to the hole
		        SpellerCell cell = cells[i];
		
		        // Set position and size
		        cell.x = holeOffsetX + spacing + column * (holeSize + spacing);
		        cell.y = holeOffsetY + spacing + row * (holeSize + spacing);

		        cell.height = holeSize;
		        cell.width = holeSize;

	        }	
	
        }

        public void setScore(int newScore) {
	        score = newScore;
        }

        public void setFixation(bool fix) {
	        showFixation = fix;
        }

        public void setCountDown(int count) {
	        showCountDown = count;
        }

        public void setLongestCue(int l) {
            longestCue = l;
        }

        public bool resourcesLoaded() {
            return isStarted();		    // in this task resources are loaded upon initialization of the scene (not on the fly during the scene loop), so this suffices
        }

    }

}
