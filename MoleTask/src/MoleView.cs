/**
 * The MoleView class
 * 
 * ...
 * 
 * 
 * Copyright (C) 2017:  RIBS group (Nick Ramsey Lab), University Medical Center Utrecht (The Netherlands) & external contributors
 * Author(s):           Max van den Boom            (info@maxvandenboom.nl)
 * 
 * Adapted from:        Max van den Boom and Meron Vermaas  (m.vermaas-2@umcutrecht.nl)
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

namespace MoleTask {

    /// <summary>
    /// The <c>MoleView</c> class.
    /// 
    /// ...
    /// </summary>
    public class MoleView : OpenTKView, IView {
    //class MoleView : SharpGLView, IView {

        private static Logger logger = LogManager.GetLogger("MoleView");                // the logger object for the view

        private Object textureLock = new Object();                                      // threadsafety lock for texture events

		private int holeTexture = 0;
		private int moleTexture = 0;
		private int exitTexture = 0;
		private List<MoleCell> moleCells = null;	                        // MoleCell objects
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
		private bool selected = false;
		private bool showGrid = false;
		private bool showFixation = false;								    // whether the fixation should be shown
		private int showCountDown = -1;									    // whether the countdown should be shown (-1 = off, 1..3 = count)

		private glFreeTypeFont scoreFont = new glFreeTypeFont();
		private glFreeTypeFont fixationFont = new glFreeTypeFont();
		private glFreeTypeFont countdownFont = new glFreeTypeFont();
		private int score = -1;									            // the score that is being shown (-1 = do not show score)

        // general Palmtree variables
        private bool showConnectionLost = false;
        private int connectionLostTexture = 0;
        private glFreeTypeFont textFont = new glFreeTypeFont();
        private string showText = "";
        private int showTextWidth = 0;



        public MoleView() : base(120, 0, 0, 640, 480, true) {
            
        }

        public MoleView(int updateFrequency, int x, int y, int width, int height, bool border) : base(updateFrequency, x, y, width, height, border) {
            
        }

        ///////////////////////
        /// task functions
        //////////////////////



        ///////////////////////
        /// openGL load and draw functions
        //////////////////////


        protected override void load() {

            // initialize the text font
            textFont.init(this, AppDomain.CurrentDomain.BaseDirectory + "\\fonts\\ariblk.ttf", (uint)(getContentHeight() / 20), "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ. ");

            // initialize the countdown, text and fixation fonts
            countdownFont.init(this, AppDomain.CurrentDomain.BaseDirectory + "\\fonts\\ariblk.ttf", (uint)(getContentHeight() / 7), "1234567890");
            fixationFont.init(this, AppDomain.CurrentDomain.BaseDirectory + "\\fonts\\ariblk.ttf", (uint)(getContentHeight() / 10), "+");

            // initialize the score font
            scoreFont.init(this, AppDomain.CurrentDomain.BaseDirectory + "\\fonts\\ariblk.ttf", (uint)(getContentHeight() / 30), "Score: 0123456789");

            // lock for textures events (thread safety)
            lock(textureLock) {

                // load the connection lost texture
                connectionLostTexture = (int)loadImage(AppDomain.CurrentDomain.BaseDirectory + "\\images\\nosignal.png");

                // Load textures
                holeTexture = (int)loadImage(AppDomain.CurrentDomain.BaseDirectory + "\\images\\hole.png");
                moleTexture = (int)loadImage(AppDomain.CurrentDomain.BaseDirectory + "\\images\\mole.png");
                exitTexture = (int)loadImage(AppDomain.CurrentDomain.BaseDirectory + "\\images\\exit.png");

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
	
	        // Check if we should draw grid
	        if(showGrid) {
                
		        // loop through the holes	
		        for (int i = 0; i < moleCells.Count; i++) {

			        // retrieve hole reference
			        MoleCell cell = moleCells[i];
                    
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


        public void setGrid(bool visible) {
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

        public void initGridPositions(bool allowExit, int holeRows, int holeColumns, int spacing) {
            
            // calculate the cell holes for the task
            int numHoles = holeRows * holeColumns;

            // create the array of cells for the task
            moleCells = new List<MoleCell>(0);
            for (int i = 0; i < numHoles; i++) {
                if ((i % holeColumns == 0 || i <= holeColumns) && (i != 2 || !allowExit))
                    moleCells.Add(new MoleCell(0, 0, 0, 0, MoleCell.CellType.Empty));
                else if (i == 2 && allowExit)
                    moleCells.Add(new MoleCell(0, 0, 0, 0, MoleCell.CellType.Exit));
                else
                    moleCells.Add(new MoleCell(0, 0, 0, 0, MoleCell.CellType.Hole));
            }
	
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
	        for(int i = 0; i < moleCells.Count; i++) {

		        // calculate the row and column index (0 based)
		        int row = (int)Math.Floor((double)(i / holeColumns));
		        int column = i - (row * holeColumns);

		        // retrieve the reference to the hole
		        MoleCell cell = moleCells[i];
		
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

        // set the mole (-1 is no mole)
        public void setMole(int index) { 
	        for(int i = 0; i < moleCells.Count; i++) {
                if (i == index)     moleCells[i].type = MoleCell.CellType.Mole;
                else                moleCells[i].type = MoleCell.CellType.Hole;
	        }
        }
        
        public bool resourcesLoaded() {
            return isStarted();		    // in this task resources are loaded upon initialization of the scene (not on the fly during the scene loop), so this suffices
        }

    }

}
