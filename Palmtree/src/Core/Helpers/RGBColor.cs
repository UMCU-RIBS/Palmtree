/**
 * The RGBColorByte/RGBColorFloat class
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

namespace Palmtree.Core.Helpers {

    /*
    /// <summary>
    /// The <c>RGBColorByte</c> class.
    /// 
    /// ...
    /// </summary>
    public class RGBColorByte {

        public byte red = 0;
        public byte green = 0;
        public byte blue = 0;

        public RGBColorByte() {
        
        }

        public RGBColorByte(byte red, byte green, byte blue) {
            if (red < 0)        red = 0;
            if (green < 0)      green = 0;
            if (blue < 0)       blue = 0;
            if (red > 255)      red = 255;
            if (green > 255)    green = 255;
            if (blue > 255)     blue = 255;

            this.red    = red;
            this.green  = green;
            this.blue   = blue;

        }

        public float redAsFloat()   {   return (float)(this.red / 255.0);    }
        public float greenAsFloat() {   return (float)(this.green / 255.0);  }
        public float blueAsFloat()  {   return (float)(this.blue / 255.0);   }

    }
    */

    /// <summary>
    /// The <c>RGBColorFloat</c> class.
    /// 
    /// ...
    /// </summary>
    public class RGBColorFloat {

        private float red = 0;
        private float green = 0;
        private float blue = 0;
        
        public RGBColorFloat(float red, float green, float blue) {
            setRed(red);
            setGreen(green);
            setBlue(blue);
        }

        public RGBColorFloat(byte red, byte green, byte blue) {
            setRed(red);
            setGreen(green);
            setBlue(blue);
        }

        public RGBColorFloat(int value) {
            setInt24(value);
        }

        public float getRed()           {   return this.red;    }
        public float getGreen()         {   return this.green;  }
        public float getBlue()          {   return this.blue;   }

        public byte getRedAsByte()      {   return (byte)(this.red * 255.0);     }
        public byte getGreenAsByte()    {   return (byte)(this.green * 255.0);   }
        public byte getBlueAsByte()     {   return (byte)(this.blue * 255.0);    }


        public void setRed(byte red) {
            if (red < 0)      red = 0;
            if (red > 255)    red = 255;
            this.red = (float)(red / 255.0);
        }

        public void setGreen(byte green) {
            if (green < 0)      green = 0;
            if (green > 255)    green = 255;
            this.green = (float)(green / 255.0);
        }

        public void setBlue(byte blue) {
            if (blue < 0)      blue = 0;
            if (blue > 255)    blue = 255;
            this.blue = (float)(blue / 255.0);
        }


        public void setRed(float red) {
            if (red < 0)        red = 0;
            if (red > 1)        red = 1;
            this.red = red;
        }

        public void setGreen(float green) {
            if (green < 0)      green = 0;
            if (green > 1)      green = 1;
            this.green = green;
        }

        public void setBlue(float blue) {
            if (blue < 0)       blue = 0;
            if (blue > 1)       blue = 1;
            this.blue = blue;
        }

        public void setInt24(int value) {
            if (value < 0)          value = 0;
            if (value > 16777215)   value = 16777215;
            
            // convert int to RGB
            int red = (value >> 16) & 0xFF;
            int green = (value >> 8) & 0xFF;
            int blue = value & 0xFF;

            // store the values
            setRed((byte)red);
            setGreen((byte)green);
            setBlue((byte)blue);

        }

    }

}
