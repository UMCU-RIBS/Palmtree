/**
 * The Globals class
 * 
 * ...
 * Note: static class over singleton pattern because we do not need an instance using an interface or be passed around
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
using Expressive;
using NLog;
using System;
using System.Collections.Generic;
using UNP.Core.Helpers;
using UNP.Core.Params;

namespace UNP.Core {

    /// <summary>
    /// The <c>Globals</c> class.
    /// 
    /// ...
    /// </summary>
    public static class Globals {

        private static Logger logger = LogManager.GetLogger("Globals");
        private static List<iParam> paramList = new List<iParam>(0);
        private static Object lockParameters = new Object();                                                    // threadsafety lock for all event on the parameters
        private static Dictionary<string, object> evaluableVariables = new Dictionary<string, object>();        // dictionary for evaluatable variables

        private static iParam getParameter(string paramName) {

            // try to find the parameter by name
            for (int i = 0; i < paramList.Count; i++) {
                if (paramList[i].Name.Equals(paramName)) {
                    return paramList[i];
                }
            }

            // return
            return null;

        }


        private static iParam addParameter<T>(string paramName) {

            // create a new parameter and transfer the properties
            iParam param = null;
            Type paramType = typeof(T);
            if (paramType == typeof(ParamBool) || paramType == typeof(bool) || paramType == typeof(Boolean)) {
                param = new ParamBool(paramName, "", null, "", "", new string[0]);
            } else if (paramType == typeof(ParamInt) || paramType == typeof(int)) {
                param = new ParamInt(paramName, "", null, "", "", new string[0]);
            } else if (paramType == typeof(ParamDouble) || paramType == typeof(double)) {
                param = new ParamDouble(paramName, "", null, "", "", new string[0]);
            } else if (paramType == typeof(ParamBoolArr) || paramType == typeof(bool[]) || paramType == typeof(Boolean[])) {
                param = new ParamBoolArr(paramName, "", null, "", "", new string[0]);
            } else if (paramType == typeof(ParamIntArr) || paramType == typeof(int[])) {
                param = new ParamIntArr(paramName, "", null, "", "", new string[0]);
            } else if (paramType == typeof(ParamDoubleArr) || paramType == typeof(double[])) {
                param = new ParamDoubleArr(paramName, "", null, "", "", new string[0]);
            } else if (paramType == typeof(ParamBoolMat) || paramType == typeof(bool[][]) || paramType == typeof(Boolean[][])) {
                param = new ParamBoolMat(paramName, "", null, "", "", new string[0]);
            } else if (paramType == typeof(ParamIntMat) || paramType == typeof(int[][])) {
                param = new ParamIntMat(paramName, "", null, "", "", new string[0]);
            } else if (paramType == typeof(ParamDoubleMat) || paramType == typeof(double[][])) {
                param = new ParamDoubleMat(paramName, "", null, "", "", new string[0]);
            } else if (paramType == typeof(ParamStringMat) || paramType == typeof(string[][]) || paramType == typeof(String[][])) {
                param = new ParamStringMat(paramName, "", null, "", "", new string[0]);
            } else if (paramType == typeof(ParamColor) || paramType == typeof(RGBColorFloat)) {
                param = new ParamColor(paramName, "", null, "", "", new string[0]);
            } else {

                // message
                logger.Error("Unknown parameter (generic) type '" + paramType.Name + "' for parameter paramName '" + paramName + "' in globals, discarded.");

                // return without adding parameter
                return null;
            }

            // add the parameter to the list
            paramList.Add(param);
            return param;

        }
        


        public static T getValue<T>(string paramName) {

            lock (lockParameters) {

                // retrieve the parameter
                iParam param = getParameter(paramName);

                // create the parameter if it does not exist
                if (param == null) param = addParameter<T>(paramName);

                // if not able to create, return 0
                if (param == null) return (T)Convert.ChangeType(null, typeof(T));

                // return the value
                return param.getValue<T>();

            }

        }


        public static void setValue<T>(string paramName, string paramValue) {

            lock (lockParameters) {

                // retrieve the parameter
                iParam param = getParameter(paramName);

                // create the parameter if it does not exist
                if (param == null) param = addParameter<T>(paramName);

                // if not able to create, return
                if (param == null) return;

                // call setter of the parameter for further processing
                if (param.setValue(paramValue)) {
                    // successfully set

                    // also store certain types as evaluable variables
                    if (param.GetType() == typeof(ParamBool)) {
                        evaluableVariables[paramName] = ((ParamBool)param).Value ? 1 : 0;
                    } else if (param.GetType() == typeof(ParamInt)) {
                        evaluableVariables[paramName] = ((ParamInt)param).Value;
                    } else if (param.GetType() == typeof(ParamDouble)) {
                        evaluableVariables[paramName] = ((ParamDouble)param).Value;
                    }

                } else {
                // failure to set

                    // message
                    logger.Error("Could not find parameter '" + paramName + "' in globals, discarded");

                }
            
            }

        }


        public static bool testExpression(string expression) {
            if (string.IsNullOrEmpty(expression))   return true;

            try {

                // evaluate the expression
                var expressionObject = new Expression(expression);
                var expressionResult = expressionObject.Evaluate(evaluableVariables);

                // return success, the expression is valid
                return true;

            } catch (Exception e) {

                // check if the error is not because a variable is not found
                if (e.Message.Contains("has not been supplied")) {
                    
                    // return succes, the expression is valid, just the variables are not set
                    return true;

                } else {

                    // return failure, the experession is incorrect
                    return false;
                }

            }
        }

        public static bool evaluateConditionExpression(string expression) {
            if (string.IsNullOrEmpty(expression)) return false;

            try {

                // evaluate the expression
                var expressionObject = new Expression(expression);
                var expressionResult = expressionObject.Evaluate(evaluableVariables);

                // try to interpret the result as an boolean
                bool result = false;
                if (!bool.TryParse(expressionResult.ToString(), out result)) {

                    // message
                    logger.Error("Error while converting the output of the expression '" + expression + "' to boolean, returning false");

                    // return failure
                    return false;

                }

                // return the boolean result
                return result;

            } catch (Exception e) {

                // check if the error is not because a variable is not found
                if (!e.Message.Contains("has not been supplied")) {

                    // message
                    logger.Error("Error while evaluating expression '" + expression + "', returning false");

                }

            }

            // return failure
            return false;

        }

        public static double evaluateExpression(string expression) {

            try {

                // evaluate the expression
                var expressionObject = new Expression(expression);
                var expressionResult = expressionObject.Evaluate(evaluableVariables);

                // try to interpret the result as a double
                double result = -1;
                if (!double.TryParse(expressionResult.ToString(), out result)) {

                    // message
                    logger.Error("Error while converting the output of the expression '" + expression + "' to double, returning -1");

                    // return failure
                    return -1;

                }

                // return the double result
                return result;
                
            } catch (Exception e) {

                // check if the error is not because a variable is not found
                if (!e.Message.Contains("has not been supplied")) {

                    // message
                    logger.Error("Error while evaluating expression '" + expression + "', returning -1");

                }

            }

            // return failure
            return -1;

        }


    }

}
