using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UNP.Core.Params {

    public abstract class ParamBoolBase : Param {
        private bool boolStdValue = false;

        public ParamBoolBase(String name, String group, Parameters parentSet, String desc, String[] options) : base(name, group, parentSet, desc, options) { }

        public bool setStdValue(String stdValue) {

            // make lowercase and store the standardvalue
            this.stdValue = stdValue.ToLower();

            // interpret the standard value 
            this.boolStdValue = (this.stdValue.Equals("1") || this.stdValue.Equals("true"));

            // return true
            return true;

        }

    }

}
