using System;

namespace UNP.Core.Params {

    public class ParamString : Param, iParam {

        protected string value = "";

        public ParamString(string name, string group, Parameters parentSet, string desc, string stdValue, string[] options) : base(name, group, parentSet, desc, stdValue, options) {
            minValue = "";
            maxValue = "";
        }

        public string getValue() {
            return this.value;
        }

        public T getValue<T>() {

            Type paramType = typeof(T);
            if(paramType == typeof(string)) {     
                // request to return as string

                // return value
                return (T)Convert.ChangeType(Value, typeof(string));

            } else {
                // request to return as other

                // message and return 0
                logger.Error("Could not retrieve the value for parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "') as '" + paramType.Name + "', can only return value as string. Returning empty string");
                return (T)Convert.ChangeType("", typeof(T));    

            }
            
        }

        public T getUnit<T>() {

            Type paramType = typeof(T);
            if (paramType == typeof(Parameters.Units)) {
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
            logger.Error("Trying to retrieve the value in samples for string parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "') in number of samples, cannot convert a boolean, returning 0");

            // return value
            return (T)Convert.ChangeType(0, typeof(int));

        }

        public override string ToString() {
            return getValue();
        }

        public string Value {
            get {   return this.value;  }
        }

        public bool tryValue(string value) {
            return true;
        }

        public bool setValue(string value) {

            // assign
            this.value = value;

            // return success
            return true;

        }

        public iParam clone() {
            ParamString clone = new ParamString(name, group, parentSet, desc, stdValue, options);

            clone.stdValue = stdValue;
            
            clone.minValue = minValue;
            clone.maxValue = maxValue;
            
            clone.value = value;

            return clone;
        }
        
    }

}
