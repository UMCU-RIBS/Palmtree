/**
 * The ParamIntMat class
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
using Palmtree.Core.Helpers;

namespace Palmtree.Core.Params {

    /// <summary>
    /// The <c>ParamIntMat</c> class.
    /// 
    /// ...
    /// </summary>
    public class ParamIntMat : ParamIntBase, iParam {

        private int[][] values = new int[0][];
        private Parameters.Units[][] units = new Parameters.Units[0][];

        public ParamIntMat(string name, string group, Parameters parentSet, string desc, string stdValue, string[] options) : base(name, group, parentSet, desc, stdValue, options) { }

        public string getValue() {
            string strRet = "";
            for (int c = 0; c < values.Length; c++) {
                if (c != 0) strRet += Parameters.MatColumnDelimiters[0];
                for (int r = 0; r < values[c].Length; r++) {
                    if (r != 0) strRet += Parameters.MatRowDelimiters[0];
                    strRet += values[c][r].ToString();
                    strRet += (units[c][r] == Parameters.Units.Seconds ? "s" : "");
                }
            }
            return strRet;
        }

        public T getValue<T>() {

            Type paramType = typeof(T);
            if(paramType == typeof(int[][])) {
                // request to return as int[][]

                // create a copy (since an array is passed by reference, and we don't want values being changed this way)
                int[][] cValues = new int[values.Length][];
                for (int c = 0; c < values.Length; c++) {
                    cValues[c] = new int[values[c].Length];
                    for (int r = 0; r < values[c].Length; r++) {
                        cValues[c][r] = values[c][r];
                    }
                }

                // return value
                return (T)Convert.ChangeType(cValues, typeof(int[][]));

            } else {
                // request to return as other

                // message and return 0
                logger.Error("Could not retrieve the value for parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "') as '" + paramType.Name + "', can only return value as a matrix of integers (int[][]). Returning empty matrix");
                return (T)Convert.ChangeType(0, typeof(T));    

            }
            
        }

        public T getUnit<T>() {

            Type paramType = typeof(T);
            if(paramType == typeof(Parameters.Units[][])) {
                // request to return as Parameters.Units[][]

                // create a copy (since an array is passed by reference, and we don't want values being changed this way)
                Parameters.Units[][] cUnits = new Parameters.Units[units.Length][];
                for (int c = 0; c < units.Length; c++) {
                    cUnits[c] = new Parameters.Units[units[c].Length];
                    for (int r = 0; r < units[c].Length; r++) {
                        cUnits[c][r] = units[c][r];
                    }
                }

                // return value
                return (T)Convert.ChangeType(cUnits, typeof(Parameters.Units[][]));

            } else {
                // request to return as other

                // message and return false
                logger.Error("Could not retrieve the unit for parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "') as '" + paramType.Name + "', can only return value as 'Parameters.Units[][]'. Returning 0");
                return (T)Convert.ChangeType(0, typeof(T));    

            }

        }

        public T getValueInSamples<T>() {

            Type paramType = typeof(T);
            if (paramType == typeof(int[][])) {
                // request to return as int[][]

                // return value
                return (T)Convert.ChangeType(getValueInSamples(), typeof(int[][]));

            } else {
                // request to return as other

                // message and return 0
                logger.Error("Could not retrieve the value in samples for parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "') as '" + paramType.Name + "', can only return value as int[][]. Returning 0");
                return (T)Convert.ChangeType(0, typeof(T));

            }

        }

        public int[][] getValueInSamples() {

            // create an matrix of values (in samples) to return
            int[][] retValues = new int[values.Length][];

            // loop through the columns
            for (int c = 0; c < units.Length; c++) {

                // create array of values (in samples) for this column
                retValues[c] = new int[values[c].Length];

                // loop through the rows
                for (int r = 0; r < values[c].Length; r++) {

                    // retrieve the value
                    int val = values[c][r];
                    int intSamples = 0;

                    // check if the unit is set in seconds
                    if (units[c][r] == Parameters.Units.Seconds) {
                        // flagged as seconds

                        // convert, check rounding
                        double samples = SampleConversion.timeToSamplesAsDouble(val);   // conversion result as double, no rounding before
                        intSamples = (int)Math.Round(samples);
                        if (samples != intSamples) {

                            // message
                            logger.Warn("Value for parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "') was retrieved in number of samples (" + val + "s * " + SampleConversion.sampleRate() + "Hz), but has been rounded from " + samples + " samples to " + intSamples + " samples");

                        }

                        // set the rounded value
                        retValues[c][r] = intSamples;

                    } else {
                        // not flagged as seconds

                        // assume the value is in samples and set the value
                        retValues[c][r] = val;

                    }

                }

            }

            // return number of samples
            return retValues;

        }

        public override string ToString() {
            return getValue();
        }

        public int[][] Value {
            get {   return this.values;  }
        }

        public Parameters.Units[][] Unit {
            get {   return this.units;  }
        }
        
        public bool setValue(int[][] values) {

            // check if options (fixed columns) are set and if the set matches the dimensions
            if (this.options.Length > 0 && this.options.Length != values.Length) {

                // message
                logger.Error("Could not store the values for parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "'), the number of columns in the value matrix does not match the options (fixed number of colums) set for the parameter");

                // return failure
                return false;

            }

            // re-initialize the buffer holding the units for the values
            units = new Parameters.Units[values.Length][];
            for (int i = 0; i < values.Length; i++) units[i] = new Parameters.Units[values[i].Length];

            // set the values
            this.values = values;

            // return success
            return true;

        }

        public bool tryValue(string value) {
            
            if (String.IsNullOrEmpty(value)) return true;
            
            string[] splitColumns = value.Split(Parameters.MatColumnDelimiters);
            if (this.options.Length > 0 && this.options.Length != splitColumns.Length)      return false;
            for (int i = 0; i < splitColumns.Length; i++) {
                string[] splitRows = splitColumns[i].Split(Parameters.MatRowDelimiters);
                for (int j = 0; j < splitRows.Length; j++) {
                    int intValue;
                    Parameters.Units unit;
                    if (!tryParseValue(splitRows[j], out intValue, out unit)) return false;
                }

            }

            // return success
            return true;

        }

        public bool setValue(string value) {

            // check if the input is empty
            if (String.IsNullOrEmpty(value)) {

                // store empty matrices
                this.values = new int[0][];
                this.units = new Parameters.Units[0][];

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
            int[][] values = new int[splitColumns.Length][];
            Parameters.Units[][] units = new Parameters.Units[splitColumns.Length][];

            // parse the values as doubles
            for (int i = 0; i < splitColumns.Length; i++) {
                
                // try to split up the rows of each column string
                string[] splitRows = splitColumns[i].Split(Parameters.MatRowDelimiters);

                // resize the arrays rows
                values[i] = new int[splitRows.Length];
                units[i] = new Parameters.Units[splitRows.Length];

                // loop through each row in the column (cell)
                for (int j = 0; j < splitRows.Length; j++) {

                    // try to parse the value
                    int intValue;
                    Parameters.Units unit;
                    if (!tryParseValue(splitRows[j], out intValue, out unit)) {

                        // message
                        logger.Error("Could not store the values for parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "'), the value(s) '" + splitRows[j] + "' could not be parsed as an array of integers due to value '" + splitRows[j] + "'");
                
                        // return failure
                        return false;

                    }

                    // add to the array of doubles
                    values[i][j] = intValue;
                    units[i][j] = unit;

                }

            }

            // store the values
            this.values = values;
            this.units = units;

            // return success
            return true;

        }

        public bool setUnit(Parameters.Units[][] units) {

            // check length
            bool lengthMismatch = values.Length != units.Length;
            if (!lengthMismatch && values.Length > 0) {
                for (int i = 0; i < values.Length; i++)
                    if (values[i].Length != units[i].Length) {
                        lengthMismatch = true;
                        break;
                    }
            }
            if (lengthMismatch) {
                    
                // message
                logger.Error("Could not set the units for parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "'), the dimensions of the unit array do not match the dimensions of the value array");
                    
                // return without doing anything
                return false;
                
            }

            // set units
            this.units = units;

            // return success
            return true;

        }

        public iParam clone() {
            ParamIntMat clone = new ParamIntMat(name, group, parentSet, desc, stdValue, options);

            clone.stdValue = stdValue;
            
            clone.minValue = minValue;
            clone.intMinValue = intMinValue;
            clone.unitMinValue = unitMinValue;

            clone.maxValue = maxValue;
            clone.intMaxValue = intMaxValue;
            clone.unitMaxValue = unitMaxValue;

            clone.values = new int[values.Length][];
            clone.units = new Parameters.Units[units.Length][];
            for (int c = 0; c < values.Length; c++) {
                clone.values[c] = new int[values[c].Length];
                clone.units[c] = new Parameters.Units[units[c].Length];
                for (int r = 0; r < values[c].Length; r++) {
                    clone.values[c][r] = values[c][r];
                    clone.units[c][r] = units[c][r];
                }
            }

            return clone;

        }

    }

}
