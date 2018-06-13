/**
 * The CWAMView class
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
using UNP.Core;
using UNP.Views;

namespace continuousWAM {

    /// <summary>
    /// The <c>CWAMView</c> class.
    /// 
    /// ...
    /// </summary>
    public class CWAMView : OpenTKView, IView {
    //class CWAMView : SharpGLView, IView {

        private static Logger logger = LogManager.GetLogger("CWAMView");                // the logger object for the view

        private Object textureLock = new Object();                                      // threadsafety lock for texture events

		private int holeTexture = 0;
		private int moleTexture = 0;
		private int exitTexture = 0;
        private int escapeTexture = 0;
        private int moleLaughTexture = 0;
        private int moleWhackedTexture = 0;
        private List<MoleCell> taskCells = new List<MoleCell>(0);	                // MoleCell objects
		private int selectionX = 0;
		private int selectionY = 0;
		public int selectionWidth = 0;
		private int selectionHeight = 0;
		private int holeSize = 0;
		private int spacing = 0;
		private int holeRows = 0;
		private int holeColumns = 0;
		private int holeOffsetX = 0;
		private int holeOffsetY = 0;
        private int escapeOffsetX = 0;
        private int escapeOffsetY = 0;
        private bool selected = false;
		private bool showGrid = false;
        private bool showEscape = false;
		private bool showFixation = false;								    // whether the fixation should be shown
		private int showCountDown = -1;									    // whether the countdown should be shown (-1 = off, 1..3 = count)

        private bool showScore = false;
        private int maxScoreRows = 2;
        private int scoreCellsPerRow = 0;
        private int scoreRows = 0;
        private int scoreCols = 0;
        private int scoreCellWidth = 0;
        private int scoreCellHeight = 0;
        private int minScoreCellWidth = 100;
        private int minScoreCellHeight = 100;
        private int maxScoreCellWidth = 0;
        private int maxScoreCellHeight = 0;
        private int scoreGridOffsetX = 0;
        private int scoreGridOffsetY = 0;
        private int totalScoresOnScreen = 0;

        List<continuousWAM.scoreTypes> posAndNegs = new List<continuousWAM.scoreTypes>(0);


        private glFreeTypeFont scoreFont = new glFreeTypeFont();
		private glFreeTypeFont fixationFont = new glFreeTypeFont();
		private glFreeTypeFont countdownFont = new glFreeTypeFont();
		private int score = 0;									            

        // general UNP variables
        private bool showConnectionLost = false;
        private int connectionLostTexture = 0;
        private glFreeTypeFont textFont = new glFreeTypeFont();
        private string showText = "";
        private int showTextWidth = 0;



        public CWAMView() : base(120, 0, 0, 640, 480, true) {
            
        }

        public CWAMView(int updateFrequency, int x, int y, int width, int height, bool border) : base(updateFrequency, x, y, width, height, border) {
            
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
            scoreFont.init(this, "fonts\\ariblk.ttf", (uint)(getContentHeight() / 30), "Score: 0123456789%");

            // lock for textures events (thread safety)
            lock(textureLock) {

                // load the connection lost texture
                connectionLostTexture = (int)loadImage("images\\nosignal.png");

                // Load textures
                holeTexture = (int)loadImage("images\\hole.png");
                moleTexture = (int)loadImage("images\\mole.png");
                exitTexture = (int)loadImage("images\\exit.png");
                escapeTexture = (int)loadImage("images\\escape.png");
                moleLaughTexture = (int)loadImage("images\\mole_new_laughing2.png");
                moleWhackedTexture = (int)loadImage("images\\mole_new_whacked2.png");


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

                // clear the task textures
                glDeleteTexture(holeTexture);
                glDeleteTexture(moleTexture);
                glDeleteTexture(exitTexture);
                //glDeleteTexture(hammerTexture);
	            
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

	        if (showCountDown >= 0) {
                
		        // set the countdown to white
		        glColor3(1f, 1f, 1f);

		        // set the text count
                int countTextWidth = countdownFont.getTextWidth(showCountDown.ToString());
                countdownFont.printLine((int)((getContentWidth() - countTextWidth) / 2), (int)((getContentHeight() - countdownFont.height) / 2), showCountDown.ToString());

	        }
	
            if(showEscape) {

                // set white color for drawing
                glColor3(1f, 1f, 1f);

                // set escape texture
                glBindTexture2D(escapeTexture);

                // draw texture
                glBeginTriangles();

                    // vertex 0
                    glTexCoord2(1.0f, 1.0f);
                    glVertex3(escapeOffsetX + holeSize, escapeOffsetY + holeSize, 0.0f);

                    glTexCoord2(1.0f, 0.0f);
                    glVertex3(escapeOffsetX + holeSize, escapeOffsetY, 0.0f);

                    glTexCoord2(0.0f, 0.0f);
                    glVertex3(escapeOffsetX, escapeOffsetY, 0.0f);

                    //vertex 1
                    glTexCoord2(0.0f, 1.0f);
                    glVertex3(escapeOffsetX, escapeOffsetY + holeSize, 0.0f);

                    glTexCoord2(1.0f, 1.0f);
                    glVertex3(escapeOffsetX + holeSize, escapeOffsetY + holeSize, 0.0f);

                    glTexCoord2(0.0f, 0.0f);
                    glVertex3(escapeOffsetX, escapeOffsetY, 0.0f);

                glEnd();
            }

	        // Check if we should draw grid
	        if(showGrid) {
                
		        // loop through the holes	
		        for (int i = 0; i < taskCells.Count; i++) {

			        // retrieve hole reference
			        MoleCell cell = taskCells[i];
                    
			        if (cell.type == MoleCell.CellType.Hole || cell.type == MoleCell.CellType.Mole || cell.type == MoleCell.CellType.Exit ) {

				        // set white color for drawing
				        glColor3(1f, 1f, 1f);

				        // set texture
				        if (cell.type == MoleCell.CellType.Hole)
                            glBindTexture2D(holeTexture);
				        else if (cell.type == MoleCell.CellType.Exit)
					        glBindTexture2D(exitTexture);
				        else
					        glBindTexture2D(moleTexture);

				        // draw hole
                        glBeginTriangles();
	
					        // vertex 0
				            glTexCoord2(1.0f, 1.0f);
				            glVertex3(cell.x + cell.width,	cell.y + cell.height,	    0.0f);

				            glTexCoord2(1.0f, 0.0f);
				            glVertex3(cell.x + cell.width,	cell.y,					0.0f);

				            glTexCoord2(0.0f, 0.0f);
				            glVertex3(cell.x,					cell.y,					0.0f);

					        //vertex 1
				            glTexCoord2(0.0f, 1.0f);
				            glVertex3(cell.x,					cell.y + cell.height,	    0.0f);

				            glTexCoord2(1.0f, 1.0f);
				            glVertex3(cell.x + cell.width,	cell.y + cell.height,	    0.0f);

				            glTexCoord2(0.0f, 0.0f);
				            glVertex3(cell.x,					cell.y,					0.0f);

				        glEnd();

			        }

		        }

		        // check if the selection should be drawn
		        if (selectionWidth != 0 && selectionHeight != 0 ) {
		
			        // set the color
			        float colorR = 1, colorG = 1, colorB = 0;
			        if (selected)			colorG = 0;
                    
			        // draw selection
			        drawRectangle(	selectionX, 
							        selectionY,
							        (selectionX + selectionWidth),
							        (selectionY + selectionHeight),
							        5, 
							        colorR, colorG, colorB );
                    
		        }

	        }

            if(showScore) {

                // print score (accuracy)
                glColor3(1f, 1f, 1f);
                scoreFont.printLine(getContentWidth() - scoreFont.height * 9, 5, ("Score: " + score + "%"));

                // get offsets
                int x = scoreGridOffsetX;
                int y = scoreGridOffsetY;

                // determine at which index to begin plotting the scores (it is possible that there are more scores than can fit on screen (scoreRows * scoreCellsPerRow fit on screen), so when screen is full, only new scores are plotted
                int index = (int)Math.Floor(posAndNegs.Count / (double)totalScoresOnScreen) * totalScoresOnScreen;

                // loop through the scores and plot
                for (int i = index; i < posAndNegs.Count; i++) {

                    // to determine if was false or true
                    bool correct = false;

                    // set white color for drawing
                    glColor3(1f, 1f, 1f);

                    // set texture
                    if (posAndNegs[i] == continuousWAM.scoreTypes.FalseNegative)            glBindTexture2D(moleLaughTexture);
                    else if (posAndNegs[i] == continuousWAM.scoreTypes.FalsePositive)       glBindTexture2D(holeTexture);
                    else if (posAndNegs[i] == continuousWAM.scoreTypes.TruePositive)        { glBindTexture2D(moleWhackedTexture); correct = true; }
                    else if (posAndNegs[i] == continuousWAM.scoreTypes.TruePositiveEscape)  { glBindTexture2D(escapeTexture); correct = true; } 
                    else if (posAndNegs[i] == continuousWAM.scoreTypes.FalseNegativeEscape) glBindTexture2D(escapeTexture);

                    else return;

                    // draw hole
                    glBeginTriangles();

                        // vertex 0
                        glTexCoord2(1.0f, 1.0f);
                        glVertex3(x + scoreCellWidth, y + scoreCellHeight, 0.0f);

                        glTexCoord2(1.0f, 0.0f);
                        glVertex3(x + scoreCellWidth, y, 0.0f);

                        glTexCoord2(0.0f, 0.0f);
                        glVertex3(x, y, 0.0f);

                        //vertex 1
                        glTexCoord2(0.0f, 1.0f);
                        glVertex3(x, y + scoreCellHeight, 0.0f);

                        glTexCoord2(1.0f, 1.0f);
                        glVertex3(x + scoreCellWidth, y + scoreCellHeight, 0.0f);

                        glTexCoord2(0.0f, 0.0f);
                        glVertex3(x, y, 0.0f);

                    glEnd();

                    // set color for border, indicating false of true positive or negative
                    float colorR = 1, colorG = 0, colorB = 0;
                    if (correct) { colorR = 0; colorG = 1; colorB = 0; }

                    // draw colored border
                    drawRectangle(x, y, (x + scoreCellWidth), (y + scoreCellHeight), 5, colorR, colorG, colorB);

                    // update x and y coordinates for plotting next scoreItem
                    x = x + scoreCellWidth + spacing;
                        if( (x + scoreCellWidth + spacing) > getContentWidth()) {
                            y = y + scoreCellHeight + spacing;
                            x = scoreGridOffsetX;
                    }

                }

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


        public void setGrid(bool visible) {
	        showGrid = visible;	
        }

        public void setScore(List<continuousWAM.scoreTypes> posAndNegs, int score) {
            this.posAndNegs = posAndNegs;
            this.score = score;
        }

        public void setEscape(bool visible) {
            showEscape = visible;
        }

        public void selectRow(int rowID, bool selected) {

	        // Check if no row should be visible
	        if (rowID == -1) {

		        selectionX = 0;
		        selectionY = 0;
		        selectionWidth = 0;
		        selectionHeight = 0;

            } else {

		        selectionX = holeOffsetX + spacing / 2;
		        selectionY = holeOffsetY + rowID * (holeSize + spacing)  + spacing / 2;
		        selectionWidth = holeColumns * (holeSize + spacing);
		        selectionHeight = holeSize + spacing; 

		        this.selected = selected;

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

		        this.selected = selected;

	        }
	
        }

        public void initGridPositions(List<MoleCell> cells, int holeRows, int holeColumns, int spacing) {

	        // Store pointer to holes array (for drawing later)
	        taskCells = cells;
	
	        // Store hole parameters for drawing later
	        this.holeRows = holeRows;
	        this.holeColumns = holeColumns;
	        this.spacing = spacing;

	        // Calculate maximum possible size of holes
	        holeOffsetX = 0; holeOffsetY = 0;
	        if  ( getContentWidth() / holeColumns > getContentHeight() / holeRows )
		        holeSize = (int)Math.Floor((double)(( getContentHeight() - (spacing * (holeRows + 1)) ) / holeRows));
	        else
		        holeSize = (int)Math.Floor((double)(( getContentWidth() - (spacing * (holeColumns + 1)) ) / holeColumns));

	        // set the x and y offset
	        holeOffsetX =  (getContentWidth() - holeColumns * holeSize - spacing * (holeColumns+1)) / 2;
	        holeOffsetY =  (getContentHeight() - holeRows * holeSize - spacing * (holeRows+1)) / 2;
	
	        // Loop through the holes
	        for(int i = 0; i < cells.Count; i++) {

		        // calculate the row and column index (0 based)
		        int row = (int)Math.Floor((double)(i / holeColumns));
		        int column = i - (row * holeColumns);

		        // retrieve the reference to the hole
		        MoleCell cell = cells[i];
		
		        // Set position and size
		        cell.x = holeOffsetX + spacing + column * (holeSize + spacing);
		        cell.y = holeOffsetY + spacing + row * (holeSize + spacing);

		        cell.height = holeSize;
		        cell.width = holeSize;

	        }

            // calculate the position of the escape cue
            escapeOffsetX = (getContentWidth() - holeSize) / 2;                             // horizontal center 
            escapeOffsetY = (((getContentHeight() - holeOffsetY) / 2) - holeSize) / 2;      // vertical center between top of viewport and top of grid 
        }

        // create a grid that holds an amount of cells equal to two times the amount of moles, to allow space for each TP and FN to be logged, and an expected equal amount of FP's
        public void initScoreGrid(int numberOfMoles, int numberOfEscapes, List<MoleCell> holes) {
            
            // calculate the max height of the cells, and set max width equal to this height:
            // height of the viewport minus height of mole cell divided by two gives space underneath mole grid. Minus spacing needed and divided by max amount of rows gives maximum cell height
            maxScoreCellHeight = (int)Math.Round((((getContentHeight() - holes[0].height) / 2.0) - ((maxScoreRows + 1) * spacing)) / maxScoreRows);
            maxScoreCellWidth = maxScoreCellHeight;

            // calculate the amount of rows, making score cells as large as possible for readability.
            // expect amount of score cells to be equal to two times the amount of moles, to allow space for each TP and FN to be logged, and an expected equal amount of FP's
            scoreRows = Math.Min(maxScoreRows, (int)Math.Ceiling(((spacing + maxScoreCellWidth) * ((numberOfMoles + numberOfEscapes) * 2.0)) / (getContentWidth() - (spacing * maxScoreRows))));


            // calculate score cells per row and total amount that fit on screen
            scoreCellsPerRow = (int)Math.Ceiling(((numberOfMoles + numberOfEscapes) * 2.0) / scoreRows);
            



            

            // calculate final width and height of cell, based on amount of scoreRows
            scoreCellWidth = (int)Math.Floor((getContentWidth() - ((scoreCellsPerRow + 1.0) * spacing)) / scoreCellsPerRow);
            if(scoreCellWidth < minScoreCellWidth) {
                scoreCellWidth = minScoreCellWidth;
                scoreCellsPerRow = (int)Math.Floor(getContentWidth() / ((double)scoreCellWidth + spacing));
            }

            //scoreCellWidth = (int)Math.Floor(Math.Max(minScoreCellWidth, (getContentWidth() - ((scoreCellsPerRow + 1.0) * spacing)) / scoreCellsPerRow));



            scoreCellHeight = scoreCellWidth;
            totalScoresOnScreen = scoreRows * scoreCellsPerRow;

            logger.Info(scoreRows);

            logger.Info(scoreCellsPerRow);

            logger.Info(totalScoresOnScreen);

            // set offsets
            scoreGridOffsetX = spacing;
            scoreGridOffsetY = (int)Math.Ceiling(((getContentHeight() + holes[0].height) / 2.0) +spacing);
        }

        public void viewScore(bool show) {
            showScore = show;
        }

        


        public void setFixation(bool fix) {
	        showFixation = fix;
        }

        public void setCountDown(int count) {
	        showCountDown = count;
        }

        public bool resourcesLoaded() {
            return isStarted();		    // in this task resources are loaded upon initialization of the scene (not on the fly during the scene loop), so this suffices
        }


    }

}
