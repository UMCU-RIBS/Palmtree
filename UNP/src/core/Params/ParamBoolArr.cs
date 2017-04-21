using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UNP.Core.Params {

    public class ParamBoolArr : ParamBoolBase, iParam {

        private bool[] values = new bool[0];

        public ParamBoolArr(String name, String group, Parameters parentSet, String desc, String[] options) : base(name, group, parentSet, desc, options) {
            minValue = "0";
            maxValue = "1";
        }

        public String getValue() {
            String strRet = "";
            for (int i = 0; i < this.values.Length; i++) {
                if (i != 0)     strRet += " ";
                strRet += (this.values[i] ? "1" : "0");
            }
            return strRet;
        }

        public T getValue<T>() {

            Type paramType = typeof(T);
            if(paramType == typeof(bool[])) {     
                // request to return as bool[]

                // return value
                return (T)Convert.ChangeType(Value, typeof(bool[]));

            } else {
                // request to return as other

                // message and return false
                logger.Error("Could not retrieve the value for parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "') as '" + paramType.Name + "', can only return value as an array of booleans (bool[]). Returning empty array");
                return (T)Convert.ChangeType(false, typeof(T));    

            }
            
        }

        public int getValueInSamples() {

            // message
            logger.Warn("Trying to retrieve the value for bool[] parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "') in number of samples, use getValue<T>() instead");

            // try normal getValue
            return getValue<int>();

        }

        public override string ToString() {
            return getValue();
        }

        public bool[] Value {
            get {   return this.values;  }
        }

        public bool setValue(bool[] values) {
            this.values = values;
            return true;
        }

        public bool tryValue(String value) {
            return true;
        }

        public bool setValue(String value) {

            // try to split up the string
            string[] split = value.Split(Parameters.ArrDelimiters, StringSplitOptions.RemoveEmptyEntries);

            // parse the values as booleans
            bool[] values = new bool[split.Length];
            for (int i = 0; i < split.Length; i++) {
                split[i] = split[i].ToLower();
                values[i] = (split[i].Equals("1") || split[i].Equals("true"));
            }

            // store the values
            this.values = values;

            // return success
            return true;

        }

    }

}
