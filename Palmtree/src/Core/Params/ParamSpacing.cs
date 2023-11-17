/**
 * The ParamSpacing class
 * 
 * ...
 * 
 * 
 * Copyright (C) 2022:  RIBS group (Nick Ramsey Lab), University Medical Center Utrecht (The Netherlands) & external contributors
 * Author(s):           Max van den Boom            (info@maxvandenboom.nl)
 * 
 * This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software
 * Foundation, either version 3 of the License, or (at your option) any later version. This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for
 * more details. You should have received a copy of the GNU General Public License along with this program. If not, see <https://www.gnu.org/licenses/>.
 */
using System;
using System.Globalization;

namespace Palmtree.Core.Params {

    /// <summary>
    /// The <c>ParamSpacing</c> class.
    /// 
    /// ...
    /// </summary>
    public class ParamSpacing : ParamIntBase, iParam {

        private int value = 0;

        public ParamSpacing(int height, string group, Parameters parentSet) : base("spacing", group, parentSet, "", "", null) {
            value = height;
        }

        public string getValue() {
            return value.ToString();
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
                logger.Error("Could not retrieve the value for spacing parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "') as '" + paramType.Name + "'. Returning 0");
                return (T)Convert.ChangeType(Parameters.emptyValue<T>(), typeof(T));

            }
            
        }

        public T getUnit<T>() {


            Type paramType = typeof(T);
            if(paramType == typeof(Parameters.Units)) {
                // request to return as Parameters.Units

                // return value
                return (T)Convert.ChangeType(Parameters.Units.ValueOrSamples, typeof(Parameters.Units));

            } else {
                // request to return as other

                // message and return false
                logger.Error("Could not retrieve the unit for spacing parameter (parameter set: '" + this.getParentSetName() + "'), can only return value as 'Parameters.Units'. Returning 0");
                return (T)Convert.ChangeType(Parameters.emptyValue<T>(), typeof(T));    

            }
            
        }

        public T getValueInSamples<T>() {

            // message
            logger.Error("Trying to retrieve the value in samples on a spacing parameter (parameter set: '" + this.getParentSetName() + "'), cannot convert spacing. Returning empty string");

            // return value
            return (T)Convert.ChangeType(Parameters.emptyValue<T>(), typeof(T));

        }

        public override string ToString() {
            return "Spacing with height of " + value;
        }

        public string Value {
            get {   return this.value.ToString();  }
        }

        public bool tryValue(string value) {
            return true;
        }

        public bool setValue(string value) {

            // try to parse the value
            int intValue;
            Parameters.Units unit;
            if (!tryParseValue(value, out intValue, out unit)) {

                // message
                logger.Error("Could not store the value for spacing parameter (parameter set: '" + this.getParentSetName() + "'), value '" + value + "' could not be parsed as an integer");
                
                // return failure
                return false;

            }

            // assign
            this.value = intValue;

            // return success
            return true;

        }

        public iParam clone() {
            return new ParamSpacing(value, group, parentSet);
        }
    }

}
