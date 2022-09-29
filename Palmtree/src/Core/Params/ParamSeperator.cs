/**
 * The ParamSeperator class
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

namespace Palmtree.Core.Params {

    /// <summary>
    /// The <c>ParamSeperator</c> class.
    /// 
    /// ...
    /// </summary>
    public class ParamSeperator : Param, iParam {

        protected string value = "";

        public ParamSeperator(string name, string group, Parameters parentSet) : base(name, group, parentSet, "", "", null) {
            value = name;
        }
        public string getValue() {
            return value;
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
            return default(T);
        }

        public T getValueInSamples<T>() {

            // message
            logger.Error("Trying to retrieve the value in samples for a seperator with the text '" + this.Value + "' (parameter set: '" + this.getParentSetName() + "') in number of samples, cannot convert a seperator. Returning empty string");

            // return value
            return (T)Convert.ChangeType(Parameters.emptyValue<T>(), typeof(T));

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
            return true;
        }

        public iParam clone() {
            return new ParamSeperator(name, group, parentSet);
        }
    }

}
