using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using UNP.Applications;
using UNP.Filters;
using UNP.Sources;

namespace UNP.Core.Params {

    // ParameterManager class
    // 
    // Since all the calls in this class refer to (or give a reference to) the Dictionary object, this class is thread safe
    // 
    public static class ParameterManager {

        private static Logger logger = LogManager.GetLogger("ParameterManager");

        private static Dictionary<string, Parameters> parameterSets = new Dictionary<string, Parameters>(0);            // parameter-sets are stored as name (key) and Parameter (object) pairs in a dictionary, Dictionary collections are thread-safe

        public static Parameters GetParameters(string name, Parameters.ParamSetTypes type) {
            Parameters parameterSet = null;

            // try to retrieve the set
            if (!parameterSets.TryGetValue(name, out parameterSet)) {
                // set not found

                // create the set and add it to the dictionary
                parameterSet = new Parameters(name, type);
                parameterSets.Add(name, parameterSet);

            }

            // return the existing (or newly created) parameter set
            return parameterSet;

        }

        public static Dictionary<string, Parameters> getParameterSets() {
            return parameterSets;
        }
        public static Dictionary<string, Parameters> getParameterSetsClone() {

            // create a new empty Dictionary
            Dictionary<string, Parameters> parameterSetsClone = new Dictionary<string, Parameters>(0);

            // cycle through parameter sets and clone each set
            foreach (KeyValuePair<string, Parameters> entry in parameterSets) {
                parameterSetsClone.Add(entry.Key, entry.Value.clone());
            }
            
            // return the clone
            return parameterSetsClone;

        }




        // TODO: max: toch in ParameterManager (static hier om de source, filter en app references op te halen?)


        public static void saveParameterFile(string xmlFile, Dictionary<string, Parameters> parameterSets) {
            if (parameterSets == null) return;

            // check if the filename is not empty
            if (string.IsNullOrEmpty(xmlFile)) {

                // message
                logger.Error("Save parameter file called with invalid filename");

                // return
                return;

            }

            // TODO: indent with tabs

            // create parameter XML file with encoding declaration
            XmlDocument paramFile = new XmlDocument();
            XmlNode paramFileNode = paramFile.CreateXmlDeclaration("1.0", "UTF-8", null);
            paramFile.AppendChild(paramFileNode);

            // add root node
            XmlNode rootNode = paramFile.CreateElement("root");
            paramFile.AppendChild(rootNode);

            // VERSION NODES
            // create versions node and add to root node
            XmlNode versionsNode = paramFile.CreateElement("versions");
            rootNode.AppendChild(versionsNode);

            // get source name and version value, and create source version node 
            ISource source = MainThread.getSource();
            int sourceVersionValue = source.getClassVersion();
            string sourceVersionName = source.getClassName();
            XmlNode sourceVersionNode = paramFile.CreateElement("version");

            // add name attribute 
            XmlAttribute sourceNameAttr = paramFile.CreateAttribute("name");
            sourceNameAttr.Value = sourceVersionName;
            sourceVersionNode.Attributes.Append(sourceNameAttr);

            // add type attribute 
            XmlAttribute sourceTypeAttr = paramFile.CreateAttribute("type");
            sourceTypeAttr.Value = "source";
            sourceVersionNode.Attributes.Append(sourceTypeAttr);

            // add value attribute
            XmlAttribute sourceValueAttr = paramFile.CreateAttribute("value");
            sourceValueAttr.Value = sourceVersionValue.ToString();
            sourceVersionNode.Attributes.Append(sourceValueAttr);

            // add source version node to versions node
            versionsNode.AppendChild(sourceVersionNode);

            // loop through the filters
            List<IFilter> filters = MainThread.getFilters();
            for (int i = 0; i < filters.Count; i++) {

                // create filter version node
                XmlNode filterVersionNode = paramFile.CreateElement("version");

                // add name attribute 
                XmlAttribute filterNameAttr = paramFile.CreateAttribute("name");
                filterNameAttr.Value = filters[i].getName();
                filterVersionNode.Attributes.Append(filterNameAttr);

                // add type attribute 
                XmlAttribute filterTypeAttr = paramFile.CreateAttribute("type");
                filterTypeAttr.Value = "filter";
                filterVersionNode.Attributes.Append(filterTypeAttr);

                // add value attribute
                XmlAttribute fillterValueAttr = paramFile.CreateAttribute("value");
                fillterValueAttr.Value = filters[i].getClassVersion().ToString();
                filterVersionNode.Attributes.Append(fillterValueAttr);

                // add filter version node to versions node
                versionsNode.AppendChild(filterVersionNode);
            }

            // get application version and create application version node 
            IApplication application = MainThread.getApplication();
            int applicationVersionValue = application.getClassVersion();
            string applicationVersionName = application.getClassName();
            XmlNode applicationVersionNode = paramFile.CreateElement("version");

            // add name attribute 
            XmlAttribute applicationNameAttr = paramFile.CreateAttribute("name");
            applicationNameAttr.Value = applicationVersionName;
            applicationVersionNode.Attributes.Append(applicationNameAttr);

            // add type attribute 
            XmlAttribute applicationTypeAttr = paramFile.CreateAttribute("type");
            applicationTypeAttr.Value = "application";
            applicationVersionNode.Attributes.Append(applicationTypeAttr);

            // add value attribute
            XmlAttribute applicationValueAttr = paramFile.CreateAttribute("value");
            applicationValueAttr.Value = applicationVersionValue.ToString();
            applicationVersionNode.Attributes.Append(applicationValueAttr);

            // add application version node to versions node
            versionsNode.AppendChild(applicationVersionNode);

            // cycle through parameter sets
            foreach (KeyValuePair<string, Parameters> entry in parameterSets) {
                
                // create parameterSet node 
                XmlNode parameterSetNode = paramFile.CreateElement("parameterSet");

                // add name attribute to parameterSet node and set equal to set name
                XmlAttribute parameterSetName = paramFile.CreateAttribute("name");
                parameterSetName.Value = entry.Key;
                parameterSetNode.Attributes.Append(parameterSetName);

                // add parameterSet node to root node
                rootNode.AppendChild(parameterSetNode);

                // get Parameter object and iParams contained within
                Parameters parameterSet = entry.Value;
                List<iParam> parameters = parameterSet.getParameters();

                // loop throug the parameters in the set
                for (int p = 0; p < parameters.Count; p++) {
                    iParam parameter = parameters[p];

                    // create param node 
                    XmlNode paramNode = paramFile.CreateElement("param");

                    // add name attribute 
                    XmlAttribute paramNameAttr = paramFile.CreateAttribute("name");
                    paramNameAttr.Value = parameter.Name;
                    paramNode.Attributes.Append(paramNameAttr);

                    // add type attribute 
                    XmlAttribute paramTypeAttr = paramFile.CreateAttribute("type");
                    paramTypeAttr.Value = parameter.GetType().ToString();
                    paramNode.Attributes.Append(paramTypeAttr);

                    // add value attribute
                    XmlAttribute paramValueAttr = paramFile.CreateAttribute("value");
                    paramValueAttr.Value = parameter.ToString();
                    paramNode.Attributes.Append(paramValueAttr);

                    // add param node to parameterSet node
                    parameterSetNode.AppendChild(paramNode);

                }
                
            }

            try {

                // save xml string to file
                paramFile.Save(xmlFile);

                // message
                logger.Info("Saved parameter file: " + xmlFile);

            } catch (Exception e) {

                // message error
                logger.Error("Unable to save parameter file (" + xmlFile + "). " + e.Message);

            }

        }



        public static void loadParameterFile(string xmlFile, Dictionary<string, Parameters> parameterSets) {

            // check if the filename is not empty
            if (string.IsNullOrEmpty(xmlFile)) {

                // message
                logger.Error("Load parameter file called with invalid filename");

                return;
                
			}
            
            // initialize stream and XmlDocument object
            XmlDocument paramFile = new XmlDocument();

            // message
            logger.Info("Loaded parameter file: " + xmlFile);

            try {

                // load the xml file
                paramFile.Load(xmlFile);

			} catch (Exception) {
                
                // message
                logger.Error("Error: Could not read parameter file ('" + xmlFile + "')");

                // return failure
                return;

            }

            // get versions, output for debug
            //XmlNodeList versions = paramFile.GetElementsByTagName("version");
            //for (int v = 0; v < versions.Count; v++) { 
            //	logger.Debug("Current version of " + versions[v].Attributes["type"].Value + " " + versions[v].Attributes["name"].Value + " in parameter file: " + versions[v].Attributes["value"].Value);
            //}
            
            // get parametersets and params from xml
            XmlNodeList xmlParameterSets = paramFile.GetElementsByTagName("parameterSet");

            // for each parameter set
            for (int s = 0; s < xmlParameterSets.Count; s++) {

                // get name of set in the xml
                string parameterSetName = xmlParameterSets[s].Attributes["name"].Value;

                // load the local parameterset
                Parameters parameterSet = null;
                if (!parameterSets.TryGetValue(parameterSetName, out parameterSet)) {
                    // set not found

                    logger.Warn("The parameter set '" + parameterSetName + "' from the configuration file does not exist locally, skipping parameter set");

                    // skip this set
                    continue;

                }

                // set current parameters in set to values in xml
                if (xmlParameterSets[s].HasChildNodes) {

                    // cycle through all parameters in this set contained in xml
                    for (int p = 0; p < xmlParameterSets[s].ChildNodes.Count; p++) {

                        // get name, type and value of param
                        string xmlParamName = xmlParameterSets[s].ChildNodes[p].Attributes["name"].Value;
                        string xmlParamType = xmlParameterSets[s].ChildNodes[p].Attributes["type"].Value;
                        string xmlParamValue = xmlParameterSets[s].ChildNodes[p].Attributes["value"].Value;

                        // get current type of this param
                        string currType = parameterSet.getType(xmlParamName);

						// check if the type in xml is the same as the current type
                        if (xmlParamType == currType) {
                            
							// logger.Debug("Parameter " + xmlParamName + " is same type as currently registered.");
							
							// update value of param to value in xml
                            if (parameterSet.setValue(xmlParamName, xmlParamValue)) {
                                //logger.Debug("Parameter " + xmlParamName + " is updated to " + paramValue + " (value taken from parameter file.)");
								
                            }
							
							// update value of param to value in xml
                            // TODO: try catch
                            // TODO: give feedback in logger and Dialog that loading was succesful
                            // TODO relaod GUI so any updated values are immediately visible

                        } else {
							
							// message
                            logger.Error("The type of parameter '" + xmlParamName + "' is not of the same type as the parameter in the application, parameter not loaded from the file");
							
                        }

                        // debug
                        //logger.Debug(xmlParameterSets[s].ChildNodes[p].Attributes["name"].Value);
                        //logger.Debug(xmlParameterSets[s].ChildNodes[p].Attributes["type"].Value);
                        //logger.Debug(xmlParameterSets[s].ChildNodes[p].Attributes["value"].Value);

                    }
					
                }
				
            }

        }



    }

}
