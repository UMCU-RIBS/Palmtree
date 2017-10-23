using NLog;
using System;

namespace UNP.Core.Params {

    public abstract class Param {

        protected static Logger logger = LogManager.GetLogger("Parameter");

        // parameter properties
        protected string name = "";
        protected string group = "";
        protected string desc = "";
        protected string stdValue = "";
        protected string minValue = "";
        protected string maxValue = "";
        protected string[] options = new string[0];
        protected Parameters parentSet = null;

        public Param(string name, string group, Parameters parentSet, string desc, string stdValue, string[] options) {
            this.name = name;
            this.group = group;
            this.parentSet = parentSet;
            this.desc = desc;
            this.stdValue = stdValue;
            this.options = options;
            if (this.options == null) this.options = new string[0];
        }

        protected string getParentSetName() {
            if (parentSet == null)      return "unknown";
            return parentSet.ParamSetName;
        }

        public string   Name      {   get {   return this.name;       }   }
        public string   Group     {   get {   return this.group;      }   }
        public string   Desc      {   get {   return this.desc;       }   }
        public string   MinValue  {   get {   return this.minValue;   }   }
        public string   MaxValue  {   get {   return this.maxValue;   }   }
        public string   StdValue  {   get {   return this.stdValue;   }   }
        public string[] Options   {   get {   return this.options;    }   }

    }

}
