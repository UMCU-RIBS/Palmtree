﻿/**
 * The OpenTKView class
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
using OpenTK.Graphics.OpenGL;
using System;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Threading;
using System.Windows.Forms;
using System.Drawing;
using NLog;
using OpenTK.Input;
using System.Runtime.InteropServices;

namespace Palmtree.Views {

    /// <summary>
    /// The <c>OpenTKView</c> class.
    /// 
    /// ...
    /// </summary>
    public abstract partial class OpenTKView : Form, IOpenGLFunctions {


        // The sources of the input event that is raised and is generally recognized as mouse events
        public enum MouseEventSource {
            Mouse,          // Events raised by the mouse
            Pen,            // Events raised by a stylus
            Touch           // Events raised by touching the screen
        }

        private static Logger logger = LogManager.GetLogger("View");

        //private const bool showFPS = true;
        private const bool vsync = true;

        // API's
        [DllImport("user32.dll")]
        private static extern uint GetMessageExtraInfo();

        protected IntPtr windowHandle = IntPtr.Zero;    // handle to the window
        private bool started = false;                   // flag whether the view is starting or started
        private bool formShown = false;                 // flag whether the view form is shown
        private bool glLoaded = false;                  // flag to track whether opengl is done initializing
        private bool glControlLoaded = false;           // flag to track whether opengl form is done initializing

        private Thread mainLoopThread = null;           // thread for animations and rendering
        private bool running = false;                   // flag connected to the thread for animations and rendering (set to false to stop the thread)

        protected int glControlWidth = 0;               // variable to hold the OpenGL control width (before initialization hold the startup width of the control, after startup holds the actual width)
        protected int glControlHeight = 0;              // variable to hold the OpenGL control height (before initialization hold the startup height of the control, after startup holds the actual height)
        private int updateFrequency = 0;                // the update frequency of the main loop (in maximum fps)
        private int updateFrequencySleepTime = 0;       // the 

        Stopwatch swTimePassed = new Stopwatch();       // stopwatch object to give an exact amount to time passed inbetween loops/frames
        private long timeFPS = 0;
        private int fpsCounter = 0;                     // counter for the frames drawn
        protected int fps = 0;                          // the number of fps per second

        bool afterInitialFormResize = false;            // flag to track whether the form has been resized to it's initial dimensions (before starting)
        private int windowX = 0, windowY = 0;
        protected int windowWidth = 0;
        protected int windowHeight = 0;
        private bool windowBorder = true;
        private float windowBackgroundColorR = 0f;
        private float windowBackgroundColorG = 0f;
        private float windowBackgroundColorB = 0f;
        private bool windowBackgroundColorChanged = false;

        private MouseState mouseState = new MouseState();
        private KeyboardState keyboardState = new KeyboardState();

        protected Point mouseInWindowPos = new Point(0, 0);         // variable that holds most up-to-date coordinates of the mouse in the winow (updates real-time, not dependent on polling)

        protected bool leftClickedState = false;                    // flag to hold whether a left click was made since the last time it was checked (resets upon polling)
        protected bool rightClickedState = false;                   // flag to hold whether a right click was made since the last time it was checked (resets upon polling)
        protected Point clickedInWindowPos = new Point(0, 0);       // variable that holds the coordinates of clicked location in the winow (updates upon tapping)
        protected bool tappedState = false;                         // flag to hold whether a tap was made since the last time it was checked (resets upon polling)
        protected Point tappedInWindowPos = new Point(0, 0);        // variable that holds the coordinates of tapped location in the winow (updates upon tapping)

        // pure abstract functions that are required to be implemented by the deriving class
        protected abstract void load();
        protected abstract void unload();
        protected abstract void resize(int width, int height);
        protected abstract void update(double secondsElapsed);
        protected abstract void render();
        protected abstract void userCloseForm();

        public OpenTKView(int updateFrequency, int x, int y, int width, int height, bool border) {
            this.updateFrequency = updateFrequency;
            if (updateFrequency > 0)    updateFrequencySleepTime = 1000 / updateFrequency;
            this.windowX = x;
            this.windowY = y;
            this.glControlWidth = width;
            this.glControlHeight = height;
            this.windowBorder = border;

            // required for OpenTK 2.0
            OpenTK.Toolkit.Init();

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
        public int getContentWidth()                        {   return glControlWidth;      }
        public int getContentHeight()                       {   return glControlHeight;     }
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
        public bool isStarted()                             {   return glLoaded && glControlLoaded;    }
        public bool hasBorder()                             {   return windowBorder;     }

        public void setBorder(bool border) {
            if (!glLoaded || !glControlLoaded) {

                this.windowBorder = border;

            } else  {

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

        public void setBackgroundColor(float red, float green, float blue) {
            windowBackgroundColorR = red;
            windowBackgroundColorG = green;
            windowBackgroundColorB = blue;
            windowBackgroundColorChanged = true;
        }

        public void start() {

            // flag as starting
            started = true;

            // flag form shown as false
            formShown = false;

            // set running to true (already for when it enters the loop later)
            running = true;

            // message
            logger.Debug("Using OpenTK");

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

                // store the window handle
                windowHandle = this.Handle;

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
	        while ((started && (!formShown && !isStarted())) && waitCounter > 0) {
		        Thread.Sleep(10);
		        waitCounter--;
	        }
            
            // stop the main view loop and wait for it to finish
            if (mainLoopThread != null) {

                // try to stop the main loop using running
                running = false;
                waitCounter = 200;

                while (mainLoopThread.IsAlive && waitCounter > 0) {
                    Thread.Sleep(10);
                    waitCounter--;
                }

                // check if the main loop did not stop
                if (waitCounter == 0) {
                    
                    // abort the thread (more forcefully)
                    mainLoopThread.Abort();
                    while (mainLoopThread.IsAlive)  Thread.Sleep(10);
                }

            }

            if (this.IsHandleCreated && !this.IsDisposed) {
                try {
                    // close the form on the forms thread
                    this.Invoke((MethodInvoker)delegate {
                        try {
                            glControl.Dispose();
                            this.Close();
                            this.Dispose(true);
                            Application.ExitThread();
                        } catch (Exception) { }
                    });
                } catch (Exception) { }
            }

            // flag as not longer started
            started = false;

        }

        private void glControl_Load(object sender, EventArgs e) {
            
            // enable or disable v-sync
            glControl.VSync = vsync;

            // setup the opengl viewport
            setupGLView();

            // Disable depth testing and culling
            GL.Disable(EnableCap.DepthTest);
            GL.Disable(EnableCap.CullFace);
            GL.Enable(EnableCap.LineSmooth);
            GL.Hint(HintTarget.LineSmoothHint, HintMode.Nicest);

            // enabled textures here
            // (this assumes every vertex has textures, this might not be true, 
            //  however if bindtexture is set to a GLuint of 0 then no texture will be applied)
            GL.Enable(EnableCap.Texture2D);

            //  Set the clear color.
            GL.ClearColor(windowBackgroundColorR, windowBackgroundColorG, windowBackgroundColorB, 0f);
            windowBackgroundColorChanged = false;

            // set transparancy
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            // clear the buffer and show (black screen)
            GL.Clear(ClearBufferMask.ColorBufferBit);

            // set opengl as loaded
            glLoaded = true;

            // call load in the deriving class	
            load();

            // start the main loop for animations and rendering
            mainLoopThread = new Thread(this.mainLoop);
            mainLoopThread.Start();

            // set the focus to the view
            this.Focus();
            
        }

        private void setupGLView() {

            // retrieve the openGL control dimensions
            glControlWidth = glControl.Width;
            glControlHeight = glControl.Height;

            // update the window control dimensions
            windowX = this.Location.X;
            windowY = this.Location.Y;
            windowWidth = this.Size.Width;
            windowHeight = this.Size.Height;

            // Setup our viewport to be the entire size of the OPENGL panel in the window
            GL.Viewport(0, 0, glControlWidth, glControlHeight);

            // Change to the projection matrix, reset the matrix and set up orthagonal projection (i.e. 2D)
            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadIdentity();
            GL.Ortho(0, glControlWidth, glControlHeight, 0, 1, -1);    // Paramters: left, right, bottom, top, near, far
            
            // Reset the model matrix
            GL.MatrixMode(MatrixMode.Modelview);
            GL.LoadIdentity();

            // handle resize (call to the deriving class)
            resize(glControlWidth, glControlHeight);

        }

        private void glControl_Resize(object sender, EventArgs e) {
            if (!glLoaded)                  return;
            if (!glControlLoaded)           return;
            if (!afterInitialFormResize)    return;

            // re-setup the openGL viewport
            setupGLView();

            // make sure a redraw occurs after the resize
            //glControl.Invalidate();

        }

        private void mainLoop() {

            // name this thread
            if (Thread.CurrentThread.Name == null)
                Thread.CurrentThread.Name = "View main loop Thread";

            // message
            logger.Debug("Starting view main loop (thread)");

            // set opengl as loaded
            glControlLoaded = true;

            // set an initial start for the stopwatch
            swTimePassed.Start();

            // enter the main loop
            while (running) {

                // fps watch
                if (Stopwatch.GetTimestamp() > timeFPS) {
                    timeFPS = Stopwatch.GetTimestamp() + Stopwatch.Frequency;
                    fps = fpsCounter;
                    fpsCounter = 0;
                    //logger.Trace("fps: " + fps);
                }

                // calculate the exact time that has passed since the last run
                swTimePassed.Stop();
                double timePassed = swTimePassed.ElapsedMilliseconds;
                swTimePassed.Reset();
                swTimePassed.Start();

                // update animations using mTimePassed (call to the deriving class)
                update(timePassed / 1000f);

                // Get current mouse state
                mouseState = Mouse.GetCursorState();

                // Get current keyboard state
                keyboardState = Keyboard.GetState();

                // redraw/render
                try {
                    glControl.Invalidate();
                } catch (Exception) {}

		        // let the loop sleep to claim less unnecessary processor capacity
                if (updateFrequencySleepTime == 0)
                    Thread.Sleep(1);
                else
                    Thread.Sleep(updateFrequencySleepTime);
                
            }

            // message
            logger.Debug("View main loop (thread) stopped");

            // call unload in the deriving class
            unload();

        }

        private void glControl_Paint(object sender, PaintEventArgs e) {
            if (!glLoaded)          return;
            if (!glControlLoaded)   return;

            // check if the background color has changed, set the new clear color
            if (windowBackgroundColorChanged) {
                GL.ClearColor(windowBackgroundColorR, windowBackgroundColorG, windowBackgroundColorB, 0f);
                windowBackgroundColorChanged = false;
            }

            // clear the buffer
            GL.Clear(ClearBufferMask.ColorBufferBit);

            // Reset the matrix
            GL.MatrixMode(MatrixMode.Modelview);
            GL.LoadIdentity();

            // calculate the cursor position in relation to the window
            mouseInWindowPos = this.PointToClient(new Point(mouseState.X, mouseState.Y));

            // call the render in the deriving class
            render();

            // draw the fps
            //if (showFPS)
                //gl.DrawText(glControlWidth - 50, 10, 1f, 1f, 1f, "Arial", 12, ("fps: " + fps));

            // swap the buffer
            glControl.SwapBuffers();

            // count the frames drawn
            fpsCounter++;
            
        }

        private void glControl_MouseDown(object sender, System.Windows.Forms.MouseEventArgs e) {

            // retrieve the mouse event source
            MouseEventSource mouseEventSource = GetMouseEventSource();

            // check if the source
            if (mouseEventSource == MouseEventSource.Mouse) {
                // mouse

                if (e.Button == MouseButtons.Left) {
                    leftClickedState = true;
                    clickedInWindowPos = e.Location;

                } else if (e.Button == MouseButtons.Right) {
                    rightClickedState = true;
                    clickedInWindowPos = e.Location;
                }

            } else if (mouseEventSource == MouseEventSource.Pen) {
                // pen

                tappedState = true;
                tappedInWindowPos = e.Location;

            } else if (mouseEventSource == MouseEventSource.Touch) {
                // touch

                tappedState = true;
                tappedInWindowPos = e.Location;

            }
            
        }

        private void glControl_KeyDown(object sender, KeyEventArgs e) {
            
        }

        // returns whether the screen was tapped since the last call to this function (poll)
        public bool isTapped() {
            bool returnValue = tappedState;
            tappedState = false;
            return returnValue;
        }

        // returns whether the screen was left-clicked since the last call to this function (poll)
        public bool isLeftClicked() {
            bool returnValue = leftClickedState;
            leftClickedState = false;
            return returnValue;
        }

        // returns whether the screen was right-clicked since the last call to this function (poll)
        public bool isRightClicked() {
            bool returnValue = rightClickedState;
            rightClickedState = false;
            return returnValue;
        }

        public bool isLeftMouseDown() {
            return mouseState.IsButtonDown(MouseButton.Left);
        }

        public bool isRightMouseDown() {
            return mouseState.IsButtonDown(MouseButton.Right);
        }
        
        public bool isKeyDown(System.Windows.Forms.Keys key) {

            // TODO: something smarter to convert a generic key value (now System.Windows.Forms.Keys) to a OpenTK specific key value (OpenTK.Input.Key)
            /*
            if (key == Keys.A)                                  return keyboardState.IsKeyDown(OpenTK.Input.Key.A);
            if (key == Keys.Escape)                             return keyboardState.IsKeyDown(OpenTK.Input.Key.Escape);
            if (key == Keys.D1 || key == Keys.NumPad1)          return keyboardState.IsKeyDown(OpenTK.Input.Key.Number1);
            if (key == Keys.D2 || key == Keys.NumPad2)          return keyboardState.IsKeyDown(OpenTK.Input.Key.Number2);
            if (key == Keys.LControlKey)                        return keyboardState.IsKeyDown(OpenTK.Input.Key.ControlLeft);
            if (key == Keys.RControlKey)                        return keyboardState.IsKeyDown(OpenTK.Input.Key.ControlRight);
            */

            return false;

        }

        public bool isKeyDownEscape() {
            return keyboardState.IsKeyDown(OpenTK.Input.Key.Escape);
        }

        public bool isKeyUp(System.Windows.Forms.Keys key) {

            // TODO: something smarter to convert a generic key value (now System.Windows.Forms.Keys) to a OpenTK specific key value (OpenTK.Input.Key)
            /*
            if (key == Keys.A)                                  return keyboardState.IsKeyUp(OpenTK.Input.Key.A);
            if (key == Keys.Escape)                             return keyboardState.IsKeyUp(OpenTK.Input.Key.Escape);
            if (key == Keys.D1 || key == Keys.NumPad1)          return keyboardState.IsKeyUp(OpenTK.Input.Key.Number1);
            if (key == Keys.D2 || key == Keys.NumPad2)          return keyboardState.IsKeyUp(OpenTK.Input.Key.Number2);
            if (key == Keys.LControlKey)                        return keyboardState.IsKeyUp(OpenTK.Input.Key.ControlLeft);
            if (key == Keys.RControlKey)                        return keyboardState.IsKeyUp(OpenTK.Input.Key.ControlRight);
            */

            return false;

        }

        public void drawLine(float x1, float y1, float x2, float y2, float lineWidth, bool dashed, float colorR, float colorG, float colorB) {

            // set the color
            GL.Color3(colorR, colorG, colorB);

            // set no texture
            GL.BindTexture(TextureTarget.Texture2D, 0);

            // set the line with
            GL.LineWidth(lineWidth);

            // dashed
            if (dashed) {
                GL.LineStipple(1, 0x00FF);      /*  dashed  */
                //GL.LineStipple(1, 0x0101);      /*  dotted  */
                //GL.LineStipple(1, 0x1C47);      /*  dash/dot/dash  */
                GL.Enable(EnableCap.LineStipple);
            }

            // draw the line
            GL.Begin(PrimitiveType.Lines);
                GL.Vertex2(x1, y1);
                GL.Vertex2(x2, y2);
            GL.End();

            // disable stipple (in case it was enabled)
            GL.Disable(EnableCap.LineStipple);

        }

        public void drawRectangle(float x1, float y1, float x2, float y2, float lineWidth, float colorR, float colorG, float colorB) {
	
	        // set the color
            GL.Color3(colorR, colorG, colorB);

	        // set no texture
            GL.BindTexture(TextureTarget.Texture2D, 0);

	        // set the line with
            GL.LineWidth(lineWidth);

	        // draw the rectangle
            GL.Begin(PrimitiveType.LineLoop);
                GL.Vertex2(x1, y1);
                GL.Vertex2(x2, y1);
                GL.Vertex2(x2, y2);
                GL.Vertex2(x1, y2);
	        GL.End();

        }


        public void glColor3(byte red, byte green, byte blue)                   {   GL.Color3(red, green, blue);        }
        public void glColor3(float red, float green, float blue)                {   GL.Color3(red, green, blue);        }
        public void glColor4(byte red, byte green, byte blue, byte alpha)       {   GL.Color4(red, green, blue, alpha); }
        public void glColor4(float red, float green, float blue, float alpha)   {   GL.Color4(red, green, blue, alpha); }

        public void glVertex2(int x, int y)                 {   GL.Vertex2(x, y);       }
        public void glVertex2(float x, float y)             {   GL.Vertex2(x, y);       }
        public void glVertex2(double x, double y)           {   GL.Vertex2(x, y);       }
        public void glVertex3(int x, int y, int z)          {   GL.Vertex3(x, y, z);    }
        public void glVertex3(float x, float y, float z)    {   GL.Vertex3(x, y, z);    }
        public void glVertex3(double x, double y, double z) {   GL.Vertex3(x, y, z);    }

        public void glBindTexture2D(int texture)                {   GL.BindTexture(TextureTarget.Texture2D, texture);   }
        public void glBindTexture2D(uint texture)               {   GL.BindTexture(TextureTarget.Texture2D, texture);   }
        public void glTexCoord2(int s, int t)                   {   GL.TexCoord2(s, t);     }
        public void glTexCoord2(float s, float t)               {   GL.TexCoord2(s, t);     }
        public void glTexCoord2(double s, double t)             {   GL.TexCoord2(s, t);     }
        public void glDeleteTexture(int id)                     {   GL.DeleteTexture(id);   }
        public void glDeleteTexture(uint id)                    {   GL.DeleteTexture(id);   }
        public void glDeleteTextures(int n, uint[] textures)    {   GL.DeleteTextures(n, textures); }

        public uint glGenLists(int range)                       {   return (uint)GL.GenLists(range);    }
        public void glNewListCompile(uint list)                 {   GL.NewList(list, ListMode.Compile); }
        public void glListBase(uint listbase)                   {   GL.ListBase(listbase);              }
        public void glCallListsByte(int n, byte[] lists)        {   GL.CallLists(n, ListNameType.UnsignedByte, lists);  }
        public void glEndList()                                 {   GL.EndList();                       }
        public void glDeleteLists(uint list, int range)         {   GL.DeleteLists(list, range);        }

        public void glGenTextures(int n, uint[] textures)       {   GL.GenTextures(n, textures);        }
        public void glTex2DParameterMinFilterNearest()          {   GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest); }
        public void glTex2DParameterMinFilterLinear()           {   GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);  }
        public void glTex2DParameterMagFilterNearest()          {   GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMinFilter.Nearest); }
        public void glTex2DParameterMagFilterLinear()           {   GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMinFilter.Linear);  }
        public void glTexImage2D_IntFormatRGBA_formatBGRA_bytes(int width, int height, IntPtr pixels) {
            GL.TexImage2D(  TextureTarget.Texture2D, 0,
                            PixelInternalFormat.Rgba,width, height, 0, OpenTK.Graphics.OpenGL.PixelFormat.Bgra,
                            PixelType.UnsignedByte, pixels);
        }
        public void glTexImage2D_IntFormatRGBA_formatBGRA_bytes(int width, int height, byte[] pixels) {
            GL.TexImage2D(  TextureTarget.Texture2D, 0,
                            PixelInternalFormat.Rgba,width, height, 0, OpenTK.Graphics.OpenGL.PixelFormat.Bgra,
                            PixelType.UnsignedByte, pixels);
        }
        public void glTexImage2D_IntFormatRGBA_formatLumAlpha_bytes(int width, int height, byte[] pixels) {
            GL.TexImage2D(  TextureTarget.Texture2D, 0,
                            PixelInternalFormat.Rgba, width, height, 0, OpenTK.Graphics.OpenGL.PixelFormat.LuminanceAlpha,
                            PixelType.UnsignedByte, pixels);
        }

        public void glGetIntegerViewport(ref int[] parameters)          {       GL.GetInteger(GetPName.Viewport, parameters);       }
        public void glGetFloatModelviewMatrix(ref float[] parameters)   {       GL.GetFloat(GetPName.ModelviewMatrix, parameters);  }

        public void glMatrixModeProjection()                    {   GL.MatrixMode(MatrixMode.Projection);           }
        public void glMatrixModeModelView()                     {   GL.MatrixMode(MatrixMode.Modelview);            }
        public void glOrtho(double left, double right, double bottom, double top, double zNear, double zFar) {
            GL.Ortho(left, right, bottom, top, zNear, zFar);
        }

        public void glLoadIdentity()                            {   GL.LoadIdentity();                              }
        public void glPushMatrix()                              {   GL.PushMatrix();                                }
        public void glPopMatrix()                               {   GL.PopMatrix();                                 }
        public void glMultMatrix(float[] m)                     {   GL.MultMatrix(m);                               }

        public void glPushAttribTransform()                     {   GL.PushAttrib(AttribMask.TransformBit);         }
        public void glPushAttribList()                          {   GL.PushAttrib(AttribMask.ListBit);              }
        public void glPushAttribCurrent()                       {   GL.PushAttrib(AttribMask.CurrentBit);           }
        public void glPushAttribEnable()                        {   GL.PushAttrib(AttribMask.EnableBit);            }
        public void glPushAttribTransformListCurrentEnable()    {
            GL.PushAttrib(AttribMask.TransformBit | AttribMask.ListBit | AttribMask.CurrentBit | AttribMask.EnableBit);
        }
        public void glPopAttrib()                               {   GL.PopAttrib();                                 }


        public void glEnableTexture2D()                         {   GL.Enable(EnableCap.Texture2D);     }
        public void glEnableLighting()                          {   GL.Enable(EnableCap.Lighting);      }
        public void glEnableDepthTest()                         {   GL.Enable(EnableCap.DepthTest);     }
        public void glEnableBlend()                             {   GL.Enable(EnableCap.Blend);         }
        public void glDisableTexture2D()                        {   GL.Disable(EnableCap.Texture2D);    }
        public void glDisableLighting()                         {   GL.Disable(EnableCap.Lighting);     }
        public void glDisableDepthTest()                        {   GL.Disable(EnableCap.DepthTest);    }
        public void glDisableBlend()                            {   GL.Disable(EnableCap.Blend);        }

        public void glBlendFunc_SrcAlpha_DstOneMinusSrcAlpha() {
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        }

        public void glTranslate(float x, float y, float z)      {   GL.Translate(x, y, z);  }
        public void glTranslate(double x, double y, double z)   {   GL.Translate(x, y, z);  }

        public void glBeginQuads()       {   GL.Begin(PrimitiveType.Quads);     }
        public void glBeginTriangles()   {   GL.Begin(PrimitiveType.Triangles); }
        public void glBeginPolygon()     {   GL.Begin(PrimitiveType.Polygon);   }
        public void glBeginLineLoop()    {   GL.Begin(PrimitiveType.LineLoop);  }
        public void glBeginLines()       {   GL.Begin(PrimitiveType.Lines);  }
        public void glEnd()              {   GL.End();                          }


        public uint loadImage(string file) {
            if (!glLoaded) {

                // message
                logger.Error("Error while loading image, gl not yet loaded");

                // return 0
                return 0;

            }

            try {

                // load the image
                Bitmap bitmap = new Bitmap(file);
                
                // create the texture
                uint tex;
                GL.GenTextures(1, out tex);
                if (tex == 0) {

                    // message
                    logger.Error("OpenGL was unable to execute glGenTextures succesfully. GLError: " + GL.GetError().ToString());

                    // return 0
                    return 0;
                }

                // bind the texture
                GL.BindTexture(TextureTarget.Texture2D, tex);

                // set the texture properties for OpenGL
                GL.Enable(EnableCap.Texture2D);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);   //GL_NEAREST = no smoothing
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);   //GL_NEAREST = no smoothing

                // transfer the data to the texture
                BitmapData data = bitmap.LockBits(  new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height), 
                                                    ImageLockMode.ReadOnly,
                                                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                GL.TexImage2D(TextureTarget.Texture2D,
                                0,
                                PixelInternalFormat.Rgba,
                                data.Width,
                                data.Height,
                                0,
                                OpenTK.Graphics.OpenGL.PixelFormat.Bgra,
                                PixelType.UnsignedByte, 
                                data.Scan0);
                bitmap.UnlockBits(data);



                // message
                logger.Debug("Loaded image '" + file + "'");

	            // unbind
                glBindTexture2D(0);

                // return the texture id
                return tex;

            } catch(Exception) {

                // message
                logger.Error("Error while loading image ('" + file + "'), could not find file or file not an image");

                // return 0
                return 0;

            }

        }

        private void OpenTKView_Move(object sender, EventArgs e) {

            // update the window control location
            windowX = this.Location.X;
            windowY = this.Location.Y;

        }

        private void OpenTKView_Shown(object sender, EventArgs e) {

            // flag formshown as false (done starting)
            formShown = true;

        }

        private void OpenTKView_FormClosing(object sender, FormClosingEventArgs e) {
            
            // check whether the user is closing the form
            if (e.CloseReason == CloseReason.UserClosing && running) {

                // call user close form function
                userCloseForm();

            }

        }
        
        /// <summary>
        /// Determines what input device triggered the mouse event.
        /// </summary>
        /// <returns>
        /// A result indicating whether the last mouse event was triggered by a touch, pen or the mouse.
        /// </returns>
        public static MouseEventSource GetMouseEventSource() {
            uint extra = GetMessageExtraInfo();
            bool isTouchOrPen = ((extra & 0xFFFFFF00) == 0xFF515700);

            if (!isTouchOrPen)
                return MouseEventSource.Mouse;

            bool isTouch = ((extra & 0x00000080) == 0x00000080);

            return isTouch ? MouseEventSource.Touch : MouseEventSource.Pen;
        }

    }

}
