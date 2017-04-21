using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UNP.Core.Helpers;

namespace UNP.Core.Params {

    public abstract class ParamDoubleBase : Param {

        private double doubleStdValue = 0;
        private Parameters.Units unitStdValue = Parameters.Units.ValueOrSamples;
        private double doubleMinValue = 0;
        private Parameters.Units unitMinValue = Parameters.Units.ValueOrSamples;
        private double doubleMaxValue = 0;
        private Parameters.Units unitMaxValue = Parameters.Units.ValueOrSamples;

        public ParamDoubleBase(String name, String group, Parameters parentSet, String desc, String[] options) : base(name, group, parentSet, desc, options) { }

        protected bool tryParseValue(String value, out double doubleValue, out Parameters.Units unit) {
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
            if (!double.TryParse(value, NumberStyles.AllowDecimalPoint, Parameters.NumberCulture, out doubleValue)) return false;
            
            // successfull parsing, return true
            return true;

        }


        public bool setMinValue(String minValue) {

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

        public bool setMaxValue(String maxValue) {

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

        public bool setStdValue(String stdValue) {

            // parse the standard value
            if (!tryParseValue(stdValue, out doubleStdValue, out unitStdValue)) {
                    
                // set the stdvalue to be unlimited
                this.stdValue = "";

                // return fail
                return false;

            }

            // make lowercase and store the stdvalue
            this.stdValue = stdValue.ToLower();

            // return success
            return true;

        }

    }

}
