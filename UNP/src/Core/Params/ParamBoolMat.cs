using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UNP.Core.Params {

    public class ParamBoolMat : ParamBoolBase, iParam {

        private bool[][] values = new bool[0][];

        public ParamBoolMat(string name, string group, Parameters parentSet, string desc, string stdValue, string[] options) : base(name, group, parentSet, desc, stdValue, options) {
            minValue = "0";
            maxValue = "1";
        }

        public string getValue() {
            string strRet = "";
            for (int c = 0; c < this.values.Length; c++) {
                if (c != 0) strRet += Parameters.MatColumnDelimiters[0];
                for (int r = 0; r < this.values[c].Length; r++) {
                    if (r != 0) strRet += Parameters.MatRowDelimiters[0];
                    strRet += (this.values[c][r] ? "1" : "0");
                }
            }
            return strRet;
        }

        public T getValue<T>() {

            Type paramType = typeof(T);
            if(paramType == typeof(bool[][])) {     
                // request to return as bool[][]

                // return value
                return (T)Convert.ChangeType(Value, typeof(bool[][]));

            } else {
                // request to return as other

                // message and return false
                logger.Error("Could not retrieve the value for parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "') as '" + paramType.Name + "', can only return value as a matrix of booleans (bool[][]). Returning empty matrix");
                return (T)Convert.ChangeType(false, typeof(T));    

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

        public int getValueInSamples() {

            // message
            logger.Warn("Trying to retrieve the value for bool[][] parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "') in number of samples, use getValue<T>() instead");
            
            // try normal getValue
            return getValue<int>();

        }

        public override string ToString() {
            return getValue();
        }

        public bool[][] Value {
            get {   return this.values;  }
        }

        public bool setValue(bool[][] values) {

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
            if (this.options.Length > 0 && this.options.Length != splitColumns.Length)    return false;
            
            return true;

        }

        public bool setValue(string value) {

            // check if the input is empty
            if (String.IsNullOrEmpty(value)) {

                // store empty matrices
                this.values = new bool[0][];

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
            bool[][] values = new bool[splitColumns.Length][];

            // parse the values as doubles
            for (int i = 0; i < splitColumns.Length; i++) {
                
                // try to split up the rows of each column string
                string[] splitRows = splitColumns[i].Split(Parameters.MatRowDelimiters);

                // resize the arrays rows
                values[i] = new bool[splitRows.Length];

                // loop through each row in the column (cell)
                for (int j = 0; j < splitRows.Length; j++) {

                    // try to parse the value
                    splitRows[j] = splitRows[j].ToLower();
                    values[i][j] = (splitRows[j].Equals("1") || splitRows[j].Equals("true"));

                }

            }

            // store the values
            this.values = values;

            // return success
            return true;

        }

        public iParam clone() {
            ParamBoolMat clone = new ParamBoolMat(name, group, parentSet, desc, stdValue, options);

            clone.stdValue = stdValue;
            
            clone.minValue = minValue;
            clone.maxValue = maxValue;

            clone.values = new bool[values.Length][];
            for (int c = 0; c < values.Length; c++) {
                clone.values[c] = new bool[values[c].Length];
                for (int r = 0; r < values[c].Length; r++) {
                    clone.values[c][r] = values[c][r];
                }
            }

            return clone;
        }

    }

}
