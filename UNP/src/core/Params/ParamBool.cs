using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UNP.Core.Params {

    public class ParamBool : ParamBoolBase, iParam {

        private bool value = false;

        public ParamBool(String name, String group, Parameters parentSet, String desc, String[] options) : base(name, group, parentSet, desc, options) {
            minValue = "0";
            maxValue = "1";
        }

        public String getValue() {
            return (this.value ? "1" : "0");
        }

        public T getValue<T>() {

            Type paramType = typeof(T);
            if(paramType == typeof(bool)) {     
                // request to return as bool

                // return value
                return (T)Convert.ChangeType(Value, typeof(bool));

            } else {
                // request to return as other

                // message and return false
                logger.Error("Could not retrieve the value for parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "') as '" + paramType.Name + "', can only return value as boolean. Returning false");
                return (T)Convert.ChangeType(false, typeof(T));    

            }
            
        }


        public int getValueInSamples() {

            // message
            logger.Error("Trying to retrieve the value for bool parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "') in number of samples, use getValue<T>() instead, returning 0");

            // return 0
            return 0;

        }

        public override string ToString() {
            return getValue();
        }

        public bool Value {
            get {   return this.value;  }
        }

        public bool setValue(bool value) {
            this.value = value;
            return true;
        }

        public bool tryValue(String value) {
            return true;
        }

        public bool setValue(String value) {
            value = value.ToLower();
            this.value = (value.Equals("1") || value.Equals("true"));
            return true;
        }

    }

}
