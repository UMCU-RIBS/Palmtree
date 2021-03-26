/**
 * The LocalizerView class
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
using Palmtree.Core;
using Palmtree.Views;

namespace LocalizerTask {

    /// <summary>
    /// The <c>LocalizerView</c> class.
    /// 
    /// ...
    /// </summary>
    class LocalizerView : OpenTKView, IView {
        
        // fundamentals
        private static Logger logger = LogManager.GetLogger("LocalizerView");                           // the logger object for the view
        private Object textureLock = new Object();                                                      // threadsafety lock for texture events
        private bool showConnectionLost;                                                                // whether the connection is lost

        // visual elements
        private string showText = "";                                                                   // text being shown in middle of screen. Used to present instructions, cues and stimuli
        private int showTextWidth = 0;                                                                  // width of text
        private glFreeTypeFont showTextFont = new glFreeTypeFont();                                     // font of text
        private int connectionLostTexture = 0;                                                          // texture for image shown when connection is lost

        public LocalizerView() : base(120, 0, 0, 640, 480, true) {}

        public LocalizerView(int updateFrequency, int x, int y, int width, int height, bool border) : base(updateFrequency, x, y, width, height, border) {}

        protected override void load() {

            // initialize the showText font
            showTextFont.init(this, AppDomain.CurrentDomain.BaseDirectory + "\\fonts\\ariblk.ttf", (uint)(getContentHeight() / 20), @"abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 .,?!:\=+*#@-/()<>");
            
            // lock for textures events (thread safety)
            lock (textureLock) {

                // load the connection lost texture
                connectionLostTexture = (int)loadImage(AppDomain.CurrentDomain.BaseDirectory + "\\images\\nosignal.png");
            }
        }

        protected override void unload() {

            // clear the used fonts
            showTextFont.clean();

            // clear the textures
            lock (textureLock) {

                // clear the no signal texture
                glDeleteTexture(connectionLostTexture);
            }
        }

        protected override void resize(int width, int height) {}

        protected override void update(double secondsElapsed) {}

        protected override void render() {

            // check if text should be shown
            if (showText.Length != 0) {

                // set the color to white
                glColor3(1f, 1f, 1f);

                // print the text in the middle of the view
                showTextFont.printLine((getContentWidth() - showTextWidth) / 2, getContentHeight() / 2, showText);
            }


            // check if there is no signal
            if (showConnectionLost) {

                // set white color for drawing
                glColor3(1f, 1f, 1f);

                // print text
                int textWidth = showTextFont.getTextWidth("Lost connection with device");
                showTextFont.printLine((int)((getContentWidth() - textWidth) / 2), (int)((getContentHeight()) / 4), "Lost connection with device");

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

            // if not empty, determine the width
            if (!String.IsNullOrEmpty(showText))
                showTextWidth = showTextFont.getTextWidth(showText);

        }

        public void setConnectionLost(bool connectionLost) {
            showConnectionLost = connectionLost;
        }

    }

}
