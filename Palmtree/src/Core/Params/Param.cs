/**
 * The Param class
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
using NLog;
using System;

namespace Palmtree.Core.Params {

    /// <summary>
    /// Param class.
    /// 
    /// ...
    /// </summary>
    public abstract class Param {

        protected static Logger logger = LogManager.GetLogger("Parameter");
        
        public struct ParamSideButton {
            public string name;
            public EventHandler clickEvent;
            public int width;
            public ParamSideButton(string name, int width, EventHandler clickEvent) {
                this.name = name;
                this.width = width;
                this.clickEvent = clickEvent;
            }
        }

        // parameter properties
        protected string name                       = "";
        protected string group                      = "";
        protected string desc                       = "";
        protected string stdValue                   = "";
        protected string minValue                   = "";
        protected string maxValue                   = "";
        protected string[] options                  = new string[0];
        protected ParamSideButton[] buttons         = null;
        protected Parameters parentSet              = null;

        public Param(string name, string group, Parameters parentSet, string desc, string stdValue, string[] options) {
            this.name = name;
            this.group = group;
            this.parentSet = parentSet;
            this.desc = desc;
            this.stdValue = stdValue;
            this.options = options;
            if (this.options == null) this.options = new string[0];
        }

        public Param(string name, string group, Parameters parentSet, string desc, string stdValue, string[] options, ParamSideButton[] buttons) {
            this.name = name;
            this.group = group;
            this.parentSet = parentSet;
            this.desc = desc;
            this.stdValue = stdValue;
            this.options = options;
            if (this.options == null) this.options = new string[0];
            this.buttons = buttons;
        }

        protected string getParentSetName() {
            if (parentSet == null)      return "unknown";
            return parentSet.ParamSetName;
        }

        public string   Name                    {   get {   return this.name;       }   }
        public string   Group                   {   get {   return this.group;      }   }
        public string   Desc                    {   get {   return this.desc;       }   }
        public string   MinValue                {   get {   return this.minValue;   }   }
        public string   MaxValue                {   get {   return this.maxValue;   }   }
        public string   StdValue                {   get {   return this.stdValue;   }   }
        public string[] Options                 {   get {   return this.options;    }   }
        public ParamSideButton[] Buttons        {   get {   return this.buttons;    }   }

    }

}
