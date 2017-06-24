using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UNP.Core.Params {

    public class ParamIntArr : ParamIntBase, iParam {

        private int[] values = new int[0];
        private Parameters.Units[] units = new Parameters.Units[0];

        public ParamIntArr(string name, string group, Parameters parentSet, string desc, string stdValue, string[] options) : base(name, group, parentSet, desc, stdValue, options) { }

        public string getValue() {
            string strRet = "";
            for (int i = 0; i < this.values.Length; i++) {
                if (i != 0)     strRet += Parameters.ArrDelimiters[0];
                strRet += this.values[i].ToString(Parameters.NumberCulture);
                strRet += (this.units[i] == Parameters.Units.Seconds ? "s" : "");
            }
            return strRet;
        }

        public T getValue<T>() {

            Type paramType = typeof(T);
            if(paramType == typeof(int[])) {     
                // request to return as int[]

                // return vlaue
                return (T)Convert.ChangeType(Value, typeof(int[]));

            } else {
                // request to return as other

                // message and return 0
                logger.Error("Could not retrieve the value for parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "') as '" + paramType.Name + "', can only return value as an array of integers (int[]). Returning empty array");
                return (T)Convert.ChangeType(0, typeof(T));    

            }
            
        }

        public T getUnit<T>() {

            Type paramType = typeof(T);
            if(paramType == typeof(Parameters.Units[])) {
                // request to return as Parameters.Units[]

                // return value
                return (T)Convert.ChangeType(units, typeof(Parameters.Units[]));

            } else {
                // request to return as other

                // message and return false
                logger.Error("Could not retrieve the unit for parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "') as '" + paramType.Name + "', can only return value as 'Parameters.Units[]'. Returning 0");
                return (T)Convert.ChangeType(0, typeof(T));    

            }

        }

        public int getValueInSamples() {
            
            // TODO:

            //
            return getValue<int>();

        }
        
        public override string ToString() {
            return getValue();
        }

        public int[] Value {
            get {   return this.values;  }
        }

        public Parameters.Units[] Unit {
            get {   return this.units;  }
        }

        public bool setValue(int[] values) {

            // re-initialize the buffer holding the units for the values
            units = new Parameters.Units[values.Length];

            // set the values
            this.values = values;

            // return success
            return true;

        }
        

        public bool tryValue(string value) {

            // try to split up the string
            string[] split = value.Split(Parameters.ArrDelimiters, StringSplitOptions.RemoveEmptyEntries);

            // parse the values as doubles
            for (int i = 0; i < split.Length; i++) {
                int intValue;
                Parameters.Units unit;
                if (!tryParseValue(split[i], out intValue, out unit)) return false;
            }

            // return success
            return true;

        }


        public bool setValue(string value) {

            // try to split up the string
            string[] split = value.Split(Parameters.ArrDelimiters, StringSplitOptions.RemoveEmptyEntries);

            // parse the values as integers
            int[] values = new int[split.Length];
            Parameters.Units[] units = new Parameters.Units[split.Length];
            for (int i = 0; i < split.Length; i++) {

                // try to parse the value
                int intValue;
                Parameters.Units unit;
                if (!tryParseValue(split[i], out intValue, out unit)) {

                    // message
                    logger.Error("Could not store the values for parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "'), the value(s) '" + value + "' could not be parsed as an array of integers due to value '" + split[i] + "'");
                
                    // return failure
                    return false;

                }

                // check if the parameter has options and the unit is set in seconds
                if (this.options.Length > 0 && unit == Parameters.Units.Seconds) {

                    // message
                    logger.Warn("Parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "') has a list of options, yet one of the values is specified in seconds, this is unlikely to be correct");
                    
                }

                // add to the array of integers
                values[i] = intValue;
                units[i] = unit;

            }

            // store the values
            this.values = values;
            this.units = units;

            // return success
            return true;

        }

        public bool setUnit(Parameters.Units[] units) {

            // check length
            if (values.Length != units.Length) {
                    
                // message
                logger.Error("Could not set the units for parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "'), the given length of the unit array does not match the length of the value array");
                    
                // return without doing anything
                return false;
                
            }

            // check if the parameter has options and any of the units are set in seconds
            if (this.options.Length > 0) {
                bool hasSeconds = false;
                for (int i = 0; i < units.Length; i++) if (units[i] == Parameters.Units.Seconds) hasSeconds = true;
                if (hasSeconds) {

                    // message
                    logger.Warn("Parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "') has a list of options, yet one of the values is specified in seconds, this is unlikely to be correct");
                    
                }
            }

            // set units
            this.units = units;

            // return success
            return true;

        }

        public iParam clone() {
            ParamIntArr clone = new ParamIntArr(name, group, parentSet, desc, stdValue, options);

            clone.stdValue = stdValue;
            
            clone.minValue = minValue;
            clone.intMinValue = intMinValue;
            clone.unitMinValue = unitMinValue;

            clone.maxValue = maxValue;
            clone.intMaxValue = intMaxValue;
            clone.unitMaxValue = unitMaxValue;

            clone.values = new int[values.Length];
            clone.units = new Parameters.Units[units.Length];
            for (int i = 0; i < values.Length; i++) {
                clone.values[i] = values[i];
                clone.units[i] = units[i];
            }

            return clone;
        }
        
    }

}
