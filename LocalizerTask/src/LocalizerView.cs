using NLog;
using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UNP.Views;

namespace LocalizerTask {

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
            showTextFont.init(this, "fonts\\ariblk.ttf", (uint)(getContentHeight() / 20), "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ. ");
            
            // lock for textures events (thread safety)
            lock (textureLock) {

                // load the connection lost texture
                connectionLostTexture = (int)loadImage("images\\nosignal.png");
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

                // print text
                int textWidth = showTextFont.getTextWidth("Lost connection with device");
                showTextFont.printLine((int)((getContentWidth() - textWidth) / 2), (int)((getContentHeight()) / 4), "Lost connection with device");

                // set texture
                glBindTexture2D(connectionLostTexture);

                // set white color for drawing
                glColor3(1f, 1f, 1f);

                // draw texture
                glBeginTriangles();

                // vertex 0
                glTexCoord2(1.0f, 0.0f);
                glVertex3((getContentWidth() - 200) / 2 + 200, (getContentHeight() - 200) / 2 + 200, 0.0f);

                glTexCoord2(1.0f, 1.0f);
                glVertex3((getContentWidth() - 200) / 2 + 200, (getContentHeight() - 200) / 2, 0.0f);

                glTexCoord2(0.0f, 1.0f);
                glVertex3((getContentWidth() - 200) / 2, (getContentHeight() - 200) / 2, 0.0f);

                //vertex 1
                glTexCoord2(0.0f, 0.0f);
                glVertex3((getContentWidth() - 200) / 2, (getContentHeight() - 200) / 2 + 200, 0.0f);

                glTexCoord2(1.0f, 0.0f);
                glVertex3((getContentWidth() - 200) / 2 + 200, (getContentHeight() - 200) / 2 + 200, 0.0f);

                glTexCoord2(0.0f, 1.0f);
                glVertex3((getContentWidth() - 200) / 2, (getContentHeight() - 200) / 2, 0.0f);

                glEnd();

            }

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
