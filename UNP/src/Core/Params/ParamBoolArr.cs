/**
 * The ParamBoolArr class
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

namespace UNP.Core.Params {

    /// <summary>
    /// The <c>ParamBoolArr</c> class.
    /// 
    /// ...
    /// </summary>
    public class ParamBoolArr : ParamBoolBase, iParam {

        private bool[] values = new bool[0];

        public ParamBoolArr(string name, string group, Parameters parentSet, string desc, string stdValue, string[] options) : base(name, group, parentSet, desc, stdValue, options) {
            minValue = "0";
            maxValue = "1";
        }

        public string getValue() {
            string strRet = "";
            for (int i = 0; i < this.values.Length; i++) {
                if (i != 0)     strRet += Parameters.ArrDelimiters[0];
                strRet += (this.values[i] ? "1" : "0");
            }
            return strRet;
        }

        public T getValue<T>() {

            Type paramType = typeof(T);
            if(paramType == typeof(bool[])) {
                // request to return as bool[]

                // create a copy (since an array is passed by reference, and we don't want values being changed this way)
                bool[] cValues = new bool[values.Length];
                for (int i = 0; i < values.Length; i++) {
                    cValues[i] = values[i];
                }

                // return value
                return (T)Convert.ChangeType(cValues, typeof(bool[]));

            } else {
                // request to return as other

                // message and return false
                logger.Error("Could not retrieve the value for parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "') as '" + paramType.Name + "', can only return value as an array of booleans (bool[]). Returning empty array");
                return (T)Convert.ChangeType(false, typeof(T));    

            }
            
        }

        public T getUnit<T>() {

            Type paramType = typeof(T);
            if(paramType == typeof(Parameters.Units[])) {
                // request to return as Parameters.Units[]

                // return value
                Parameters.Units[] units = new Parameters.Units[values.Length];
                for (int i = 0; i < values.Length; i++) units[i] = Parameters.Units.ValueOrSamples;
                return (T)Convert.ChangeType(units, typeof(Parameters.Units[]));

            } else {
                // request to return as other

                // message and return false
                logger.Error("Could not retrieve the unit for parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "') as '" + paramType.Name + "', can only return value as 'Parameters.Units[]'. Returning 0");
                return (T)Convert.ChangeType(0, typeof(T));    

            }

        }

        public T getValueInSamples<T>() {

            // message
            logger.Error("Trying to retrieve the value in samples for bool[] parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "') in number of samples, cannot convert booleans, returning 0");

            // return value
            return (T)Convert.ChangeType(0, typeof(int));

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

        public bool tryValue(string value) {
            return true;
        }

        public bool setValue(string value) {

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

        public iParam clone() {
            ParamBoolArr clone = new ParamBoolArr(name, group, parentSet, desc, stdValue, options);
            
            clone.stdValue = stdValue;
            
            clone.minValue = minValue;
            clone.maxValue = maxValue;

            clone.values = new bool[values.Length];
            for (int i = 0; i < values.Length; i++)    clone.values[i] = values[i];

            return clone;
        }

    }

}
