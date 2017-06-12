using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UNP.Core.Helpers;

namespace UNP.Core.Params {

    public class ParamInt : ParamIntBase, iParam {

        private int value = 0;
        private Parameters.Units unit = Parameters.Units.ValueOrSamples;

        public ParamInt(string name, string group, Parameters parentSet, string desc, string stdValue, string[] options) : base(name, group, parentSet, desc, stdValue, options) { }

        public string getValue() {
            return this.value.ToString(Parameters.NumberCulture) + (this.unit == Parameters.Units.Seconds ? "s" : "");
        }

        public T getValue<T>() {

            Type paramType = typeof(T);
            if(paramType == typeof(int)) {     
                // request to return as int

                // return value
                return (T)Convert.ChangeType(Value, typeof(int));

            } else {
                // request to return as other

                // message and return 0
                logger.Error("Could not retrieve the value for parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "') as '" + paramType.Name + "', can only return value as integer. Returning 0");
                return (T)Convert.ChangeType(0, typeof(T));    

            }
            
        }

        public T getUnit<T>() {

            Type paramType = typeof(T);
            if(paramType == typeof(Parameters.Units)) {
                // request to return as Parameters.Units

                // return value
                return (T)Convert.ChangeType(Unit, typeof(Parameters.Units));

            } else {
                // request to return as other

                // message and return false
                logger.Error("Could not retrieve the unit for parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "') as '" + paramType.Name + "', can only return value as 'Parameters.Units'. Returning 0");
                return (T)Convert.ChangeType(0, typeof(T));    

            }

        }

        public int getValueInSamples() {

            // retrieve the value as integer
            int val = getValue<int>();
            int intSamples = 0;

            // check if the unit is set in seconds
            if (unit == Parameters.Units.Seconds) {
                // flagged as seconds

                // convert, check rounding
                double samples = SampleConversion.timeToSamplesAsDouble(val);   // conversion result as double, no rounding before
                intSamples = (int)Math.Round(samples);
                if (samples != intSamples) {

                    // message
                    logger.Warn("Value for parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "') was retrieved in number of samples (" + val + "s * " + SampleConversion.sampleRate() + "Hz), but has been rounded from " + samples + " samples to " + intSamples + " samples");

                }

                // convert and return
                return intSamples;

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

        public bool tryValue(string value) {
            int intValue;
            Parameters.Units unit;
            return tryParseValue(value, out intValue, out unit);
        }

        public bool setValue(string value) {

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

        public iParam clone() {
            ParamInt clone = new ParamInt(name, group, parentSet, desc, stdValue, options);

            clone.stdValue = stdValue;
            
            clone.minValue = minValue;
            clone.intMinValue = intMinValue;
            clone.unitMinValue = unitMinValue;

            clone.maxValue = maxValue;
            clone.intMaxValue = intMaxValue;
            clone.unitMaxValue = unitMaxValue;

            clone.value = value;
            clone.unit = unit;

            return clone;
        }
        
    }

}
