using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UNP.Core.Params {

    public abstract class Param {

        protected static Logger logger = LogManager.GetLogger("Parameter");

        // parameter properties
        private String name     = "";
        private String group    = "";
        private String desc     = "";
        protected String stdValue = "";
        protected String minValue = "";
        protected String maxValue = "";
        protected String[] options = new String[0];
        private Parameters parentSet = null;

        public Param(String name, String group, Parameters parentSet, String desc, String[] options) {
            this.name = name;
            this.group = group;
            this.parentSet = parentSet;
            this.desc = desc;
            this.options = options;
        }

        protected String getParentSetName() {
            if (parentSet == null)      return "unknown";
            return parentSet.ParamSetName;
        }

        public String   Name      {   get {   return this.name;       }   }
        public String   Group     {   get {   return this.group;      }   }
        public String   Desc      {   get {   return this.desc;       }   }
        public String   MinValue  {   get {   return this.minValue;   }   }
        public String   MaxValue  {   get {   return this.maxValue;   }   }
        public String   StdValue  {   get {   return this.stdValue;   }   }
        public String[] Options   {   get {   return this.options;    }   }

    }

}
