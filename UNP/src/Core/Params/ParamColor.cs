using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UNP.Core.Helpers;

namespace UNP.Core.Params {

    public class ParamColor : ParamColorBase, iParam {

        private RGBColorFloat value = new RGBColorFloat(0f, 0f, 0f);

        public ParamColor(string name, string group, Parameters parentSet, string desc, string stdValue, string[] options) : base(name, group, parentSet, desc, stdValue, options) {
            minValue = "0";
            maxValue = "16777216";
        }

        public string getValue() {
            return value.getRedAsByte() + ";" + value.getGreenAsByte() + ";" + value.getBlueAsByte();
        }

        public T getValue<T>() {

            Type paramType = typeof(T);
            if(paramType == typeof(RGBColorFloat)) {
                // request to return as RGBColorFloat

                // return value
                return (T)Convert.ChangeType(Value, typeof(RGBColorFloat));

            } else {
                // request to return as other

                // message and return false
                logger.Error("Could not retrieve the value for parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "') as '" + paramType.Name + "', can only return value as color (RGBColorFloat). Returning null");
                return (T)Convert.ChangeType(null, typeof(T));    

            }
            
        }

        public T getUnit<T>() {

            Type paramType = typeof(T);
            if(paramType == typeof(Parameters.Units)) {
                // request to return as Parameters.Units

                // return value
                Parameters.Units unit = Parameters.Units.ValueOrSamples;
                return (T)Convert.ChangeType(unit, typeof(Parameters.Units));

            } else {
                // request to return as other

                // message and return false
                logger.Error("Could not retrieve the unit for parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "') as '" + paramType.Name + "', can only return value as 'Parameters.Units'. Returning 0");
                return (T)Convert.ChangeType(0, typeof(T));    

            }

        }

        public int getValueInSamples() {

            // message
            logger.Warn("Trying to retrieve the value for color parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "') in number of samples, use getValue<T>() instead, returning 0");
            
            // return 0
            return 0;

        }

        public override string ToString() {
            return getValue();
        }

        public RGBColorFloat Value {
            get {   return this.value;  }
        }

        public bool setValue(RGBColorFloat value) {
            this.value = value;
            return true;
        }

        public bool tryValue(string value) {
            if (String.IsNullOrEmpty(value))    return false;

            string[] split = value.Split(Parameters.ArrDelimiters, StringSplitOptions.RemoveEmptyEntries);

            if (split.Length != 1 && split.Length != 3)     return false;
            if (split.Length == 1) {
                int intValue;
                if (!tryParseValue(value, out intValue))    return false;
            } else if (split.Length == 3) {
                int intRedValue;
                int intGreenValue;
                int intBlueValue;
                if (!tryParseValue(split, out intRedValue, out intGreenValue, out intBlueValue))    return false;
            }

            return true;
        }

        public bool setValue(string value) {
            
            // try to split up the string
            string[] split = value.Split(Parameters.ArrDelimiters, StringSplitOptions.RemoveEmptyEntries);
            
            // check if it either gives 1 value (color as integer) or 3 values (color as rgb)
            if (split.Length != 1 && split.Length != 3) {

                // message
                logger.Error("Could not store the value for parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "'), the value '" + value + "' could not be parsed as a color. Either pass the color as a single number between 0 and 16777216, or as a RGB value in the format (RRR;GGG;BBB)");
                
                // return failure
                return false;

            }
            
            // single value or RGB values
            if (split.Length == 1) {
                
                // try to parse the value
                int intValue;
                if (!tryParseValue(value, out intValue)) {

                    // message
                    logger.Error("Could not store the value for parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "'), the value '" + value + "' could not be parsed as a color. If passing the color as a single number, the number should be between 0 and 16777216");
                
                    // return failure
                    return false;

                }
                
                // convert int to RGB
                int blue = (int)Math.Floor(intValue / 65536.0);
                int green = (int)Math.Floor((intValue - blue * 65536) / 256.0);
                int red = intValue - (blue * 65536) - (green * 256);

                // store the values
                this.value.setRed((byte)red);
                this.value.setGreen((byte)green);
                this.value.setBlue((byte)blue);

            } else if (split.Length == 3) {

                // try to parse the value
                int intRedValue;
                int intGreenValue;
                int intBlueValue;
                if (!tryParseValue(split, out intRedValue, out intGreenValue, out intBlueValue)) {

                    // message
                    logger.Error("Could not store the value for parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "'), the value '" + value + "' could not be parsed as a color. The one of the RGB values is not a number between 0 and 255, make sure the value is in the format (RRR;GGG;BBB)");
                
                    // return failure
                    return false;

                }
                
                // store the values
                this.value.setRed((byte)intRedValue);
                this.value.setGreen((byte)intGreenValue);
                this.value.setBlue((byte)intBlueValue);

            }

            // return success
            return true;
        }
        
        public iParam clone() {
            ParamColor clone = new ParamColor(name, group, parentSet, desc, stdValue, options);

            clone.stdValue = stdValue;

            clone.minValue = minValue;
            clone.maxValue = maxValue;

            clone.value = new RGBColorFloat(value.getRed(), value.getGreen(), value.getBlue());

            return clone;

        }

    }

}
