using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UNP.Core.Helpers;

namespace UNP.Core.Params {

    public class ParamColor : Param, iParam {

        private RGBColorFloat value = new RGBColorFloat();

        public ParamColor(string name, string group, Parameters parentSet, string desc, string[] options) : base(name, group, parentSet, desc, options) {
            minValue = "0";
            maxValue = "16777216";
        }

        public string getValue() {
            return "";
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

        public int getValueInSamples() {


            // message
            logger.Warn("Trying to retrieve the value for color parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "') in number of samples, use getValue<T>() instead, returning 0");
            
            // return 0
            return 0;

        }

        public override string ToString() {
            return getValue();
        }

        public RGBColorFloat Value {
            get {   return this.value;  }
        }

        public bool setStdValue(string stdValue) {
            return true;
        }

        public bool setValue(RGBColorFloat value) {
            this.value = value;
            return true;
        }

        public bool tryValue(string value) {
            // TODO:
            return true;
        }

        public bool setValue(string value) {
            // TODO:
            return true;
        }
        
    }

}
