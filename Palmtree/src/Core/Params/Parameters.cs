/**
 * The Parameters class
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
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Palmtree.Core.Helpers;

namespace Palmtree.Core.Params {

    /// <summary>
    /// The <c>Parameters</c> class.
    /// 
    /// ...
    /// </summary>
    public sealed class Parameters {

        public static Char[] ArrDelimiters = new Char[] { ' ', ',', ';', '\n' };
        public static Char[] MatColumnDelimiters = new Char[] { ';', '\n' };
        public static Char[] MatRowDelimiters = new Char[] { ',' };
        public static CultureInfo NumberCulture = (CultureInfo)Thread.CurrentThread.CurrentCulture.Clone();

        private static Logger logger = LogManager.GetLogger("Parameters");

        private string paramSetName = "";
        private ParamSetTypes paramSetType = ParamSetTypes.Source;

        private List<iParam>    paramList = new List<iParam>(0);
        private static Object   lockParameters = new Object();                         // threadsafety lock for all event on the parameters

        public enum ParamSetTypes : int {
            Data = 0,
            Source = 1,
            Filter = 2,
            Application = 3,
            Plugin = 4
        }

        public enum Units : int {
            ValueOrSamples = 0,
            Seconds = 1
        }

        public Parameters(string paramSetName, ParamSetTypes paramSetType) {
            this.paramSetName = paramSetName;
            this.paramSetType = paramSetType;
        }

        public string ParamSetName {
            get { return this.paramSetName; }
        }
        
        public ParamSetTypes ParamSetType {
            get { return this.paramSetType; }
        }

        public List<iParam> getParameters() {
            return paramList;
        }
        
        public iParam addParameter<T>(string name, string desc) {
            return addParameter<T>(name, "", desc, "", "", "");
        }        
        public iParam addParameter<T>(string name, string desc, string[] options) {
            return addParameter<T>(name, "", desc, "", "", "", options);
        }
        public iParam addParameter<T>(string name, string desc, string stdValue) {
            return addParameter<T>(name, "", desc, "", "", stdValue, new string[0]);
        }
        public iParam addParameter<T>(string name, string desc, string stdValue, string[] options) {
            return addParameter<T>(name, "", desc, "", "", stdValue, options);
        }
        public iParam addParameter<T>(string name, string desc, string minValue, string maxValue) {
            return addParameter<T>(name, "", desc, minValue, maxValue, "", new string[0]);
        }
        public iParam addParameter<T>(string name, string desc, string minValue, string maxValue, string stdValue) {
            return addParameter<T>(name, "", desc, minValue, maxValue, stdValue, new string[0]);
        }
        public iParam addParameter<T>(string name, string desc, string minValue, string maxValue, string[] options) {
            return addParameter<T>(name, "", desc, minValue, maxValue, "", options);
        }
        public iParam addParameter<T>(string name, string desc, string minValue, string maxValue, string stdValue, string[] options) {
            return addParameter<T>(name, "", desc, minValue, maxValue, stdValue, options);
        }
        public iParam addParameter<T>(string name, string desc, string minValue, string maxValue, string stdValue, string[] options, Param.ParamSideButton[] buttons) {
            return addParameter<T>(name, "", desc, minValue, maxValue, stdValue, options, buttons);
        }
        public iParam addParameter<T>(string name, string group, string desc, string minValue, string maxValue, string stdValue) {
            return addParameter<T>(name, group, desc, minValue, maxValue, stdValue, new string[0]);
        }
        public iParam addParameter<T>(string name, string group, string desc, string minValue, string maxValue, string stdValue, string[] options) {
            return addParameter<T>(name, group, desc, minValue, maxValue, stdValue, options, null);
        }
        public iParam addParameter<T>(string name, string group, string desc, string minValue, string maxValue, string stdValue, string[] options, Param.ParamSideButton[] buttons) {
            
            lock (lockParameters) {

                // check if a parameter with that name already exists, return without adding if this is the case
                if (getParameter(name) != null) {
                    logger.Warn("A parameter with the name '" + name + "' already exists in parameter set '" + paramSetName + "', not added.");
                    return null;
                }
                
                // retrieve the type of parameter
                Type paramType = typeof(T);

                // only allow for side buttons on string types
                if (buttons != null && buttons.Length != 0) {
                    if (!(paramType == typeof(ParamString) || paramType == typeof(string) || paramType == typeof(String))) {
                        logger.Warn("Discarding extra buttons for parameter '" + name + "' in parameter set '" + paramSetName + "', extra buttons are only allowed for parameters of the String-type");
                        buttons = null;
                    } else if (paramType == typeof(ParamFileString)) {
                        logger.Warn("Discarding extra buttons for parameter '" + name + "' in parameter set '" + paramSetName + "', only a browse button will be added for a parameter of the type FileString");
                        buttons = null;
                    }
                }

                // create a new parameter and transfer the properties
                iParam param = null;
                if (paramType == typeof(ParamBool) || paramType == typeof(bool) || paramType == typeof(Boolean)) {

                    param = new ParamBool(name, group, this, desc, stdValue, options);

                } else if (paramType == typeof(ParamInt) || paramType == typeof(int)) {

                    param = new ParamInt(name, group, this, desc, stdValue, options);

                } else if (paramType == typeof(ParamDouble) || paramType == typeof(double)) {

                    param = new ParamDouble(name, group, this, desc, stdValue, options);

                } else if (paramType == typeof(ParamBoolArr) || paramType == typeof(bool[]) || paramType == typeof(Boolean[])) {

                    param = new ParamBoolArr(name, group, this, desc, stdValue, options);

                } else if (paramType == typeof(ParamIntArr) || paramType == typeof(int[])) {

                    param = new ParamIntArr(name, group, this, desc, stdValue, options);

                } else if (paramType == typeof(ParamDoubleArr) || paramType == typeof(double[])) {

                    param = new ParamDoubleArr(name, group, this, desc, stdValue, options);

                } else if (paramType == typeof(ParamBoolMat) || paramType == typeof(bool[][]) || paramType == typeof(Boolean[][])) {

                    param = new ParamBoolMat(name, group, this, desc, stdValue, options);

                } else if (paramType == typeof(ParamIntMat) || paramType == typeof(int[][])) {

                    param = new ParamIntMat(name, group, this, desc, stdValue, options);

                } else if (paramType == typeof(ParamDoubleMat) || paramType == typeof(double[][])) {

                    param = new ParamDoubleMat(name, group, this, desc, stdValue, options);

                } else if (paramType == typeof(ParamString) || paramType == typeof(string) || paramType == typeof(String)) {

                    param = new ParamString(name, group, this, desc, stdValue, options, buttons);

                } else if (paramType == typeof(ParamFileString)) {

                    param = new ParamFileString(name, group, this, desc, stdValue, options);

                } else if (paramType == typeof(ParamStringMat) || paramType == typeof(string[][]) || paramType == typeof(String[][])) {

                    param = new ParamStringMat(name, group, this, desc, stdValue, options);

                } else if (paramType == typeof(ParamColor) || paramType == typeof(RGBColorFloat)) {

                    param = new ParamColor(name, group, this, desc, stdValue, options);

                } else {

                    // message
                    logger.Error("Unknown parameter (generic) type '" + paramType.Name + "' for parameter name '" + name + "' in parameter set '" + paramSetName + "', not added.");

                    // return without adding parameter
                    return null;
                }

                // check if the parameter is integer based
                if (param is ParamIntBase) {

                    // check if the minimum value is valid
                    if (!((ParamIntBase)param).setMinValue(minValue)) {

                        // message
                        logger.Error("Could not add parameter '" + name + "' in parameter set '" + paramSetName + "', minimum value '" + minValue + "' could not be parsed as an integer");

                        // return without adding parameter
                        return null;

                    }

                    // check if the maximum value is valid
                    if (!((ParamIntBase)param).setMaxValue(maxValue)) {

                        // message
                        logger.Error("Could not add parameter '" + name + "' in parameter set '" + paramSetName + "', maximum value '" + maxValue + "' could not be parsed as an integer");

                        // return without adding parameter
                        return null;

                    }

                }

                // check if the parameter is double based
                if (param is ParamDoubleBase) {

                    // check if the minimum value is valid
                    if (!((ParamDoubleBase)param).setMinValue(minValue)) {

                        // message
                        logger.Error("Could not add parameter '" + name + "' in parameter set '" + paramSetName + "', minimum value '" + minValue + "' could not be parsed as an integer");

                        // return without adding parameter
                        return null;

                    }

                    // check if the maximum value is valid
                    if (!((ParamDoubleBase)param).setMaxValue(maxValue)) {

                        // message
                        logger.Error("Could not add parameter '" + name + "' in parameter set '" + paramSetName + "', maximum value '" + maxValue + "' could not be parsed as an integer");

                        // return without adding parameter
                        return null;

                    }

                }

                // check if there is a standard value given
                if (!String.IsNullOrEmpty(stdValue)) {

                    // set the standard value as initial value
                    if (!param.setValue(param.StdValue)) {

                        // message
                        logger.Error("Standard value for parameter '" + name + "' in parameter set '" + paramSetName + "' is invalid, parameter not added");

                        // return without adding parameter
                        return null;

                    }

                }

                // add the parameter to the list
                paramList.Add(param);
                return param;

            }

        }
        public iParam addSeperator(string text) {
            return addSeperator(text, "");
        }
        public iParam addSeperator(string text, string group) {
            
            // create a seperator parameter
            iParam param = new ParamSeperator(text, group, this);
            
            // add the seperator to the list
            paramList.Add(param);
            return param;

        }
        
        private iParam getParameter(string paramName) {

            // try to find the parameter by name
            for (int i = 0; i < paramList.Count; i++) {
                if (paramList[i].GetType() != typeof(ParamSeperator) && paramList[i].Name.Equals(paramName))
                    return paramList[i];
            }

            // return
            return null;

        }

        public T getValue<T>(string paramName) {

            lock (lockParameters) {

                iParam param = getParameter(paramName);
                if (param == null) {

                    // message
                    logger.Error("Could not find parameter '" + paramName + "' in parameter set '" + paramSetName + "', returning 0");

                    // return 0
                    return (T)Convert.ChangeType(Parameters.emptyValue<T>(), typeof(T));

                }

                // return the value
                return param.getValue<T>();

            }

        }

        public T getValueInSamples<T>(string paramName) {

            lock (lockParameters) {

                iParam param = getParameter(paramName);
                if (param == null) {

                    // message
                    logger.Error("Could not find parameter '" + paramName + "' in parameter set '" + paramSetName + "', returning 0");

                    // return 0
                    return (T)Convert.ChangeType(Parameters.emptyValue<T>(), typeof(T));

                }

                // return the value
                return param.getValueInSamples<T>();

            }

        }

        public T getValueInSamples<T>(string paramName, int[] ignoreColumns) {

            lock (lockParameters) {    
            
                // try to retrieve the parameter
                iParam param = getParameter(paramName);
                if (param == null) {

                    // message and return 0
                    logger.Error("Could not find parameter '" + paramName + "' in parameter set '" + paramSetName + "', returning 0");
                    return (T)Convert.ChangeType(Parameters.emptyValue<T>(), typeof(T));

                }
                    
                // if int[][] or double[][] matrix then get values in samples while ignoring columns
                if (param.GetType() == typeof(ParamDoubleMat) || param.GetType() == typeof(double[][])) {
                    return ((ParamDoubleMat)param).getValueInSamples<T>(ignoreColumns);

                } else if (param.GetType() == typeof(ParamIntMat) || param.GetType() == typeof(int[][])) {
                    return ((ParamIntMat)param).getValueInSamples<T>(ignoreColumns);

                } else {

                    // message and return 0
                    logger.Error("Could not retrieve the values (in samples) for parameter '" + paramName + "' (parameter set: '" + paramSetName + "'), the getValueInSamples functionality with ignoreColumns can only be used for matrix parameters (int[][] or double[][]). Returning 0");
                    return (T)Convert.ChangeType(Parameters.emptyValue<T>(), typeof(T));

                }
                
            }

        }

        public int getValueInSamples(string paramName) {
            return getValueInSamples<int>(paramName);
        }

        public int[] getValuesInSamples(string paramName) {
            return getValueInSamples<int[]>(paramName);
        }

        public string getType(string paramName) {

            lock (lockParameters) {

                iParam param = getParameter(paramName);
                if (param == null) {

                    // message
                    logger.Error("Could not find parameter '" + paramName + "' in parameter set '" + paramSetName + "', returning 0");

                    // return ""
                    return "";

                }

                // return the value
                return param.GetType().ToString();

            }

        }

        public T getUnit<T>(string paramName) {

            lock (lockParameters) {

                iParam param = getParameter(paramName);
                if (param == null) {

                    // message
                    logger.Error("Could not find parameter '" + paramName + "' in parameter set '" + paramSetName + "', returning 0");

                    // return 0
                    return (T)Convert.ChangeType(null, typeof(T));

                }

                // return the value
                return param.getUnit<T>();

            }

        }
        
        
        public string ToString(string paramName) {

            lock (lockParameters) {

                iParam param = getParameter(paramName);
                if (param == null) {
                    logger.Error("Could not find parameter '" + paramName + "' in parameter set '" + paramSetName + "', returning null");
                    return null;
                }

                return param.ToString();
            
            }

        }


        public bool setValue(string paramName, bool paramValue) {

            lock (lockParameters) {

                iParam param = getParameter(paramName);
                if (param == null) {
                    logger.Error("Could not find parameter '" + paramName + "' in parameter set '" + paramSetName + "', value not set");
                    return false;
                }

                // check if the parameter is indeed used to store a boolean
                if (param.GetType() != typeof(ParamBool)) {
                    logger.Error("Could not set parameter '" + paramName + "' in parameter set '" + paramSetName + "', trying to set a boolean value in a '" + param.GetType().Name + "' parameter");
                    return false;
                }

                // set the value
                if (!((ParamBool)param).setValue(paramValue)) return false;

                // return success
                return true;
            
            }

        }

        public bool setValue(string paramName, int paramValue) {
            return setValue(paramName, paramValue, Units.ValueOrSamples);
        }
        public bool setValue(string paramName, int paramValue, Parameters.Units paramUnit) {

            lock (lockParameters) {

                iParam param = getParameter(paramName);
                if (param == null) {
                    logger.Error("Could not find parameter '" + paramName + "' in parameter set '" + paramSetName + "', value not set");
                    return false;
                }

                // check if the parameter is indeed used to store an integer
                if (param.GetType() != typeof(ParamInt)) {
                    logger.Error("Could not set parameter '" + paramName + "' in parameter set '" + paramSetName + "', trying to set an integer value in a '" + param.GetType().Name + "' parameter");
                    return false;
                }

                // set the value
                if (!((ParamInt)param).setValue(paramValue))    return false;
                if (!((ParamInt)param).setUnit(paramUnit))      return false;

                // return success
                return true;
            
            }

        }

        public bool setValue(string paramName, double paramValue) {
            return setValue(paramName, paramValue, Units.ValueOrSamples);
        }
        public bool setValue(string paramName, double paramValue, Parameters.Units paramUnit) {

            lock (lockParameters) {

                iParam param = getParameter(paramName);
                if (param == null) {
                    logger.Error("Could not find parameter '" + paramName + "' in parameter set '" + paramSetName + "', value not set");
                    return false;
                }

                // check if the parameter is indeed used to store a double
                if (param.GetType() != typeof(ParamDouble)) {
                    logger.Error("Could not set parameter '" + paramName + "' in parameter set '" + paramSetName + "', trying to set a double value in a '" + param.GetType().Name + "' parameter");
                    return false;
                }

                // set the value
                if (!((ParamDouble)param).setValue(paramValue)) return false;
                if (!((ParamDouble)param).setUnit(paramUnit))   return false;

                // return success
                return true;
            
            }

        }

        public bool setValue(string paramName, bool[] paramValue) {

            lock (lockParameters) {

                iParam param = getParameter(paramName);
                if (param == null) {
                    logger.Error("Could not find parameter '" + paramName + "' in parameter set '" + paramSetName + "', value not set");
                    return false;
                }

                // check if the parameter is indeed used to store a boolean array
                if (param.GetType() != typeof(ParamBoolArr)) {
                    logger.Error("Could not set parameter '" + paramName + "' in parameter set '" + paramSetName + "', trying to set an array of booleans in a '" + param.GetType().Name + "' parameter");
                    return false;
                }

                // set the value
                if (!((ParamBoolArr)param).setValue(paramValue)) return false;

                // return success
                return true;
            
            }

        }

        public bool setValue(string paramName, int[] paramValue) {
            return setValue(paramName, paramValue, new Parameters.Units[paramValue.Length]);
        }
        public bool setValue(string paramName, int[] paramValue, Parameters.Units[] paramUnit) {

            lock (lockParameters) {

                iParam param = getParameter(paramName);
                if (param == null) {
                    logger.Error("Could not find parameter '" + paramName + "' in parameter set '" + paramSetName + "', value not set");
                    return false;
                }

                // check if the parameter is indeed used to store an integer array
                if (param.GetType() != typeof(ParamIntArr)) {
                    logger.Error("Could not set parameter '" + paramName + "' in parameter set '" + paramSetName + "', trying to set an array of integers in a '" + param.GetType().Name + "' parameter");
                    return false;
                }

                // set the value
                if (!((ParamIntArr)param).setValue(paramValue)) return false;
                if (!((ParamIntArr)param).setUnit(paramUnit))   return false;

                // return success
                return true;

            }
        }

        public bool setValue(string paramName, double[] paramValue) {
            return setValue(paramName, paramValue, new Parameters.Units[paramValue.Length]);
        }
        public bool setValue(string paramName, double[] paramValue, Parameters.Units[] paramUnit) {

            lock (lockParameters) {

                iParam param = getParameter(paramName);
                if (param == null) {
                    logger.Error("Could not find parameter '" + paramName + "' in parameter set '" + paramSetName + "', value not set");
                    return false;
                }

                // check if the parameter is indeed used to store a double array
                if (param.GetType() != typeof(ParamDoubleArr)) {
                    logger.Error("Could not set parameter '" + paramName + "' in parameter set '" + paramSetName + "', trying to set an array of doubles in a '" + param.GetType().Name + "' parameter");
                    return false;
                }

                // set the value
                if (!((ParamDoubleArr)param).setValue(paramValue))  return false;
                if (!((ParamDoubleArr)param).setUnit(paramUnit))    return false;

                // return success
                return true;
            
            }

        }

        public bool setValue(string paramName, bool[][] paramValue) {

            lock (lockParameters) {

                iParam param = getParameter(paramName);
                if (param == null) {
                    logger.Error("Could not find parameter '" + paramName + "' in parameter set '" + paramSetName + "', value not set");
                    return false;
                }

                // check if the parameter is indeed used to store a boolean matix
                if (param.GetType() != typeof(ParamBoolMat)) {
                    logger.Error("Could not set parameter '" + paramName + "' in parameter set '" + paramSetName + "', trying to set a matrix of doubles in a '" + param.GetType().Name + "' parameter");
                    return false;
                }

                // set the value
                if (!((ParamBoolMat)param).setValue(paramValue)) return false;

                // return success
                return true;
            
            }

        }

        public bool setValue(string paramName, int[][] paramValue) {
            Parameters.Units[][] paramUnit = new Parameters.Units[paramValue.Length][];
            for (int i = 0; i < paramUnit.Length; i++) paramUnit[i] = new Parameters.Units[paramValue[i].Length];
            return setValue(paramName, paramValue, paramUnit);
        }
        public bool setValue(string paramName, int[][] paramValue, Parameters.Units[][] paramUnit) {

            lock (lockParameters) {

                iParam param = getParameter(paramName);
                if (param == null) {
                    logger.Error("Could not find parameter '" + paramName + "' in parameter set '" + paramSetName + "', value not set");
                    return false;
                }

                // check if the parameter is indeed used to store a integer matix
                if (param.GetType() != typeof(ParamIntMat)) {
                    logger.Error("Could not set parameter '" + paramName + "' in parameter set '" + paramSetName + "', trying to set a matrix of integers in a '" + param.GetType().Name + "' parameter");
                    return false;
                }

                // set the value
                if (!((ParamIntMat)param).setValue(paramValue)) return false;
                if (!((ParamIntMat)param).setUnit(paramUnit))   return false;

                // return success
                return true;
            
            }

        }

        public bool setValue(string paramName, double[][] paramValue) {
            Parameters.Units[][] paramUnit = new Parameters.Units[paramValue.Length][];
            for (int i = 0; i < paramUnit.Length; i++) paramUnit[i] = new Parameters.Units[paramValue[i].Length];
            return setValue(paramName, paramValue, paramUnit);
        }
        public bool setValue(string paramName, double[][] paramValue, Parameters.Units[][] paramUnit) {

            lock (lockParameters) {

                iParam param = getParameter(paramName);
                if (param == null) {
                    logger.Error("Could not find parameter '" + paramName + "' in parameter set '" + paramSetName + "', value not set");
                    return false;
                }

                // check if the parameter is indeed used to store a double matix
                if (param.GetType() != typeof(ParamDoubleMat)) {
                    logger.Error("Could not set parameter '" + paramName + "' in parameter set '" + paramSetName + "', trying to set a matrix of doubles in a '" + param.GetType().Name + "' parameter");
                    return false;
                }

                // set the value
                if (!((ParamDoubleMat)param).setValue(paramValue)) return false;
                if (!((ParamDoubleMat)param).setUnit(paramUnit)) return false;

                // return success
                return true;
            
            }

        }

        public bool setValue(string paramName, string[][] paramValue) {

            lock (lockParameters) {

                iParam param = getParameter(paramName);
                if (param == null) {
                    logger.Error("Could not find parameter '" + paramName + "' in parameter set '" + paramSetName + "', value not set");
                    return false;
                }

                // check if the parameter is indeed used to store a string matix
                if (param.GetType() != typeof(ParamStringMat)) {
                    logger.Error("Could not set parameter '" + paramName + "' in parameter set '" + paramSetName + "', trying to set a matrix of strings in a '" + param.GetType().Name + "' parameter");
                    return false;
                }

                // set the value
                if (!((ParamStringMat)param).setValue(paramValue)) return false;

                // return success
                return true;
            
            }

        }


        public bool setValue(string paramName, string paramValue) {

            lock (lockParameters) {

                iParam param = getParameter(paramName);
                if (param == null) {
                    logger.Error("Could not find parameter '" + paramName + "' in parameter set '" + paramSetName + "', value not set");
                    return false;
                }

                // call setter of the parameter for further processing
                if (!param.setValue(paramValue)) return false;

                // return success
                return true;

            }

        }
        
        public static T emptyValue<T>() {
            Type paramType = typeof(T);
            if (paramType == typeof(ParamBool) || paramType == typeof(bool) || paramType == typeof(Boolean)) {
                return (T)Convert.ChangeType(false, typeof(T));
            } else if (paramType == typeof(ParamInt) || paramType == typeof(int) || paramType == typeof(ParamDouble) || paramType == typeof(double)) {
                return (T)Convert.ChangeType(0, typeof(T));
            } else if (paramType == typeof(ParamBoolArr) || paramType == typeof(bool[]) || paramType == typeof(Boolean[])) {
                return (T)Convert.ChangeType(new bool[0], typeof(T));
            } else if (paramType == typeof(ParamIntArr) || paramType == typeof(int[])) {
                return (T)Convert.ChangeType(new int[0], typeof(T));
            } else if (paramType == typeof(ParamDoubleArr) || paramType == typeof(double[])) {
                return (T)Convert.ChangeType(new double[0], typeof(T));
            } else if (paramType == typeof(ParamBoolMat) || paramType == typeof(bool[][]) || paramType == typeof(Boolean[][])) {
                return (T)Convert.ChangeType(new bool[0][], typeof(T));
            } else if (paramType == typeof(ParamIntMat) || paramType == typeof(int[][])) {
                return (T)Convert.ChangeType(new int[0][], typeof(T));
            } else if (paramType == typeof(ParamDoubleMat) || paramType == typeof(double[][])) {
                return (T)Convert.ChangeType(new double[0][], typeof(T));
            } else if (paramType == typeof(ParamStringMat) || paramType == typeof(string[][]) || paramType == typeof(String[][])) {
                return (T)Convert.ChangeType(new string[0][], typeof(T));
            } else if (paramType == typeof(ParamColor) || paramType == typeof(RGBColorFloat)) {
                return (T)Convert.ChangeType(null, typeof(T));
            } else {
                return (T)Convert.ChangeType(null, typeof(T));
            }
        }

        public Parameters clone() {

            lock (lockParameters) {

                Parameters clone = new Parameters(this.paramSetName, this.paramSetType);

                // get a reference to the clone's parameter list
                List<iParam> cloneParamList = clone.getParameters();

                // clone every parameter from the parameter list of this instance to
                // and add these to the clone instance's parameter list
                for (int i = 0; i < paramList.Count; i++) {
                    cloneParamList.Add(paramList[i].clone());
                }

                // return the clone
                return clone;

            }

        }
        
        public Parameters clone(string[] paramListToClone) {

            // if empty list is provided, clone all parameters
            if (paramListToClone.Length == 0) {

                logger.Warn("Provided empty list of parameters for cloning. All parameters will be cloned.");
                return clone();

            } else {

                lock (lockParameters) {

                    // create new parameter object
                    Parameters clone = new Parameters(this.paramSetName, this.paramSetType);

                    // get a reference to the clone's parameter list
                    List<iParam> cloneParamList = clone.getParameters();

                    // add desired parameters
                    for (int i = 0; i < paramListToClone.Length; i++) {
                        iParam result = getParameter(paramListToClone[i]);

                        if (result == null)
                            logger.Error("Could not clone parameter " + paramListToClone[i] + " as it is not present in parameterset " + this.paramSetName);
                        else
                            cloneParamList.Add(result);

                    }

                    // return the clone
                    return clone;

                }

            }

        }   // end function

    }

}
