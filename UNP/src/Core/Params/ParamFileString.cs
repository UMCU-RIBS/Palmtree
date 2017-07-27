using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UNP.Core.Params {

    public class ParamFileString : ParamString {

        public ParamFileString(string name, string group, Parameters parentSet, string desc, string stdValue, string[] options) : base(name, group, parentSet, desc, stdValue, options) {

        }

        public new iParam clone() {

            ParamFileString clone = new ParamFileString(name, group, parentSet, desc, stdValue, options);

            clone.stdValue = stdValue;

            clone.minValue = minValue;
            clone.maxValue = maxValue;

            clone.value = value;

            return clone;
        }

    }

}
