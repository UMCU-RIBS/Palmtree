using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using SharpGL;
using System.Threading;
using System.Diagnostics;
using System.Drawing.Imaging;
using NLog;

namespace UNP.Views {

    public abstract partial class SharpGLView : Form, IOpenGLFunctions {

        private static Logger logger = LogManager.GetLogger("View");

        private const bool showFPS = true;
        //private const bool vsync = false;

        
        protected OpenGL gl = null;                 // reference to the gl object instance (acquired through the glControl object)
        private bool formShown = false;             // flag whether the view form is shown
        private bool glLoaded = false;              // flag to track whether opengl is done initializing
        private bool running = false;               // flag to indicate whether the view should be drawing

        protected int glControlWidth = 0;           // variable to hold the OpenGL control width (before initialization hold the startup width of the control, after startup holds the actual width)
        protected int glControlHeight = 0;          // variable to hold the OpenGL control height (before initialization hold the startup height of the control, after startup holds the actual height)
        private int updateFrequency = 0;            // the update frequency of the main loop (in maximum fps)

        Stopwatch swTimePassed = new Stopwatch();   // stopwatch opbject to give an exact amount to time passed inbetween loops/frames
        private long timeFPS = 0;
        private int fpsCounter = 0;                 // counter for the frames drawn
        protected int fps = 0;                      // the number of fps per second

        bool afterInitialFormResize = false;        // flag to track whether the form has been resized to it's initial dimensions (before starting)
        private int windowX = 0, windowY = 0;
        protected int windowWidth = 0;
        protected int windowHeight = 0;
        private bool windowBorder = true;

        // pure abstract functions that are required to be implemented by the deriving class
        protected abstract void load();
        protected abstract void unload();
        protected abstract void resize(int width, int height);
        protected abstract void update(double secondsElapsed);
        protected abstract void render();
        
        public SharpGLView(int updateFrequency, int x, int y, int width, int height, bool border) {
            this.updateFrequency = updateFrequency;
            this.windowX = x;
            this.windowY = y;
            this.glControlWidth = width;
            this.glControlHeight = height;
            this.windowBorder = border;
        }

        public int getWindowX()                             {   return this.windowX;    }
        public int getWindowY()                             {   return this.windowY;    }
        public void setWindowLocation(int x, int y) {
            if (afterInitialFormResize) {
                this.Invoke((MethodInvoker)delegate {
                    this.Location = new Point(x, y);
                });
            } else {
                this.Location = new Point(x, y);
                windowX = x;
                windowY = y;
            }
        }
        public int getWindowWidth()                         {   return this.windowWidth;    }
        public int getWindowHeight()                        {   return this.windowHeight;    }
        public void setWindowSize(int width, int height)    {   
            this.Invoke((MethodInvoker)delegate {
                this.Size = new Size(width, height);
            });
        }
        public int getContentWidth()                        {   return this.glControlWidth;    }
        public int getContentHeight()                       {   return this.glControlHeight;    }
        public void setContentSize(int width, int height) {
            if (afterInitialFormResize) {
                this.Invoke((MethodInvoker)delegate {
                    this.ClientSize = new Size(width, height);
                });
            } else {
                this.ClientSize = new Size(width, height);
                glControlWidth = width;
                glControlHeight = height;
            }
        }
        public bool isStarted()                             {   return glLoaded && running;    }
        public bool hasBorder()                             {   return windowBorder;     }


        public void setBorder(bool border) {
            if (!glLoaded) {

                this.windowBorder = border;

            } else {

                if (this.FormBorderStyle == System.Windows.Forms.FormBorderStyle.None && border) {
                    // add the border

                    this.Invoke((MethodInvoker)delegate {
                    
                        // determine the X and Y of the window without border
                        Point withoutBorderPoint = PointToScreen(glControl.Location);

                        // add the border
                        this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;

                        // correct for the border (using the X and Y of the window with border)
                        Point withBorderPoint = PointToScreen(glControl.Location);
                        Point borderDelta = Point.Subtract(withBorderPoint, new Size(withoutBorderPoint));
                        this.Location = Point.Subtract(this.Location, new Size(borderDelta));

                        // set window border as true
                        windowBorder = true;

                    });

                }
                if (this.FormBorderStyle != System.Windows.Forms.FormBorderStyle.None && !border) {
                    // remove the border

                    this.Invoke((MethodInvoker)delegate {

                        // determine the X and Y of the gl control
                        Point point = PointToScreen(glControl.Location);

                        // remove the border
                        this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;

                        // put to it's original position (but this time without the frame)
                        this.Location = point;

                        // retrieve the actual window size
                        windowWidth = this.Size.Width;
                        windowHeight = this.Size.Height;

                        // set window border as false
                        windowBorder = false;

                    });


                }

            }
        }


        public void start() {

            // flag form shown as false
            formShown = false;

            // message
            logger.Debug("Using SharpGL");

            // create a view (as a separate process)
            Thread thread = new Thread(() => {
                
                // do the initialize component step here, needs to be in this thread
                InitializeComponent();

                // set the borderstyle
                this.FormBorderStyle = (windowBorder ? System.Windows.Forms.FormBorderStyle.Sizable : System.Windows.Forms.FormBorderStyle.None);

                // set the form/glcontrol position and size
                this.Location = new Point(windowX, windowY);
                if (this.FormBorderStyle == System.Windows.Forms.FormBorderStyle.None)
                    this.Size = new Size(glControlWidth, glControlHeight);
                else
                    this.ClientSize = new Size(glControlWidth, glControlHeight);

                // set the initial form resize as completed (only now use resize to retrieve window and control dimenstions)
                afterInitialFormResize = true;

                // retrieve the actual window size
                windowWidth = this.Size.Width;
                windowHeight = this.Size.Height;

                // name this thread
                if (Thread.CurrentThread.Name == null)
                    Thread.CurrentThread.Name = "View Thread";

                // message
                logger.Debug("Starting View (thread)");

                // start the GUI
                Application.Run(this);

                // message
                logger.Debug("View (thread) stopped");
                
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();

        }

        public void stop() {

	        // wait till the form is no longer starting and the glprocess started or a maximum amount of 4 seconds (4.000 / 10 = 400)
            // (resourcesLoaded also includes whether GL is loaded)
	        int waitCounter = 400;
            while ((!formShown || !isStarted()) && waitCounter > 0) {
		        Thread.Sleep(10);
		        waitCounter--;
	        }
            
            // close the form on the forms thread
            this.Invoke((MethodInvoker)delegate {

                // flag running to false (stop GL from drawing)
                running = false;

                // call unload in the deriving class	
                unload();

                try {
                    this.Close();
                    this.Dispose(true);
                } catch (Exception) { }
            });

        }


        /// <summary>
        /// Handles the OpenGLInitialized event of the openGLControl control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        private void openGLControl_OpenGLInitialized(object sender, EventArgs e) {

            //  get the OpenGL object.
            gl = glControl.OpenGL;

            // set the framerate (this will try to enforce a number of fps)
            if (this.updateFrequency < 1)
                glControl.FrameRate = 100;      // if we want to render as quickly as possible, then just set a very high rate here (which probably will be limited to the screen vsync anyway)
            else
                glControl.FrameRate = updateFrequency;

            // setup the opengl viewport
            setupGLView();

            // Disable depth testing and culling
            gl.Disable(OpenGL.GL_DEPTH_TEST);
            gl.Disable(OpenGL.GL_CULL_FACE);
            gl.Enable(OpenGL.GL_LINE_SMOOTH);
            gl.Hint(OpenGL.GL_LINE_SMOOTH_HINT, OpenGL.GL_NICEST);

            // enabled textures here
            // (this assumes every vertex has textures, this might not be true, 
            //  however if bindtexture is set to a GLuint of 0 then no texture will be applied)
            gl.Enable(OpenGL.GL_TEXTURE_2D);

            //  Set the clear color.
            gl.ClearColor(0, 0, 0, 0);

            // set transparancy
            gl.Enable(OpenGL.GL_BLEND);
            gl.BlendFunc(OpenGL.GL_SRC_ALPHA, OpenGL.GL_ONE_MINUS_SRC_ALPHA);

            // clear the buffer and show (black screen)
            gl.Clear(OpenGL.GL_COLOR_BUFFER_BIT);

            // set an initial start for the stopwatche
            swTimePassed.Start();

            // call load in the deriving class	
            load();

            // set opengl as loaded
            glLoaded = true;

            // flag as running (allow drawing)
            running = true;

        }

        /// <summary>
        /// Handles the Resized event of the openGLControl control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        private void openGLControl_Resized(object sender, EventArgs e) {
            if (!glLoaded) return;
            if (!afterInitialFormResize)    return;

            // re-setup the openGL viewport
            setupGLView();

        }

        private void setupGLView() {
            if (gl == null)    return;

            if (glLoaded) {
                // retrieve the openGL control dimensions
                glControlWidth = glControl.Width;
                glControlHeight = glControl.Height;
            }

            // update the window control dimensions
            windowX = this.Location.X;
            windowY = this.Location.Y;
            windowWidth = this.Size.Width;
            windowHeight = this.Size.Height;

            // Setup our viewport to be the entire size of the OPENGL panel in the window
            gl.Viewport(0, 0, glControlWidth, glControlHeight);

            // Change to the projection matrix, reset the matrix and set up orthagonal projection (i.e. 2D)
            gl.MatrixMode(OpenGL.GL_PROJECTION);
            gl.LoadIdentity();
            gl.Ortho(0, glControlWidth, glControlHeight, 0, 1, -1);    // Paramters: left, right, bottom, top, near, far

            // Reset the model matrix
            gl.MatrixMode(OpenGL.GL_MODELVIEW);
            gl.LoadIdentity();

            // handle resize (call to the deriving class)
            resize(glControlWidth, glControlHeight);

        }
        
        /// <summary>
        /// Handles the OpenGLDraw event of the openGLControl control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RenderEventArgs"/> instance containing the event data.</param>
        private void openGLControl_OpenGLDraw(object sender, RenderEventArgs e) {
            if (gl == null)     return;
            if (!running)       return;
            
            // fps watch
            if (Stopwatch.GetTimestamp() > timeFPS) {
                timeFPS = Stopwatch.GetTimestamp() + Stopwatch.Frequency;
                fps = fpsCounter;
                fpsCounter = 0;
            }
            
            // calculate the exact time that has passed since the last run
            swTimePassed.Stop();
            double timePassed = swTimePassed.ElapsedMilliseconds;
            swTimePassed.Reset();
            swTimePassed.Start();

            // update animations using mTimePassed (call to the deriving class)
            update(timePassed / 1000f);

            // render
            sceneRender();

            // add a frame to the counter
            fpsCounter++;

        }

        void sceneRender() {

            // clear the buffer
            gl.Clear(OpenGL.GL_COLOR_BUFFER_BIT);

            // Reset the matrix
            gl.MatrixMode(OpenGL.GL_MODELVIEW);
            gl.LoadIdentity();

            // call the render in the deriving class
            render();

            // draw the fps
	        if (showFPS)
                gl.DrawText(glControlWidth - 50, 10, 1f, 1f, 1f, "Arial", 12, ("fps: " + fps));

        }


        public void drawRectangle(float x1, float y1, float x2, float y2, float lineWidth, float colorR, float colorG, float colorB) {
	
	        // set the color
            gl.Color(colorR, colorG, colorB);

	        // set no texture
            gl.BindTexture(OpenGL.GL_TEXTURE_2D, 0);

	        // set the line with
            gl.LineWidth(lineWidth);

	        // draw the rectangle
            gl.Begin(OpenGL.GL_LINE_LOOP);
                gl.Vertex(x1, y1);
                gl.Vertex(x2, y1);
                gl.Vertex(x2, y2);
                gl.Vertex(x1, y2);
            gl.End();

        }


        public void glColor3(byte red, byte green, byte blue)                   {   gl.Color(red, green, blue);         }
        public void glColor3(float red, float green, float blue)                {   gl.Color(red, green, blue);         }
        public void glColor4(byte red, byte green, byte blue, byte alpha)       {   gl.Color(red, green, blue, alpha);  }
        public void glColor4(float red, float green, float blue, float alpha)   {   gl.Color(red, green, blue, alpha);  }

        public void glVertex2(int x, int y)                 {   gl.Vertex(x, y);    }
        public void glVertex2(float x, float y)             {   gl.Vertex(x, y);    }
        public void glVertex2(double x, double y)           {   gl.Vertex(x, y);    }
        public void glVertex3(int x, int y, int z)          {   gl.Vertex(x, y, z); }
        public void glVertex3(float x, float y, float z)    {   gl.Vertex(x, y, z); }
        public void glVertex3(double x, double y, double z) {   gl.Vertex(x, y, z); }

        public void glBindTexture2D(int texture)                {  gl.BindTexture(OpenGL.GL_TEXTURE_2D, (uint)texture);     }
        public void glBindTexture2D(uint texture)               {  gl.BindTexture(OpenGL.GL_TEXTURE_2D, texture);           }
        public void glTexCoord2(int s, int t)                   {   gl.TexCoord(s, t);     }
        public void glTexCoord2(float s, float t)               {   gl.TexCoord(s, t);     }
        public void glTexCoord2(double s, double t)             {   gl.TexCoord(s, t);     }
        public void glDeleteTexture(int id)                     {   gl.DeleteTextures(1, new uint[] { (uint)id });  }
        public void glDeleteTexture(uint id)                    {   gl.DeleteTextures(1, new uint[] { id });        }
        public void glDeleteTextures(int n, uint[] textures)    {   gl.DeleteTextures(n, textures); }

        public uint glGenLists(int range)                       {   return gl.GenLists(range);              }
        public void glNewListCompile(uint list)                 {   gl.NewList(list, OpenGL.GL_COMPILE);    }
        public void glListBase(uint listbase)                   {   gl.ListBase(listbase);                  }
        public void glCallListsByte(int n, byte[] lists)        {   gl.CallLists(n, lists);                 }
        public void glEndList()                                 {   gl.EndList();                           }
        public void glDeleteLists(uint list, int range)         {   gl.DeleteLists(list, range);            }

        public void glGenTextures(int n, uint[] textures)       {   gl.GenTextures(n, textures);    }
        public void glTex2DParameterMinFilterNearest()          {   gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MIN_FILTER, OpenGL.GL_NEAREST); }
        public void glTex2DParameterMinFilterLinear()           {   gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MIN_FILTER, OpenGL.GL_LINEAR);  }
        public void glTex2DParameterMagFilterNearest()          {   gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MAG_FILTER, OpenGL.GL_NEAREST); }
        public void glTex2DParameterMagFilterLinear()           {   gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MAG_FILTER, OpenGL.GL_LINEAR);  }
        public void glTexImage2D_IntFormatRGBA_formatBGRA_bytes(int width, int height, IntPtr pixels) {
            gl.TexImage2D(  OpenGL.GL_TEXTURE_2D, 0,
                            OpenGL.GL_RGBA, width, height, 0, OpenGL.GL_BGRA,
                            OpenGL.GL_UNSIGNED_BYTE, pixels);
        }
        public void glTexImage2D_IntFormatRGBA_formatBGRA_bytes(int width, int height, byte[] pixels) {
            gl.TexImage2D(  OpenGL.GL_TEXTURE_2D, 0,
                            OpenGL.GL_RGBA, width, height, 0, OpenGL.GL_BGRA,
                            OpenGL.GL_UNSIGNED_BYTE, pixels);
        }
        public void glTexImage2D_IntFormatRGBA_formatLumAlpha_bytes(int width, int height, byte[] pixels) {
            gl.TexImage2D(  OpenGL.GL_TEXTURE_2D, 0,
                            OpenGL.GL_RGBA, width, height, 0, OpenGL.GL_LUMINANCE_ALPHA,
                            OpenGL.GL_UNSIGNED_BYTE, pixels);
        }

        public void glGetIntegerViewport(ref int[] parameters)          {   gl.GetInteger(SharpGL.Enumerations.GetTarget.Viewport, parameters);     }
        public void glGetFloatModelviewMatrix(ref float[] parameters)   {   gl.GetFloat(SharpGL.Enumerations.GetTarget.ModelviewMatix, parameters); }

        public void glMatrixModeProjection()                    {   gl.MatrixMode(SharpGL.Enumerations.MatrixMode.Projection);              }
        public void glMatrixModeModelView()                     {   gl.MatrixMode(SharpGL.Enumerations.MatrixMode.Modelview);               }
        public void glOrtho(double left, double right, double bottom, double top, double zNear, double zFar) {
            gl.Ortho(left, right, bottom, top, zNear, zFar);
        }

        public void glLoadIdentity()                            {   gl.LoadIdentity();                                                      }
        public void glPushMatrix()                              {   gl.PushMatrix();                                                        }
        public void glPopMatrix()                               {   gl.PopMatrix();                                                         }
        public void glMultMatrix(float[] m)                     {   gl.MultMatrix(m);                                                       }

        public void glPushAttribTransform()                     {   gl.PushAttrib(SharpGL.Enumerations.AttributeMask.Transform);            }
        public void glPushAttribList()                          {   gl.PushAttrib(SharpGL.Enumerations.AttributeMask.List);                 }
        public void glPushAttribCurrent()                       {   gl.PushAttrib(SharpGL.Enumerations.AttributeMask.Current);              }
        public void glPushAttribEnable()                        {   gl.PushAttrib(SharpGL.Enumerations.AttributeMask.Enable);               }
        public void glPushAttribTransformListCurrentEnable()    {
            gl.PushAttrib(  SharpGL.Enumerations.AttributeMask.Transform | 
                            SharpGL.Enumerations.AttributeMask.List |
                            SharpGL.Enumerations.AttributeMask.Current |
                            SharpGL.Enumerations.AttributeMask.Enable);
        }
        public void glPopAttrib()                               {   gl.PopAttrib();                                                         }

        public void glEnableTexture2D()                         {   gl.Enable(OpenGL.GL_TEXTURE_2D);    }
        public void glEnableLighting()                          {   gl.Enable(OpenGL.GL_LIGHTING);      }
        public void glEnableDepthTest()                         {   gl.Enable(OpenGL.GL_DEPTH_TEST);    }
        public void glEnableBlend()                             {   gl.Enable(OpenGL.GL_BLEND);         }
        public void glDisableTexture2D()                        {   gl.Disable(OpenGL.GL_TEXTURE_2D);   }
        public void glDisableLighting()                         {   gl.Disable(OpenGL.GL_LIGHTING);     }
        public void glDisableDepthTest()                        {   gl.Disable(OpenGL.GL_DEPTH_TEST);   }
        public void glDisableBlend()                            {   gl.Disable(OpenGL.GL_BLEND);        }

        public void glBlendFunc_SrcAlpha_DstOneMinusSrcAlpha() {
            gl.BlendFunc(SharpGL.Enumerations.BlendingSourceFactor.SourceAlpha, SharpGL.Enumerations.BlendingDestinationFactor.OneMinusSourceAlpha);
        }

        public void glTranslate(float x, float y, float z)      {   gl.Translate(x, y, z);  }
        public void glTranslate(double x, double y, double z)   {   gl.Translate(x, y, z);  }

        public void glBeginQuads()       {   gl.Begin(OpenGL.GL_QUADS);      }
        public void glBeginTriangles()   {   gl.Begin(OpenGL.GL_TRIANGLES);  }
        public void glBeginPolygon()     {   gl.Begin(OpenGL.GL_POLYGON);    }
        public void glBeginLineLoop()    {   gl.Begin(OpenGL.GL_LINE_LOOP);  }
        public void glEnd()              {   gl.End();                       }

        public uint loadImage(string file) {
            if (gl == null) {    
                
                // message
                logger.Error("Error while loading image, gl not yet loaded");

                // return 0
                return 0;

            }

            try {

                // load the image
                Bitmap bitmap = new Bitmap(file);

                // create the texture
                uint[] tex = new uint[1];
                gl.GenTextures(1, tex);
                if (tex[0] == 0) {
                    
                    // message
                    logger.Error("OpenGL was unable to execute glGenTextures succesfully. GLError: " + gl.GetError().ToString());

                    // return 0
                    return 0;
                }
                
                // bind the texture
                gl.BindTexture(OpenGL.GL_TEXTURE_2D, tex[0]);

                // set the texture properties for OpenGL
                gl.Enable(OpenGL.GL_TEXTURE_2D);
                gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MIN_FILTER, OpenGL.GL_LINEAR);     //GL_NEAREST = no smoothing
                gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MAG_FILTER, OpenGL.GL_LINEAR);
                    

                // transfer the data to the texture
                BitmapData data = bitmap.LockBits(  new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height),
                                                    ImageLockMode.ReadOnly, 
                                                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                gl.TexImage2D(  OpenGL.GL_TEXTURE_2D, 
                                0, 
                                OpenGL.GL_RGBA,
                                data.Width,
                                data.Height, 
                                0,
                                OpenGL.GL_BGRA,
                                OpenGL.GL_UNSIGNED_BYTE,
                                data.Scan0);
                bitmap.UnlockBits(data);


                // message
                logger.Debug("Loaded image '" + file + "'");

                // unbind
                glBindTexture2D(0);

                // return the texture id
                return tex[0];

            } catch(Exception) {

                // message
                logger.Error("Error while loading image ('" + file + "'), could not find file or file not an image");

                // return 0
                return 0;

            }

        }

        private void SharpGLView_Move(object sender, EventArgs e) {

            // update the window control location
            windowX = this.Location.X;
            windowY = this.Location.Y;

        }

        private void SharpGLView_Shown(object sender, EventArgs e) {

            // flag formshown as true (done starting)
            formShown = true;

        }

    }
}
