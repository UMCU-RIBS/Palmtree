/**
 * The glFreeTypeFont class
 * 
 * ...
 * 
 * 
 * Copyright (C) 2017:  RIBS group (Nick Ramsey Lab), University Medical Center Utrecht (The Netherlands) & external contributors
 * Author(s):           Max van den Boom            (info@maxvandenboom.nl)
 * 
 * Adapted from:        The FreeType Project and Cross-platform FreeType bindings for .NET (www.freetype.org; Robert Rouhani) 
 * 
 * This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software
 * Foundation, either version 3 of the License, or (at your option) any later version. This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for
 * more details. You should have received a copy of the GNU General Public License along with this program. If not, see <https://www.gnu.org/licenses/>.
 */
using System;
using SharpFont;
using NLog;

namespace UNP.Views {

    /// <summary>
    /// The <c>glFreeTypeFont</c> class.
    /// 
    /// ...
    /// </summary>
    public class glFreeTypeFont {

        private static Logger logger = LogManager.GetLogger("Freetype");

		private bool initialized = false;
        private string font = "";
        public float height = 8;			        ///< Holds the height of the font.
        private uint[] textures = null;	            ///< Holds the texture id's 
        private uint list_base= 0;	                ///< Holds the first display list id
        private int[] charWidths = null;
        private bool[] charEnabled = null;
        private IOpenGLFunctions glView = null;

		//The init function will create a font of
		//of the height h from the file fname.
		public void init(IOpenGLFunctions glView, string font, uint height) {

            // store the height of the font and the reference to the view providing openGL functions
            this.glView = glView;
            this.font = font;
            this.height = height;

            // 
            charEnabled = new bool[128];
            for (uint i = 0; i < 128; i++) charEnabled[i] = true;
            
            // 
            init();

        }

        public void init(IOpenGLFunctions glView, string font, uint height, string initCharacters) {

            // store the height of the font and the reference to the view providing openGL functions
            this.glView = glView;
            this.font = font;
            this.height = height;

            byte[] bInitCharacters = System.Text.Encoding.ASCII.GetBytes(initCharacters);

            // 
            charEnabled = new bool[128];
            for (uint i = 0; i < bInitCharacters.Length; i++) {
                if (bInitCharacters[i] >= 0 && bInitCharacters[i] <= 255)
                    charEnabled[bInitCharacters[i]] = true;
            }
            
            // 
            init();

        }

        private void init() {

            // Allocate some memory to store the texture ids, initialize the arrays to hold the character widths
            textures = new uint[128];
            charWidths = new int[128];

            //Create and initilize a freetype font library
            Library library = new Library();

            //The object in which Freetype holds information on a given
            //font is called a "face".
            Face face;

            //This is where we load in the font information from the file.
            //Of all the places where the code might die, this is the most likely,
            //as FT_New_Face will die if the font file does not exist or is somehow broken.
            face = new Face(library, font);

            // set the character size
            face.SetCharSize((int)height, (int)height, 96, 96);

            //Here we ask opengl to allocate resources for
            //all the textures and displays lists which we
            //are about to create.  
            list_base = glView.glGenLists(128);
            glView.glGenTextures(128, textures);

            //This is where we actually create each of the fonts display lists.
            // loop for every character (we want to use) in the font
            for (uint i = 0; i < 128; i++) {
                if (charEnabled[i])     charWidths[i] = make_dlist(face, i, list_base, textures);
            }
            //We don't need the face information now that the display
            //lists have been created, so we free the assosiated resources.
            face.Dispose();

            //Ditto for the library.
            library.Dispose();

            // set font as initialized
            initialized = true;

        }

        // This function gets the first power of 2 >= the
        // int that we pass it.
        private int next_p2 ( int a ) {
	        int rval=1;
	        while(rval<a) rval<<=1;
	        return rval;
        }

	    // Create a display list coresponding to the give character.
        // return character width
	    private int make_dlist (Face face, uint ch, uint list_base, uint[] tex_base) {
            int charWidth = 0;

		    //The first thing we do is get FreeType to render our character
		    //into a bitmap.  This actually requires a couple of FreeType commands:

		    //Load the Glyph for our character.
            face.LoadGlyph(face.GetCharIndex(ch), LoadFlags.Default, LoadTarget.Normal);

		    //Move the face's glyph into a Glyph object.
            Glyph glyph = face.Glyph.GetGlyph();
            
		    //Convert the glyph to a bitmap.
            glyph.ToBitmap(RenderMode.Normal, new FTVector26Dot6(0, 0), true);
            BitmapGlyph bitmap_glyph = glyph.ToBitmapGlyph();

		    //This reference will make accessing the bitmap easier
		    FTBitmap bitmap = bitmap_glyph.Bitmap;

		    //Use our helper function to get the widths of
		    //the bitmap data that we will need in order to create
		    //our texture.
		    int width = next_p2( bitmap.Width );
		    int height = next_p2( bitmap.Rows );

            //Allocate memory for the texture data.
            byte[] expanded_data = new byte[2 * width * height];
            
		    //Here we fill in the data for the expanded bitmap.
		    //Notice that we are using two channel bitmap (one for
		    //luminocity and one for alpha), but we assign
		    //both luminocity and alpha to the value that we
		    //find in the FreeType bitmap. 
		    //We use the ?: operator so that value which we use
		    //will be 0 if we are in the padding zone, and whatever
		    //is the the Freetype bitmap otherwise.
            for(int j = 0; j < height ; j++) {
			    for(int i = 0; i < width; i++) {
				    expanded_data[2 * (i + j * width)] = 255;

                    if (i >= bitmap.Width || j >= bitmap.Rows)
                        expanded_data[2 * (i + j * width) + 1] = 0;
                    else
                        expanded_data[2 * (i + j * width) + 1] = bitmap.BufferData[i + bitmap.Width * j];

			    }
		    }

            //Now we just setup some texture paramaters.
            glView.glBindTexture2D(tex_base[ch]);
            glView.glTex2DParameterMinFilterLinear();
            glView.glTex2DParameterMagFilterLinear();

            //Here we actually create the texture itself, notice
            //that we are using GL_LUMINANCE_ALPHA to indicate that
            //we are using 2 channel data.
            glView.glTexImage2D_IntFormatRGBA_formatLumAlpha_bytes(width, height, expanded_data);

            //So now we can create the display list
            glView.glNewListCompile(list_base + ch);
            glView.glBindTexture2D(tex_base[ch]);

            glView.glPushMatrix();                                // better results with popping the matrix here..

            //first we need to move over a little so that
            //the character has the right amount of space
            //between it and the one before it.
            glView.glTranslate((float)bitmap_glyph.Left, 0f, 0f);

            //Now we move down a little in the case that the
            //bitmap extends past the bottom of the line 
            //(this is only true for characters like 'g' or 'y'.
            //GL.PushMatrix();                              // better results without popping the matrix before top translation
            int topOffset = bitmap_glyph.Top - bitmap.Rows;
            glView.glTranslate(0f, (float)topOffset, 0f);
            //glTranslatef(0, (GLfloat)bitmap_glyph->top-bitmap.rows, 0);

            //Now we need to account for the fact that many of
            //our textures are filled with empty padding space.
            //We figure what portion of the texture is used by 
            //the actual character and store that information in 
            //the x and y variables, then when we draw the
            //quad, we will only reference the parts of the texture
            //that we contain the character itself.
            float	x=(float)bitmap.Width / (float)width,
                    y=(float)bitmap.Rows / (float)height;

            //Here we draw the texturemaped quads.
            //The bitmap that we got from FreeType was not 
            //oriented quite like we would like it to be,
            //so we need to link the texture to the quad
            //so that the result will be properly aligned.
            glView.glBeginQuads();
                glView.glTexCoord2(0, 0); glView.glVertex2(0f, (float)bitmap.Rows);
                glView.glTexCoord2(0, y); glView.glVertex2(0f, 0f);
                glView.glTexCoord2(x, y); glView.glVertex2((float)bitmap.Width, 0);
                glView.glTexCoord2(x, 0); glView.glVertex2((float)bitmap.Width, (float)bitmap.Rows);
            glView.glEnd();
            glView.glPopMatrix();
            glView.glTranslate((float)(face.Glyph.Advance.X), 0f, 0f);

            // set the char width
            charWidth = (int)face.Glyph.Advance.X;

            //Finish the display list
            glView.glEndList();

            // free the glyph memory (bugfix)
            bitmap.Dispose();
            bitmap_glyph.Dispose();
            glyph.Dispose();

            // return the character width
            return charWidth;

        }


	    /// A fairly straightforward function that pushes
	    /// a projection matrix that will make object world 
	    /// coordinates identical to window coordinates.
	    private void pushScreenCoordinateMatrix() {
            glView.glPushAttribTransform();
            int[] viewport = new int[4];
            glView.glGetIntegerViewport(ref viewport);
            glView.glMatrixModeProjection();
            glView.glPushMatrix();
            glView.glLoadIdentity();
            glView.glOrtho(viewport[0], viewport[2], viewport[1], viewport[3], 0, 1); // Paramters: left, right, bottom, top, near, far
            glView.glPopAttrib();
	    }

	    /// Pops the projection matrix without changing the current MatrixMode.
        private void pop_projection_matrix() {
            glView.glPushAttribTransform();
            glView.glMatrixModeProjection();
            glView.glPopMatrix();
            glView.glPopAttrib();
	    }


	    // print
	    public void printLine(float x, float y, string text)  {
            if (String.IsNullOrEmpty(text)) return;
            if (!initialized)               return;
            
            // get the viewport size
            int[] viewport = new int[4];
            glView.glGetIntegerViewport(ref viewport);
            
            // We want a coordinate system where things coresponding to window pixels.
            pushScreenCoordinateMatrix();
            
            // add attributes
            glView.glPushAttribTransformListCurrentEnable();
            glView.glMatrixModeModelView();
            glView.glDisableLighting();
            glView.glEnableTexture2D();
            glView.glDisableDepthTest();
            glView.glEnableBlend();

            glView.glBlendFunc_SrcAlpha_DstOneMinusSrcAlpha();
            
            uint font = list_base;
            glView.glListBase(list_base);

            float[] modelview_matrix = new float[16];
            glView.glGetFloatModelviewMatrix(ref modelview_matrix);

            // draw the text
            glView.glPushMatrix();
            glView.glLoadIdentity();
            glView.glTranslate(x, viewport[3] - y - height, 0);
            glView.glMultMatrix(modelview_matrix);
            
            //
            byte[] bText = System.Text.Encoding.ASCII.GetBytes(text);
            glView.glCallListsByte(bText.Length, bText);
            
            glView.glPopMatrix();

            // release attributes
            glView.glPopAttrib();

            pop_projection_matrix();

	    }

	    public int getTextWidth(string text) {
            if (String.IsNullOrEmpty(text)) return 0;
            if (!initialized)               return 0;

            byte[] bText = System.Text.Encoding.ASCII.GetBytes(text);
            
		    // count the total width
		    int totalWidth = 0;
		    for (int i = 0; i < bText.Length; i++) {
                totalWidth += charWidths[bText[i]];
		    }

		    return totalWidth;

	    }

		//Free all the resources assosiated with the font.
		public void clean() {
            if (!initialized)   return;

            // clear the lists and textures
            glView.glDeleteLists(list_base, 128);
            glView.glDeleteTextures(128, textures);

            // remove the reference
            glView = null;

            // flag as uninitialized
            initialized = false;

        }

    }

}
