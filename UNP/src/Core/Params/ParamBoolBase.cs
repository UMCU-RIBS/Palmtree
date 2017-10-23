using System;

namespace UNP.Core.Params {

    public abstract class ParamBoolBase : Param {

        public ParamBoolBase(string name, string group, Parameters parentSet, string desc, string stdValue, string[] options) : base(name, group, parentSet, desc, stdValue, options) { }

    }

}
