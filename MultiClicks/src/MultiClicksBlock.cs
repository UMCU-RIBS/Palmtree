using System;

namespace MultiClicksTask {

    public class MultiClicksBlock {

        public float mX;
        public float mY;
        public float mWidth;
        public float mHeight;
        public float mColorR;
        public float mColorG;
        public float mColorB;
        public int mTexture;

        public MultiClicksBlock(float x, float y, float width, float height, float colorR, float colorG, float colorB) {
	        mX = x;
	        mY = y;
	        mWidth = width;
	        mHeight = height;
	        mTexture = 0;
            mColorR = colorR;
            mColorG = colorG;
            mColorB = colorB;
        }
    
    }

}
