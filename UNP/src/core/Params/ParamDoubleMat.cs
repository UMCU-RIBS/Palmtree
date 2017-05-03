using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UNP.Core.Params {

    public class ParamDoubleMat : ParamDoubleBase, iParam {

        private double[][] values = new double[0][];
        private Parameters.Units[][] units = new Parameters.Units[0][];

        public ParamDoubleMat(string name, string group, Parameters parentSet, string desc, string[] options) : base(name, group, parentSet, desc, options) { }

        public string getValue() {
            string strRet = "";
            for (int c = 0; c < this.values.Length; c++) {
                if (c != 0) strRet += ";";
                for (int r = 0; r < this.values.Length; r++) {
                    if (r != 0) strRet += " ";

                    strRet += this.values[c][r].ToString(Parameters.NumberCulture);
                    strRet += (this.units[c][r] == Parameters.Units.Seconds ? "s" : "");
                }
            }
            return strRet;
        }

        public T getValue<T>() {

            Type paramType = typeof(T);
            if(paramType == typeof(double[][])) {     
                // request to return as double[][]

                // return vlaue
                return (T)Convert.ChangeType(Value, typeof(double[][]));

            } else {
                // request to return as other

                // message and return 0
                logger.Error("Could not retrieve the value for parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "') as '" + paramType.Name + "', can only return value as a matrix of doubles (double[][]). Returning empty matrix");
                return (T)Convert.ChangeType(0, typeof(T));    

            }
            
        }


        public int getValueInSamples() {
            
            // TODO: 
            
            // 
            return getValue<int>();
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
            ParamDoubleMat clone = new ParamDoubleMat(name, group, parentSet, desc, options);

            clone.stdValue = stdValue;
            clone.doubleStdValue = doubleStdValue;
            clone.unitStdValue = unitStdValue;
            
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
