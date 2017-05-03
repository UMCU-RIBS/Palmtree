using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UNP.Core.Helpers;

namespace UNP.Core.Params {

    public abstract class ParamColorBase : Param {
        
        protected RGBColorFloat colorStdValue = new RGBColorFloat(0f, 0f, 0f);

        public ParamColorBase(string name, string group, Parameters parentSet, string desc, string[] options) : base(name, group, parentSet, desc, options) { }

        protected bool tryParseValue(string value, out int intValue) {
            intValue = 0;

            // return false if the value is empty
            if (String.IsNullOrEmpty(value))    return false;

            // check if value is numeric and can be converted to an int
            // return false if unsucessful
            if (!int.TryParse(value, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, Parameters.NumberCulture, out intValue)) return false;

            // check if the number is between 0 and 16777216
            if (intValue < 0 || intValue > 16777216)    return false;

            // successfull parsing, return true
            return true;

        }

        protected bool tryParseValue(string[] value, out int intValueRed, out int intValueGreen, out int intValueBlue) {
            intValueRed = 0;
            intValueGreen = 0;
            intValueBlue = 0;

            // check the input
            if (value == null || value.Length != 3)         return false;

            // check red
            if (!int.TryParse(value[0], NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, Parameters.NumberCulture, out intValueRed))     return false;
            if (intValueRed < 0 || intValueRed > 255)       return false;

            // check green
            if (!int.TryParse(value[1], NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, Parameters.NumberCulture, out intValueGreen))   return false;
            if (intValueGreen < 0 || intValueGreen > 255)   return false;

            // check blue
            if (!int.TryParse(value[2], NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, Parameters.NumberCulture, out intValueBlue))    return false;
            if (intValueBlue < 0 || intValueBlue > 255)     return false;

            // successfull parsing, return true
            return true;

        }

        public bool setStdValue(string stdValue) {

            // try to split up the string
            string[] split = stdValue.Split(Parameters.ArrDelimiters, StringSplitOptions.RemoveEmptyEntries);
            
            // check if it either gives 1 value (color as integer) or 3 values (color as rgb)
            if (split.Length != 1 && split.Length != 3) {

                // set the stdvalue to be 0
                this.stdValue = "0";

                // return fail
                return false;

            }
            
            // single value or RGB values
            if (split.Length == 1) {
                
                // try to parse the value
                int intValue;
                if (!tryParseValue(stdValue, out intValue)) {

                    // set the stdvalue to be 0
                    this.stdValue = "0";

                    // return fail
                    return false;

                }
                
                // convert int to RGB
                int blue = (int)Math.Floor(intValue / 65536.0);
                int green = (int)Math.Floor((intValue - blue * 65536) / 256.0);
                int red = intValue - (blue * 65536) - (green * 256);

                // store the std value as RGBColorFloat
                colorStdValue = new RGBColorFloat((float)(red / 255.0), (float)(green / 255.0), (float)(blue / 255.0));
                
            } else if (split.Length == 3) {

                // try to parse the value
                int intRedValue;
                int intGreenValue;
                int intBlueValue;
                if (!tryParseValue(split, out intRedValue, out intGreenValue, out intBlueValue)) {

                    // set the stdvalue to be 0
                    this.stdValue = "0";

                    // return fail
                    return false;

                }

                // store the std value as RGBColorFloat
                colorStdValue = new RGBColorFloat((float)(intRedValue / 255.0), (float)(intGreenValue / 255.0), (float)(intBlueValue / 255.0));

            }

            // make lowercase and store the stdvalue
            this.stdValue = stdValue;

            // return success
            return true;

        }


    }

}
