using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UNP.Core.Params {
    
    public class ParamStringMat : Param, iParam {
        
        private string[][] values = new string[0][];

        public ParamStringMat(string name, string group, Parameters parentSet, string desc, string[] options) : base(name, group, parentSet, desc, options) { }

        public string getValue() {
            string strRet = "";
            for (int c = 0; c < this.values.Length; c++) {
                if (c != 0) strRet += ";";
                for (int r = 0; r < this.values.Length; r++) {
                    if (r != 0) strRet += " ";
                    strRet += this.values[c][r];
                }
            }
            return strRet;
        }

        public T getValue<T>() {

            Type paramType = typeof(T);
            if(paramType == typeof(string[][])) {     
                // request to return as string[][]

                // return vlaue
                return (T)Convert.ChangeType(Value, typeof(string[][]));

            } else {
                // request to return as other

                // message and return 0
                logger.Error("Could not retrieve the value for parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "') as '" + paramType.Name + "', can only return value as a matrix of strings (string[][]). Returning empty matrix");
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

        public string[][] Value {
            get {   return this.values;  }
        }

        
        public bool setStdValue(string stdValue)
        {
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
            ParamStringMat clone = new ParamStringMat(name, group, parentSet, desc, options);

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
