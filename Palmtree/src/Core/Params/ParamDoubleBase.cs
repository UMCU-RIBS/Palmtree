/**
 * The ParamDoubleBase class
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
using Palmtree.Core.Helpers;

namespace Palmtree.Core.Params {

    /// <summary>
    /// The <c>ParamDoubleBase</c> class.
    /// 
    /// ...
    /// </summary>
    public abstract class ParamDoubleBase : Param {

        protected double doubleMinValue = 0;
        protected Parameters.Units unitMinValue = Parameters.Units.ValueOrSamples;
        protected double doubleMaxValue = 0;
        protected Parameters.Units unitMaxValue = Parameters.Units.ValueOrSamples;

        public ParamDoubleBase(string name, string group, Parameters parentSet, string desc, string stdValue, string[] options) : base(name, group, parentSet, desc, stdValue, options) { }

        protected bool tryParseValue(string value, out double doubleValue, out Parameters.Units unit) {
            doubleValue = 0.0;
            unit = Parameters.Units.ValueOrSamples;
            
            // lower case and trim all whitespace
            value = value.ToLower().TrimAll();
            
            // check if value is in seconds
            if (value.Length > 1 && value.Substring(value.Length - 1).Equals("s")) {
                unit = Parameters.Units.Seconds;
                value = value.Substring(0, value.Length - 1);
            }

            // return false if the value is empty
            if (String.IsNullOrEmpty(value))    return false;

            // check if value is numeric and can be converted to a double
            // return false if unsucessful
            if (!double.TryParse(value, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, Parameters.NumberCulture, out doubleValue)) return false;
            
            // successfull parsing, return true
            return true;

        }


        public bool setMinValue(string minValue) {

            // check if a minimum is set
            if (!String.IsNullOrEmpty(minValue) && !minValue.Equals("%")) {
                // minimum is set

                // parse the minimum
                if (!tryParseValue(minValue, out doubleMinValue, out unitMinValue)) {
                    
                    // set the minvalue to be unlimited
                    this.minValue = "";

                    // return fail
                    return false;

                }

            }

            // store the minvalue
            this.minValue = minValue;

            // return success
            return true;

        }

        public bool setMaxValue(string maxValue) {

            // check if a maximum is set
            if (!String.IsNullOrEmpty(maxValue) && !maxValue.Equals("%")) {
                // maximum is set

                // parse the maximum
                if (!tryParseValue(minValue, out doubleMaxValue, out unitMaxValue)) {

                    // set the maxvalue to be unlimited
                    this.maxValue = "";

                    // return fail
                    return false;

                }

            }

            // store the maxvalue
            this.maxValue = maxValue;

            // return success
            return true;

        }

    }

}
