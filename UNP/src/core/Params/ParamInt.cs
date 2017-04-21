using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UNP.Core.Params {

    public class ParamInt : ParamIntBase, iParam {

        private int value = 0;
        private Parameters.Units unit = Parameters.Units.ValueOrSamples;

        public ParamInt(String name, String group, Parameters parentSet, String desc, String[] options) : base(name, group, parentSet, desc, options) { }

        public String getValue() {
            return this.value.ToString(Parameters.NumberCulture) + (this.unit == Parameters.Units.Seconds ? "s" : "");
        }

        public T getValue<T>() {

            Type paramType = typeof(T);
            if(paramType == typeof(int)) {     
                // request to return as int

                // return value
                return (T)Convert.ChangeType(value, typeof(int));

            } else {
                // request to return as other

                // message and return 0
                logger.Error("Could not retrieve the value for parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "') as '" + paramType.Name + "', can only return value as integer. Returning 0");
                return (T)Convert.ChangeType(0, typeof(T));    

            }
            
        }

        public int getValueInSamples() {

            // retrieve the value as integer
            int val = getValue<int>();
            
            // check if the unit is set in seconds
            if (unit == Parameters.Units.Seconds) {
                // flagged as seconds

                // convert and return
                return val * MainThread.SamplesPerSecond();

            } else {
                // not flagged as seconds
                
                // assume the value is in samples and return the value
                return val;

            }

        }

        public override string ToString() {
            return getValue();
        }

        public int Value {
            get {   return this.value;  }
        }

        public Parameters.Units Unit {
            get {   return this.unit;  }
        }

        public bool setValue(int value) {
            this.value = value;
            return true;
        }

        public bool tryValue(String value) {
            int intValue;
            Parameters.Units unit;
            return tryParseValue(value, out intValue, out unit);
        }

        public bool setValue(String value) {

            // try to parse the value
            int intValue;
            Parameters.Units unit;
            if (!tryParseValue(value, out intValue, out unit)) {

                // message
                logger.Error("Could not store the value for parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "'), value '" + value + "' could not be parsed as an integer");
                
                // return failure
                return false;

            }

            // check if the parameter has options and the unit is set in seconds
            if (this.options.Length > 0 && unit == Parameters.Units.Seconds) {

                // message
                logger.Warn("Parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "') has a list of options, yet the value is specified in seconds, this is unlikely to be correct");
                    
            }

            // assign
            this.value = intValue;
            this.unit = unit;

            // return success
            return true;

        }

        public bool setUnit(Parameters.Units unit) {
            
            // check if the parameter has options and the unit is set in seconds
            if (this.options.Length > 0 && unit == Parameters.Units.Seconds) {

                // message
                logger.Warn("Parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "') has a list of options, yet the value set to be in seconds, this is unlikely to be correct");
                    
            }

            // set units
            this.unit = unit;

            // return success
            return true;

        }
        
    }

}
