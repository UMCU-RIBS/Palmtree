/**
 * The EmptyView class
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
using UNP.Core;
using UNP.Views;

namespace EmptyTask {

    /// <summary>
    /// The <c>EmptyView</c> class.
    /// 
    /// ...
    /// </summary>
    class EmptyView : OpenTKView, IView {
    //class EmptyView : SharpGLView, IView {

        private static Logger logger = LogManager.GetLogger("EmptyView");                        // the logger object for the view

        public EmptyView() : base(120, 0, 0, 640, 480, true) {
            
        }

        public EmptyView(int updateFrequency, int x, int y, int width, int height, bool border) : base(updateFrequency, x, y, width, height, border) {
            
        }

        ///////////////////////
        /// task functions
        //////////////////////



        ///////////////////////
        /// openGL load and draw functions
        //////////////////////


        protected override void load() {
            

        }

        protected override void unload() {


        }

        protected override void resize(int width, int height) {
            

        }

        protected override void update(double secondsElapsed) {

        }

        protected override void render() {

            //GL.BindTexture(TextureTarget.Texture2D, 0);
            glColor3(1f, 0f, 0f);
            glBeginQuads();
                glVertex2(50f, 50f);
                glVertex2(0f, 50f);
                glVertex2(0f, 0f);
                glVertex2(50f, 0f);
            glEnd();
            
        }

        protected override void userCloseForm() {

            // pass to the mainthread
            MainThread.eventViewClosed();

        }

    }

}
