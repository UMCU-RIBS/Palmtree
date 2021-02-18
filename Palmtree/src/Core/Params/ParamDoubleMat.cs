/**
 * The ParamDoubleMat class
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
    /// The <c>ParamDoubleMat</c> class.
    /// 
    /// ...
    /// </summary>
    public class ParamDoubleMat : ParamDoubleBase, iParam {

        private double[][] values = new double[0][];
        private Parameters.Units[][] units = new Parameters.Units[0][];

        public ParamDoubleMat(string name, string group, Parameters parentSet, string desc, string stdValue, string[] options) : base(name, group, parentSet, desc, stdValue, options) { }

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
            if(paramType == typeof(double[][])) {
                // request to return as double[][]

                // create a copy (since an array is passed by reference, and we don't want values being changed this way)
                double[][] cValues = new double[values.Length][];
                for (int c = 0; c < values.Length; c++) {
                    cValues[c] = new double[values[c].Length];
                    for (int r = 0; r < values[c].Length; r++) {
                        cValues[c][r] = values[c][r];
                    }
                }

                // return vlaue
                return (T)Convert.ChangeType(cValues, typeof(double[][]));

            } else {
                // request to return as other

                // message and return 0
                logger.Error("Could not retrieve the values for parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "') as '" + paramType.Name + "', can only return values as a matrix of doubles (double[][]). Returning empty matrix");
                return (T)Convert.ChangeType(Parameters.emptyValue<T>(), typeof(T));    

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
                return (T)Convert.ChangeType(Parameters.emptyValue<T>(), typeof(T));    

            }

        }

        public T getValueInSamples<T>() {
            return getValueInSamples<T>(null);
        }
        
        public T getValueInSamples<T>(int[] ignoreColumns) {

            Type paramType = typeof(T);
            if (paramType == typeof(int[][]) || paramType == typeof(double[][])) {
                // request to return as int[][] or double[][]
                
                // retrieve the values in samples
                double[][] retValues = getValueInSamples(ignoreColumns);
                
                // return as int[][]
                if (paramType == typeof(int[][])) {
                    
                    // convert doubles to int
                    int[][] intRetValues = new int[retValues.Length][];
                    for (int c = 0; c < units.Length; c++) {
                        intRetValues[c] = new int[retValues[c].Length];
                        for (int r = 0; r < retValues[c].Length; r++) {
                            intRetValues[c][r] = (int)Math.Round(retValues[c][r]);
                            if (intRetValues[c][r] != retValues[c][r]) {
                                logger.Warn("A value in parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "') was rounded from a double (" + retValues[c][r] + ") to an integer (" + intRetValues[c][r] + ") with a loss of precision, retrieve as this parameter as double[][] to retain precision");
                            }
                        }
                    }

                    // return int[][]
                    return (T)Convert.ChangeType(intRetValues, typeof(int[][]));
                }

                // return as double[][]
                return (T)Convert.ChangeType(retValues, typeof(double[][]));

            } else {
                // request to return as other
                
                // message and return 0
                logger.Error("Could not retrieve the values (in samples) for parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "') as '" + paramType.Name + "', can only return values as double[][] or int[][]. Returning 0");
                return (T)Convert.ChangeType(Parameters.emptyValue<T>(), typeof(T));

            }

        }
        
        public double[][] getValueInSamples() {
            return getValueInSamples(null);
        }
        
        public double[][] getValueInSamples(int[] ignoreColumns) {
            
            // create an matrix of values to return
            double[][] retValues = new double[values.Length][];

            // loop through the columns
            for (int c = 0; c < units.Length; c++) {

                // check if column should be ignored, if so skip
                bool ignoreColumn = false;
                if (ignoreColumns != null && ignoreColumns.Length > 0) {
                    for (int j = 0; j < ignoreColumns.Length; j++) {
                        if (c == ignoreColumns[j]) {
                            ignoreColumn = true;
                            break;
                        }
                    }
                }

                // create array of values for this column
                retValues[c] = new double[values[c].Length];
                
                // loop through the rows
                for (int r = 0; r < values[c].Length; r++) {

                    if (ignoreColumn)

                        // just copy the value (unrounded original value)
                        retValues[c][r] = values[c][r];

                    else {
                    
                        // retrieve the value
                        double val = values[c][r];
                        int intSamples = 0;

                        // check if the unit is set in seconds
                        if (units[c][r] == Parameters.Units.Seconds) {
                            // flagged as seconds

                            // convert, check rounding
                            double samples = SampleConversion.timeToSamplesAsDouble(val);   // conversion result as double, no rounding before
                            intSamples = (int)Math.Round(samples);
                            if (samples != intSamples) {

                                // message
                                logger.Warn("Value in parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "') was retrieved in number of samples (" + val + "s * " + SampleConversion.sampleRate() + "Hz), but has been rounded from " + samples + " sample(s) to " + intSamples + " sample(s)");

                            }

                        } else {
                            // not flagged as seconds

                            // convert double to int, check rounding
                            intSamples = (int)Math.Round(val);
                            if (val != intSamples) {

                                // message
                                logger.Warn("Value in parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "') was retrieved in number of samples, but has been rounded from " + val + " sample(s) to " + intSamples + " samples");

                            }

                        }

                        // store the value in rounded samples
                        retValues[c][r] = intSamples;

                    }

                }

            }

            // return values
            return retValues;
    
        }

        public override string ToString() {
            return getValue();
        }

        public double[][] Value {
            get {   return this.values;  }
        }

        public Parameters.Units[][] Unit {
            get {   return this.units;  }
        }
        

        public bool setValue(double[][] values) {
            
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

            if (String.IsNullOrEmpty(value))    return true;
            
            string[] splitColumns = value.Split(Parameters.MatColumnDelimiters);
            if (this.options.Length > 0 && this.options.Length != splitColumns.Length)  return false;
            for (int i = 0; i < splitColumns.Length; i++) {
                string[] splitRows = splitColumns[i].Split(Parameters.MatRowDelimiters);
                for (int j = 0; j < splitRows.Length; j++) {
                    double doubleValue;
                    Parameters.Units unit;
                    if (!tryParseValue(splitRows[j], out doubleValue, out unit)) return false;
                }

            }

            // return success
            return true;

        }

        public bool setValue(string value) {
            
            // check if the input is empty
            if (String.IsNullOrEmpty(value)) {

                // store empty matrices
                this.values = new double[0][];
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
            double[][] values = new double[splitColumns.Length][];
            Parameters.Units[][] units = new Parameters.Units[splitColumns.Length][];

            // parse the values as doubles
            for (int i = 0; i < splitColumns.Length; i++) {
                
                // try to split up the rows of each column string
                string[] splitRows = splitColumns[i].Split(Parameters.MatRowDelimiters);

                // check whether the current row has the same number of values (as the row before it)
                if (i > 0 && splitRows.Length != values[i - 1].Length) {

                    // message
                    logger.Error("Could not store the values for parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "'), the rows have different numbers of values");

                    // return failure
                    return false;

                }

                // resize the arrays rows
                values[i] = new double[splitRows.Length];
                units[i] = new Parameters.Units[splitRows.Length];

                // loop through each row in the column (cell)
                for (int j = 0; j < splitRows.Length; j++) {

                    // try to parse the value
                    double doubleValue;
                    Parameters.Units unit;
                    if (!tryParseValue(splitRows[j], out doubleValue, out unit)) {

                        // message
                        logger.Error("Could not store the values for parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "'), the value(s) '" + splitRows[j] + "' could not be parsed as an array of doubles due to value '" + splitRows[j] + "'");
                
                        // return failure
                        return false;

                    }

                    // add to the array of doubles
                    values[i][j] = doubleValue;
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
            ParamDoubleMat clone = new ParamDoubleMat(name, group, parentSet, desc, stdValue, options);

            clone.stdValue = stdValue;
            
            clone.minValue = minValue;
            clone.doubleMinValue = doubleMinValue;
            clone.unitMinValue = unitMinValue;

            clone.maxValue = maxValue;
            clone.doubleMaxValue = doubleMaxValue;
            clone.unitMaxValue = unitMaxValue;

            clone.values = new double[values.Length][];
            clone.units = new Parameters.Units[units.Length][];
            for (int c = 0; c < values.Length; c++) {
                clone.values[c] = new double[values[c].Length];
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
