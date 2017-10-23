using System;

namespace UNP.Views {

    public interface IOpenGLFunctions {

        void glColor3(byte red, byte green, byte blue);
        void glColor3(float red, float green, float blue);
        void glColor4(byte red, byte green, byte blue, byte alpha);
        void glColor4(float red, float green, float blue, float alpha);

        void glVertex2(int x, int y);
        void glVertex2(float x, float y);
        void glVertex2(double x, double y);
        void glVertex3(int x, int y, int z);
        void glVertex3(float x, float y, float z);
        void glVertex3(double x, double y, double z);

        void glBindTexture2D(int texture);
        void glBindTexture2D(uint texture);
        void glTexCoord2(int s, int t);
        void glTexCoord2(float s, float t);
        void glTexCoord2(double s, double t);
        void glDeleteTexture(int id);
        void glDeleteTexture(uint id);
        void glDeleteTextures(int n, uint[] textures);

        uint glGenLists(int range);
        void glNewListCompile(uint list);
        void glListBase(uint listbase);
        void glCallListsByte(int n, byte[] lists);
        void glEndList();
        void glDeleteLists(uint list, int range);

        void glGenTextures(int n, uint[] textures);
        void glTex2DParameterMinFilterNearest();
        void glTex2DParameterMinFilterLinear();
        void glTex2DParameterMagFilterNearest();
        void glTex2DParameterMagFilterLinear();
        void glTexImage2D_IntFormatRGBA_formatBGRA_bytes(int width, int height, IntPtr pixels);
        void glTexImage2D_IntFormatRGBA_formatBGRA_bytes(int width, int height, byte[] pixels);
        void glTexImage2D_IntFormatRGBA_formatLumAlpha_bytes(int width, int height, byte[] pixels);

        void glGetIntegerViewport(ref int[] parameters);
        void glGetFloatModelviewMatrix(ref float[] parameters);

        void glMatrixModeProjection();
        void glMatrixModeModelView();
        void glOrtho(double left, double right, double bottom, double top, double zNear, double zFar);

        void glLoadIdentity();
        void glPushMatrix();
        void glPopMatrix();
        void glMultMatrix(float[] m);

        void glPushAttribTransform();
        void glPushAttribList();
        void glPushAttribCurrent();
        void glPushAttribEnable();
        void glPushAttribTransformListCurrentEnable();
        void glPopAttrib();

        void glEnableTexture2D();
        void glEnableLighting();
        void glEnableDepthTest();
        void glEnableBlend();
        void glDisableTexture2D();
        void glDisableLighting();
        void glDisableDepthTest();
        void glDisableBlend();

        void glBlendFunc_SrcAlpha_DstOneMinusSrcAlpha();

        void glTranslate(float x, float y, float z);
        void glTranslate(double x, double y, double z);

        void glBeginQuads();
        void glBeginTriangles();
        void glBeginPolygon();
        void glEnd();

    }

}
