/**
 * The FollowBlock class
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

namespace FollowTask {

    /// <summary>
    /// The <c>FollowBlock</c> class.
    /// 
    /// ...
    /// </summary>
    public class FollowBlock {

        public float mX;
        public float mY;
        public float mWidth;
        public float mHeight;
        public int mTexture;

        public FollowBlock(float x, float y, float width, float height) {
	        mX = x;
	        mY = y;
	        mWidth = width;
	        mHeight = height;
	        mTexture = 0;
        }
    
    }

}
