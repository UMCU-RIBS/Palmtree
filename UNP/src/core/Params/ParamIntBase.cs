using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UNP.Core.Helpers;

namespace UNP.Core.Params {

    public abstract class ParamIntBase : Param {
        
        protected int intStdValue = 0;
        protected Parameters.Units unitStdValue = Parameters.Units.ValueOrSamples;
        protected int intMinValue = 0;
        protected Parameters.Units unitMinValue = Parameters.Units.ValueOrSamples;
        protected int intMaxValue = 0;
        protected Parameters.Units unitMaxValue = Parameters.Units.ValueOrSamples;

        public ParamIntBase(string name, string group, Parameters parentSet, string desc, string[] options) : base(name, group, parentSet, desc, options) { }

        protected bool tryParseValue(string value, out int intValue, out Parameters.Units unit) {
            intValue = 0;
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

            // check if value is numeric and can be converted to an int
            // return false if unsucessful
            if (!int.TryParse(value, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, Parameters.NumberCulture, out intValue)) return false;

            // successfull parsing, return true
            return true;

        }

        public bool setMinValue(string minValue) {

            // check if a minimum is set
            if (!String.IsNullOrEmpty(minValue) && !minValue.Equals("%")) {
                // minimum is set

                // parse the minimum
                if (!tryParseValue(minValue, out intMinValue, out unitMinValue)) {
                    
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
                if (!tryParseValue(minValue, out intMaxValue, out unitMaxValue)) {

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


        public bool setStdValue(string stdValue) {

            // parse the standard value
            if (!tryParseValue(stdValue, out intStdValue, out unitStdValue)) {
                    
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


        /*
        protected bool checkMinimum(ref int doubleValue, ref Parameters.Units unit) {

            // check if there is no minimum, if no minimum return true
            if (String.IsNullOrEmpty(this.minValue) || this.minValue.Equals("%"))   return true;

            // 
            if (unit == unitMinValue) {

            }

            // check in which unit the value to check is set
            if (unit == Parameters.Units.ValueOrSamples) {
                // ValueOrSamples

                if (unitMinValue == Parameters.Units.ValueOrSamples) {



                } else {

                }


            } else {
                // Seconds



            }


            return true;

        }
        */



    }

}
