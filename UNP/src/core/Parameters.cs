using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UNP.helpers;

namespace UNP {

    public static class ParameterManager {

        private static Dictionary<String, Parameters> paramSets = new Dictionary<String, Parameters>(0);

        public static Parameters GetParameters(String name) {
            Parameters parameterSet = null;

            // try to retrieve the set
            if (!paramSets.TryGetValue(name, out parameterSet)) {
                // set not found

                // create the set and add it to the dictionary
                parameterSet = new Parameters(name);
                paramSets.Add(name, parameterSet);
            }

            // return the existing (or newly created) parameter set
            return parameterSet;

        }

        public static Dictionary<String, Parameters> getParameterSets() {
            return paramSets;
        }

    }

    public class Parameters {
        
        private static Logger logger = LogManager.GetLogger("Parameters");

        private List<iParam> paramList = new List<iParam>(0);
        private String selfName = "";

        public Parameters(String name) {
            selfName = name;
        }

        public List<iParam> getParameters() {
            return paramList;
        }

        //public void addParameter(String name, String desc, Param.Types type, String minValue, String maxValue, String stdValue) {
        public iParam addParameter<T>(String name, String desc, String minValue, String maxValue, String stdValue) {
            return addParameter<T>(name, "", desc, minValue, maxValue, stdValue, new String[0]);
        }
        //public void addParameter(String name, String desc, Param.Types type, String minValue, String maxValue, String stdValue, String[] options) {
        public iParam addParameter<T>(String name, String desc, String minValue, String maxValue, String stdValue, String[] options) {
            return addParameter<T>(name, "", desc, minValue, maxValue, stdValue, options);
        }
        //public void addParameter(String name, String group, String desc, Param.Types type, String minValue, String maxValue, String stdValue) {
        public iParam addParameter<T>(String name, String group, String desc, String minValue, String maxValue, String stdValue) {
            return addParameter<T>(name, group, desc, minValue, maxValue, stdValue, new String[0]);
        }
        //public void addParameter(String name, String group, String desc, Param.Types type, String minValue, String maxValue, String stdValue, String[] options) {
        public iParam addParameter<T>(String name, String group, String desc, String minValue, String maxValue, String stdValue, String[] options) {

            // check if a parameter with that name already exists, return without adding if this is the case
            if (getParameter(name) != null) {
                logger.Warn("A parameter with the name '" + name + "' already exists in parameter set '" + selfName + "', not added.");
                return null;
            }

            // TODO: check input
            /*
            param.MinValue = Regex.Replace(minValue.ToLower(), @"\s+", " ");
            param.MaxValue = Regex.Replace(maxValue.ToLower(), @"\s+", " ");
            param.Value = Regex.Replace(value.ToLower(), @"\s+", " ");
            */

            // create a new parameter and transfer the properties
            iParam param = null;
            Type paramType = typeof(T);
            if(paramType == typeof(ParamBool) || paramType == typeof(bool) || paramType == typeof(Boolean)) {
                
                param = new ParamBool(name, group, desc, minValue, maxValue, stdValue, options);

            } else if (paramType == typeof(ParamInt) || paramType == typeof(int)) {

                param = new ParamInt(name, group, desc, minValue, maxValue, stdValue, options);

            } else if (paramType == typeof(ParamDouble) || paramType == typeof(double)) {

                param = new ParamDouble(name, group, desc, minValue, maxValue, stdValue, options);

            } else if (paramType == typeof(ParamBoolArr) || paramType == typeof(bool[]) || paramType == typeof(Boolean[])) {

                param = new ParamBoolArr(name, group, desc, minValue, maxValue, stdValue, options);
                
            } else if (paramType == typeof(ParamIntArr) || paramType == typeof(int[])) {

                param = new ParamIntArr(name, group, desc, minValue, maxValue, stdValue, options);

            } else if (paramType == typeof(ParamDoubleArr) || paramType == typeof(double[])) {

                param = new ParamDoubleArr(name, group, desc, minValue, maxValue, stdValue, options);

            } else if (paramType == typeof(ParamBoolMat) || paramType == typeof(bool[][]) || paramType == typeof(Boolean[][])) {

                param = new ParamBoolMat(name, group, desc, minValue, maxValue, stdValue, options);
                
            } else if (paramType == typeof(ParamIntMat) || paramType == typeof(int[][])) {

                param = new ParamIntMat(name, group, desc, minValue, maxValue, stdValue, options);

            } else if (paramType == typeof(ParamDoubleMat) || paramType == typeof(double[][])) {

                param = new ParamDoubleMat(name, group, desc, minValue, maxValue, stdValue, options);

            } else if (paramType == typeof(ParamColor) || paramType == typeof(RGBColorFloat)) {

                param = new ParamColor(name, group, desc, minValue, maxValue, stdValue, options);
                                       
            } else {
                
                // message
                logger.Error("Unknown parameter (generic) type '" + paramType.Name + "' for parameter name '" + name + "' in parameter set '" + selfName + "', not added.");

                // return without adding parameter
                return null;
            }

            // add the parameter to the list
            paramList.Add(param);
            return param;


        }

        private iParam getParameter(String paramName) {

            // try to find the parameter by name
            for (int i = 0; i < paramList.Count(); i++) {
                if (paramList[i].Name.Equals(paramName)) {
                    return paramList[i];
                }
            }

            // return
            return null;

        }

        public T getValue<T>(String paramName) {
            iParam param = getParameter(paramName);
            if (param == null) {

                // message
                logger.Error("Could not find parameter '" + paramName + "' in parameter set '" + selfName + "', returning 0");

                // return 0
                return (T)Convert.ChangeType(0, typeof(T));

            }
            
            // return the value
            return param.getValue<T>();

        }

        public void setValue(String paramName, bool paramValue) {
            iParam param = getParameter(paramName);
            if (param == null) {
                logger.Error("Could not find parameter '" + paramName + "' in parameter set '" + selfName + "', value not set");
                return;
            }

            // check if the parameter is indeed used to store a boolean
            if (param.GetType() != typeof(ParamBool)) {
                logger.Error("Could not set parameter '" + paramName + "' in parameter set '" + selfName + "', trying to set a boolean value in a '" + param.GetType().Name + "' parameter");
                return;
            }

            // set the value
            ((ParamBool)param).Value = paramValue;

        }

        public void setValue(String paramName, int paramValue) {
            iParam param = getParameter(paramName);
            if (param == null) {
                logger.Error("Could not find parameter '" + paramName + "' in parameter set '" + selfName + "', value not set");
                return;
            }

            // check if the parameter is indeed used to store a boolean
            if (param.GetType() != typeof(ParamInt)) {
                logger.Error("Could not set parameter '" + paramName + "' in parameter set '" + selfName + "', trying to set a integer value in a '" + param.GetType().Name + "' parameter");
                return;
            }

            // set the value
            ((ParamInt)param).Value = paramValue;

        }
        /*
        public void setValue(String paramName, String paramValue) {
            iParam param = getParameter(paramName);
            if (param == null) {
                
                // message
                logger.Error("Could not find parameter '" + paramName + "' in parameter set '" + selfName + "', value not set");
                return;

            }


            //paramValue.paramValue = Regex.Replace(paramValue.ToLower(), @"\s+", " ");
            // TODO: check value

            //logger.Warn("- " + paramName + " = " + paramValue);
            //param.Value = paramValue;
        }
        */


    }

    public interface iParam {

        String Name         { get; }
        String Group        { get; }
        String Desc         { get; }
        String MinValue     { get; }
        String MaxValue     { get; }
        String StdValue     { get; }
        String[] Options    { get; }

        T getValue<T>();

    }

    public class Param {

        protected static Logger logger = LogManager.GetLogger("Param");

        // parameter properties
        private String name     = "";
        private String group    = "";
        private String desc     = "";
        private String stdValue = "";
        private String minValue = "";
        private String maxValue = "";
        private String[] options = new String[0];


        public Param(String name, String group, String desc, String minValue, String maxValue, String stdValue, String[] options) {
            this.name = name;
            this.group = group;
            this.desc = desc;
            this.minValue = minValue;
            //set {   this.minValue = Regex.Replace(minValue.ToLower(), @"\s+", " ");  }
            this.maxValue = maxValue;
            //set {   this.maxValue = Regex.Replace(value.ToLower(), @"\s+", " ");  }
            this.stdValue = stdValue;
            //set {   this.stdValue = Regex.Replace(value.ToLower(), @"\s+", " ");   }
            this.options = options;
        }

        public String   Name      {   get {   return this.name;       }   }
        public String   Group     {   get {   return this.group;      }   }
        public String   Desc      {   get {   return this.desc;       }   }
        public String   MinValue  {   get {   return this.minValue;   }   }
        public String   MaxValue  {   get {   return this.maxValue;   }   }
        public String   StdValue  {   get {   return this.stdValue;   }   }
        public String[] Options   {   get {   return this.options;    }   }

    }

    
    public class ParamBool : Param, iParam {

        private bool value = false;

        public ParamBool(String name, String group, String desc, String minValue, String maxValue, String stdValue, String[] options) : base(name, group, desc, minValue, maxValue, stdValue, options) { }
        public T getValue<T>() {

            Type paramType = typeof(T);
            if(paramType == typeof(bool)) {     
                // request to return as bool

                // return value
                return (T)Convert.ChangeType(Value, typeof(bool));

            } else {
                // request to return as other

                // message and return false
                logger.Error("Could not retrieve the value for parameter '" + this.Name + "' as '" + paramType.Name + "', can only return value as boolean. Returning false");
                return (T)Convert.ChangeType(false, typeof(T));    

            }
            
        }

        public bool Value {
            get {   return this.value;  }
            set {   this.value = value; }
        }

    }


    public class ParamInt : Param, iParam {

        private int value = 0;

        public ParamInt(String name, String group, String desc, String minValue, String maxValue, String stdValue, String[] options) : base(name, group, desc, minValue, maxValue, stdValue, options) { }
        public T getValue<T>() {

            Type paramType = typeof(T);
            if(paramType == typeof(int)) {     
                // request to return as int

                // return value
                return (T)Convert.ChangeType(Value, typeof(int));

            } else {
                // request to return as other

                // message and return 0
                logger.Error("Could not retrieve the value for parameter '" + this.Name + "' as '" + paramType.Name + "', can only return value as integer. Returning 0");
                return (T)Convert.ChangeType(0, typeof(T));    

            }
            
        }

        public int Value {
            get {   return this.value;  }
            set {   this.value = value; }
        }

    }

    public class ParamDouble : Param, iParam {

        private double value = 0.0;

        public ParamDouble(String name, String group, String desc, String minValue, String maxValue, String stdValue, String[] options) : base(name, group, desc, minValue, maxValue, stdValue, options) { }
        public T getValue<T>() {

            Type paramType = typeof(T);
            if(paramType == typeof(double)) {     
                // request to return as double

                // return value
                return (T)Convert.ChangeType(Value, typeof(double));

            } else {
                // request to return as other

                // message and return 0
                logger.Error("Could not retrieve the value for parameter '" + this.Name + "' as '" + paramType.Name + "', can only return value as double. Returning 0.0");
                return (T)Convert.ChangeType(0, typeof(T));    

            }
            
        }

        public double Value {
            get {   return this.value;  }
            set {   this.value = value; }
        }

    }


    
    public class ParamBoolArr : Param, iParam {

        private bool[] value = new bool[0];

        public ParamBoolArr(String name, String group, String desc, String minValue, String maxValue, String stdValue, String[] options) : base(name, group, desc, minValue, maxValue, stdValue, options) { }
        public T getValue<T>() {

            Type paramType = typeof(T);
            if(paramType == typeof(bool[])) {     
                // request to return as bool[]

                // return value
                return (T)Convert.ChangeType(Value, typeof(bool[]));

            } else {
                // request to return as other

                // message and return false
                logger.Error("Could not retrieve the value for parameter '" + this.Name + "' as '" + paramType.Name + "', can only return value as an array of booleans (bool[]). Returning empty array");
                return (T)Convert.ChangeType(false, typeof(T));    

            }
            
        }

        public bool[] Value {
            get {   return this.value;  }
            set {   this.value = value; }
        }

    }


    public class ParamIntArr : Param, iParam {

        private int[] value = new int[0];

        public ParamIntArr(String name, String group, String desc, String minValue, String maxValue, String stdValue, String[] options) : base(name, group, desc, minValue, maxValue, stdValue, options) { }
        public T getValue<T>() {

            Type paramType = typeof(T);
            if(paramType == typeof(int[])) {     
                // request to return as int[]

                // return vlaue
                return (T)Convert.ChangeType(Value, typeof(int[]));

            } else {
                // request to return as other

                // message and return 0
                logger.Error("Could not retrieve the value for parameter '" + this.Name + "' as '" + paramType.Name + "', can only return value as an array of integers (int[]). Returning empty array");
                return (T)Convert.ChangeType(0, typeof(T));    

            }
            
        }

        public int[] Value {
            get {   return this.value;  }
            set {   this.value = value; }
        }

    }

    public class ParamDoubleArr : Param, iParam {

        private double[] value = new double[0];

        public ParamDoubleArr(String name, String group, String desc, String minValue, String maxValue, String stdValue, String[] options) : base(name, group, desc, minValue, maxValue, stdValue, options) { }
        public T getValue<T>() {

            Type paramType = typeof(T);
            if(paramType == typeof(double[])) {     
                // request to return as double[]

                // return vlaue
                return (T)Convert.ChangeType(Value, typeof(double[]));

            } else {
                // request to return as other

                // message and return 0
                logger.Error("Could not retrieve the value for parameter '" + this.Name + "' as '" + paramType.Name + "', can only return value as an array of doubles (double[]). Returning empty array");
                return (T)Convert.ChangeType(0, typeof(T));    

            }
            
        }

        public double[] Value {
            get {   return this.value;  }
            set {   this.value = value; }
        }

    }


    
    public class ParamBoolMat : Param, iParam {

        private bool[][] value = new bool[0][];

        public ParamBoolMat(String name, String group, String desc, String minValue, String maxValue, String stdValue, String[] options) : base(name, group, desc, minValue, maxValue, stdValue, options) { }
        public T getValue<T>() {

            Type paramType = typeof(T);
            if(paramType == typeof(bool[][])) {     
                // request to return as bool[][]

                // return value
                return (T)Convert.ChangeType(Value, typeof(bool[][]));

            } else {
                // request to return as other

                // message and return false
                logger.Error("Could not retrieve the value for parameter '" + this.Name + "' as '" + paramType.Name + "', can only return value as a matrix of booleans (bool[][]). Returning empty matrix");
                return (T)Convert.ChangeType(false, typeof(T));    

            }
            
        }

        public bool[][] Value {
            get {   return this.value;  }
            set {   this.value = value; }
        }

    }


    public class ParamIntMat : Param, iParam {

        private int[][] value = new int[0][];

        public ParamIntMat(String name, String group, String desc, String minValue, String maxValue, String stdValue, String[] options) : base(name, group, desc, minValue, maxValue, stdValue, options) { }
        public T getValue<T>() {

            Type paramType = typeof(T);
            if(paramType == typeof(int[][])) {     
                // request to return as int[][]

                // return vlaue
                return (T)Convert.ChangeType(Value, typeof(int[][]));

            } else {
                // request to return as other

                // message and return 0
                logger.Error("Could not retrieve the value for parameter '" + this.Name + "' as '" + paramType.Name + "', can only return value as a matrix of integers (int[][]). Returning empty matrix");
                return (T)Convert.ChangeType(0, typeof(T));    

            }
            
        }

        public int[][] Value {
            get {   return this.value;  }
            set {   this.value = value; }
        }

    }

    public class ParamDoubleMat : Param, iParam {

        private double[][] value = new double[0][];

        public ParamDoubleMat(String name, String group, String desc, String minValue, String maxValue, String stdValue, String[] options) : base(name, group, desc, minValue, maxValue, stdValue, options) { }
        public T getValue<T>() {

            Type paramType = typeof(T);
            if(paramType == typeof(double[][])) {     
                // request to return as double[][]

                // return vlaue
                return (T)Convert.ChangeType(Value, typeof(double[][]));

            } else {
                // request to return as other

                // message and return 0
                logger.Error("Could not retrieve the value for parameter '" + this.Name + "' as '" + paramType.Name + "', can only return value as a matrix of doubles (double[][]). Returning empty matrix");
                return (T)Convert.ChangeType(0, typeof(T));    

            }
            
        }

        public double[][] Value {
            get {   return this.value;  }
            set {   this.value = value; }
        }

    }
    
    public class ParamColor : Param, iParam {

        private RGBColorFloat value = new RGBColorFloat();

        public ParamColor(String name, String group, String desc, String minValue, String maxValue, String stdValue, String[] options) : base(name, group, desc, minValue, maxValue, stdValue, options) { }
        public T getValue<T>() {

            Type paramType = typeof(T);
            if(paramType == typeof(RGBColorFloat)) {
                // request to return as RGBColorFloat

                // return value
                return (T)Convert.ChangeType(Value, typeof(RGBColorFloat));

            } else {
                // request to return as other

                // message and return false
                logger.Error("Could not retrieve the value for parameter '" + this.Name + "' as '" + paramType.Name + "', can only return value as color (RGBColorFloat). Returning null");
                return (T)Convert.ChangeType(null, typeof(T));    

            }
            
        }

        public RGBColorFloat Value {
            get {   return this.value;  }
            set {   this.value = value; }
        }

    }


}
