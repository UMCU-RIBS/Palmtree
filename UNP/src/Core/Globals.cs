using NLog;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UNP.Core.Helpers;
using UNP.Core.Params;

namespace UNP.Core {

    public static class Globals {

        private static Logger logger = LogManager.GetLogger("Globals");
        private static List<iParam> paramList = new List<iParam>(0);
        
        private static iParam getParameter(string paramName) {

            // try to find the parameter by name
            for (int i = 0; i < paramList.Count(); i++) {
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
                param = new ParamBool(paramName, "", null, "", new string[0]);
            } else if (paramType == typeof(ParamInt) || paramType == typeof(int)) {
                param = new ParamInt(paramName, "", null, "", new string[0]);
            } else if (paramType == typeof(ParamDouble) || paramType == typeof(double)) {
                param = new ParamDouble(paramName, "", null, "", new string[0]);
            } else if (paramType == typeof(ParamBoolArr) || paramType == typeof(bool[]) || paramType == typeof(Boolean[])) {
                param = new ParamBoolArr(paramName, "", null, "", new string[0]);
            } else if (paramType == typeof(ParamIntArr) || paramType == typeof(int[])) {
                param = new ParamIntArr(paramName, "", null, "", new string[0]);
            } else if (paramType == typeof(ParamDoubleArr) || paramType == typeof(double[])) {
                param = new ParamDoubleArr(paramName, "", null, "", new string[0]);
            } else if (paramType == typeof(ParamBoolMat) || paramType == typeof(bool[][]) || paramType == typeof(Boolean[][])) {
                param = new ParamBoolMat(paramName, "", null, "", new string[0]);
            } else if (paramType == typeof(ParamIntMat) || paramType == typeof(int[][])) {
                param = new ParamIntMat(paramName, "", null, "", new string[0]);
            } else if (paramType == typeof(ParamDoubleMat) || paramType == typeof(double[][])) {
                param = new ParamDoubleMat(paramName, "", null, "", new string[0]);
            } else if (paramType == typeof(ParamStringMat) || paramType == typeof(string[][]) || paramType == typeof(String[][])) {
                param = new ParamStringMat(paramName, "", null, "", new string[0]);
            } else if (paramType == typeof(ParamColor) || paramType == typeof(RGBColorFloat)) {
                param = new ParamColor(paramName, "", null, "", new string[0]);
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
            
            // retrieve the parameter
            iParam param = getParameter(paramName);

            // create the parameter if it does not exist
            if (param == null)  param = addParameter<T>(paramName);

            // if not able to create, return 0
            if (param == null)  return (T)Convert.ChangeType(null, typeof(T));

            // return the value
            return param.getValue<T>();

        }


        public static void setValue<T>(string paramName, string paramValue) {

            // retrieve the parameter
            iParam param = getParameter(paramName);

            // create the parameter if it does not exist
            if (param == null) param = addParameter<T>(paramName);

            // if not able to create, return 0
            if (param == null) return;

            // call setter of the parameter for further processing
            if (!param.setValue(paramValue)) {
                logger.Error("Could not find parameter '" + paramName + "' in globals, discarded");
            }

        }


    }

}
