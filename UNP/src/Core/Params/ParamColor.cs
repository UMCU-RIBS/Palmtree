/**
 * The ParamColor class
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
using System;
using UNP.Core.Helpers;

namespace UNP.Core.Params {

    /// <summary>
    /// The <c>ParamColor</c> class.
    /// 
    /// ...
    /// </summary>
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

        public T getValueInSamples<T>() {

            // message
            logger.Error("Trying to retrieve the value in samples for color parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "') in number of samples, cannot convert a color, returning 0");

            // return value
            return (T)Convert.ChangeType(0, typeof(int));

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

                // cap the value
                if (intValue < 0)           intValue = 0;
                if (intValue > 16777215)    intValue = 16777215;

                // convert int to RGB
                int red = (intValue >> 16) & 0xFF;
                int green = (intValue >> 8) & 0xFF;
                int blue = intValue & 0xFF;

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
