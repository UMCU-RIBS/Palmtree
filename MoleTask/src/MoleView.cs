using NLog;
using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UNP;
using UNP.views;

namespace MoleTask {

    class MoleView : OpenTKView, IView {
    //class MoleView : SharpGLView, IView {

        private static Logger logger = LogManager.GetLogger("MoleView");                        // the logger object for the view

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

    }

}
