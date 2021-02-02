/**
 * The ParamColorBase class
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
using System.Globalization;

namespace UNP.Core.Params {

    /// <summary>
    /// The <c>ParamColorBase</c> class.
    /// 
    /// ...
    /// </summary>
    public abstract class ParamColorBase : Param {
        
        public ParamColorBase(string name, string group, Parameters parentSet, string desc, string stdValue, string[] options) : base(name, group, parentSet, desc, stdValue, options) { }

        protected bool tryParseValue(string value, out int intValue) {
            intValue = 0;

            // return false if the value is empty
            if (String.IsNullOrEmpty(value))    return false;

            // check if value is numeric and can be converted to an int
            // return false if unsucessful
            if (!int.TryParse(value, NumberStyles.None, Parameters.NumberCulture, out intValue)) return false;

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
            if (!int.TryParse(value[0], NumberStyles.None, Parameters.NumberCulture, out intValueRed))     return false;
            if (intValueRed < 0 || intValueRed > 255)       return false;

            // check green
            if (!int.TryParse(value[1], NumberStyles.None, Parameters.NumberCulture, out intValueGreen))   return false;
            if (intValueGreen < 0 || intValueGreen > 255)   return false;

            // check blue
            if (!int.TryParse(value[2], NumberStyles.None, Parameters.NumberCulture, out intValueBlue))    return false;
            if (intValueBlue < 0 || intValueBlue > 255)     return false;

            // successfull parsing, return true
            return true;

        }

    }

}
