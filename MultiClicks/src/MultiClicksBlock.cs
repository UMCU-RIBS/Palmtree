/**
 * The MultiClicksBlock class
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

namespace MultiClicksTask {

    /// <summary>
    /// The <c>MultiClicksBlock</c> class.
    /// 
    /// ...
    /// </summary>
    public class MultiClicksBlock {

        public float x;
        public float y;
        public float width;
        public float height;
        public float colorR;
        public float colorG;
        public float colorB;
        public int texture;

        public MultiClicksBlock(float x, float y, float width, float height, float colorR, float colorG, float colorB) {
	        this.x = x;
	        this.y = y;
	        this.width = width;
	        this.height = height;
	        texture = 0;
            this.colorR = colorR;
            this.colorG = colorG;
            this.colorB = colorB;
        }
    
    }

}
