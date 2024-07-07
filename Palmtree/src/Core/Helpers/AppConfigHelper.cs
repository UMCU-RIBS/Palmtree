/**
 * AppConfig helper classes
 * 
 * ....
 * 
 * Copyright (C) 2021:  RIBS group (Nick Ramsey Lab), University Medical Center Utrecht (The Netherlands) & external contributors
 * Author(s):           Max van den Boom            (info@maxvandenboom.nl)
 * 
 * This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software
 * Foundation, either version 3 of the License, or (at your option) any later version. This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for
 * more details. You should have received a copy of the GNU General Public License along with this program. If not, see <https://www.gnu.org/licenses/>.
 */
using System.Collections.Generic;
using System.Configuration;
using System.Linq;

namespace Palmtree.Core.Helpers.AppConfig {
    
    public class FilterConfigurationElement : ConfigurationElement {

        [ConfigurationProperty("name", IsKey = true, IsRequired = true)]
        public string Name {
            get {   return (string)base["name"];    }
            set {   base["name"] = value;           }
        }
    
        [ConfigurationProperty("type", IsRequired = true)]
        public string Type {
            get {   return (string)base["type"];    }
            set {   base["type"] = value;           }
        }
    
    }


    public class FilterConfigurationCollection : ConfigurationElementCollection {
        /*
        public List<FilterConfigurationElement> toList() { 
                List<FilterConfigurationElement> allElements = new List<FilterConfigurationElement>(this.Count);
                for (int i = 0; i < this.Count; i++)    allElements[i] = (FilterConfigurationElement).BaseGet(i);
                return allElements;
            }
            */
        public List<FilterConfigurationElement> All { get { return this.Cast<FilterConfigurationElement>().ToList(); } }

        public FilterConfigurationElement this[int index] {
            get {   return (FilterConfigurationElement)BaseGet(index);  }
            set {
                if (BaseGet(index) != null)
                    BaseRemoveAt(index);
                BaseAdd(index, value);
            }
        }
 
        public new FilterConfigurationElement this[string key] {
            get {   return (FilterConfigurationElement)BaseGet(key);    }
            set {
                if (BaseGet(key) != null)
                    BaseRemoveAt(BaseIndexOf(BaseGet(key)));
                BaseAdd(value);
            }
        }
        
        protected override ConfigurationElement CreateNewElement() {
            return new FilterConfigurationElement();
        }
 
        protected override object GetElementKey(ConfigurationElement element) {
            return ((FilterConfigurationElement)element).Name;
        }

    }

    public class PipelineConfigurationSection : ConfigurationSection {
        public static PipelineConfigurationSection Pipeline => ConfigurationManager.GetSection("Pipeline") as PipelineConfigurationSection;


        [ConfigurationProperty("Source", IsRequired = false, IsDefaultCollection = true)]
        public KeyValueConfigurationElement Source {
            get { return (KeyValueConfigurationElement)this["Source"]; }
            set { this["Source"] = value; }
        }

        [ConfigurationProperty("Filters", IsRequired = false, IsDefaultCollection = true)]
        public FilterConfigurationCollection Filters {
            get { return (FilterConfigurationCollection)this["Filters"]; }
            set { this["Filters"] = value; }
        }

    }

}
