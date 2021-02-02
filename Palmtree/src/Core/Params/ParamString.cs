/**
 * The ParamString class
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

namespace Palmtree.Core.Params {

    /// <summary>
    /// The <c>ParamString</c> class.
    /// 
    /// ...
    /// </summary>
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
