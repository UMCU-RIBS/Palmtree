using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UNP.Core.Helpers {

    public class RGBColorByte {

        public byte red = 0;
        public byte green = 0;
        public byte blue = 0;

        public RGBColorByte() {
        
        }

        public RGBColorByte(byte red, byte green, byte blue) {
            this.red    = red;
            this.green  = green;
            this.blue   = blue;

        }        

    }

    public class RGBColorFloat {

        public float red = 0;
        public float green = 0;
        public float blue = 0;

        public RGBColorFloat() {
        
        }

        public RGBColorFloat(float red, float green, float blue) {
            if (red < 0)        red = 0;
            if (green < 0)      green = 0;
            if (blue < 0)       blue = 0;
            if (red > 1)        red = 1;
            if (green > 1)      green = 1;
            if (blue > 1)       blue = 1;

            this.red = red;
            this.green = green;
            this.blue = blue;

        }

    }
}
