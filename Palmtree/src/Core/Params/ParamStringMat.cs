/**
 * The ParamStringMat class
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
    /// The <c>ParamStringMat</c> class.
    /// 
    /// ...
    /// </summary>
    public class ParamStringMat : Param, iParam {
        
        private string[][] values = new string[0][];

        public ParamStringMat(string name, string group, Parameters parentSet, string desc, string stdValue, string[] options) : base(name, group, parentSet, desc, stdValue, options) { }

        public string getValue() {
            string strRet = "";
            for (int c = 0; c < values.Length; c++) {
                if (c != 0) strRet += Parameters.MatColumnDelimiters[0];
                for (int r = 0; r < values[c].Length; r++) {
                    if (r != 0) strRet += Parameters.MatRowDelimiters[0];
                    strRet += values[c][r];
                }
            }
            return strRet;
        }

        public T getValue<T>() {

            Type paramType = typeof(T);
            if(paramType == typeof(string[][])) {
                // request to return as string[][]

                // create a copy (since an array is passed by reference, and we don't want values being changed this way)
                string[][] cValues = new string[values.Length][];
                for (int c = 0; c < values.Length; c++) {
                    cValues[c] = new string[values[c].Length];
                    for (int r = 0; r < values[c].Length; r++) {
                        cValues[c][r] = values[c][r];
                    }
                }

                // return value
                return (T)Convert.ChangeType(cValues, typeof(string[][]));

            } else {
                // request to return as other

                // message and return 0
                logger.Error("Could not retrieve the value for parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "') as '" + paramType.Name + "', can only return value as a matrix of strings (string[][]). Returning empty matrix");
                return (T)Convert.ChangeType(0, typeof(T));    

            }
            
        }

        public T getUnit<T>() {

            Type paramType = typeof(T);
            if(paramType == typeof(Parameters.Units[][])) {
                // request to return as Parameters.Units

                // return value
                Parameters.Units[][] units = new Parameters.Units[values.Length][];
                for (int c = 0; c < values.Length; c++) {
                    units[c] = new Parameters.Units[values[c].Length];
                    for (int r = 0; r < values[c].Length; r++) {
                        units[c][r] = Parameters.Units.ValueOrSamples;
                    }
                }
                return (T)Convert.ChangeType(units, typeof(Parameters.Units[][]));

            } else {
                // request to return as other

                // message and return false
                logger.Error("Could not retrieve the unit for parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "') as '" + paramType.Name + "', can only return value as 'Parameters.Units[][]'. Returning 0");
                return (T)Convert.ChangeType(0, typeof(T));    

            } 

        }

        public T getValueInSamples<T>() {

            // message
            logger.Error("Trying to retrieve the value in samples for string[][] parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "') in number of samples, cannot convert strings, returning 0");

            // return value
            return (T)Convert.ChangeType(0, typeof(int));

        }

        public override string ToString() {
            return getValue();
        }

        public string[][] Value {
            get {   return this.values;  }
        }

        
        public bool setStdValue(string stdValue) {
            return true;
        }

        public bool setValue(string[][] values) {
            
            // check if options (fixed columns) are set and if the set matches the dimensions
            if (this.options.Length > 0 && this.options.Length != values.Length) {

                // message
                logger.Error("Could not store the values for parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "'), the number of columns in the value matrix does not match the options (fixed number of colums) set for the parameter");

                // return failure
                return false;

            }

            // set the values
            this.values = values;

            // return success
            return true;

        }

        public bool tryValue(string value) {

            if (String.IsNullOrEmpty(value))    return true;
            
            string[] splitColumns = value.Split(Parameters.MatColumnDelimiters);
            if (this.options.Length > 0 && this.options.Length != splitColumns.Length)  return false;
            
            return true;

        }

        public bool setValue(string value) {
            
            // check if the input is empty
            if (String.IsNullOrEmpty(value)) {

                // store empty matrices
                this.values = new string[0][];

                // return success
                return true;

            }

            // try to split up the columns of the string
            string[] splitColumns = value.Split(Parameters.MatColumnDelimiters);
            
            // check if options (fixed columns) are set and if the set matches the dimensions
            if (this.options.Length > 0 && this.options.Length != splitColumns.Length) {

                // message
                logger.Error("Could not store the values for parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "'), the number of columns in the value matrix does not match the options (fixed number of colums) set for the parameter");

                // return failure
                return false;

            }

            // resize the array columns
            string[][] values = new string[splitColumns.Length][];

            // parse the values as strings
            for (int i = 0; i < splitColumns.Length; i++) {
                
                // try to split up the rows of each column string
                string[] splitRows = splitColumns[i].Split(Parameters.MatRowDelimiters);

                // resize the arrays rows
                values[i] = new string[splitRows.Length];

                // loop through each row in the column (cell)
                for (int j = 0; j < splitRows.Length; j++) {

                    // add to the array of strings
                    values[i][j] = splitRows[j];

                }

            }

            // store the values
            this.values = values;

            // return success
            return true;

        }


        public iParam clone() {
            ParamStringMat clone = new ParamStringMat(name, group, parentSet, desc, stdValue, options);

            clone.stdValue = stdValue;

            clone.minValue = minValue;
            clone.maxValue = maxValue;
            
            clone.values = new string[values.Length][];
            for (int c = 0; c < values.Length; c++) {
                clone.values[c] = new string[values[c].Length];
                for (int r = 0; r < values[c].Length; r++) {
                    clone.values[c][r] = values[c][r];
                }
            }
            
            return clone;

        }

    }
    
}
