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

        public static Parameters GetParameters(String name, Parameters.ParamSetTypes type) {
            Parameters parameterSet = null;

            // try to retrieve the set
            if (!paramSets.TryGetValue(name, out parameterSet)) {
                // set not found

                // create the set and add it to the dictionary
                parameterSet = new Parameters(name, type);
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
        private String paramSetName = "";
        private ParamSetTypes paramSetType = ParamSetTypes.Source;

        public enum ParamSetTypes : int {
            Source = 0,
            Filter = 1,
            Application = 2
        }

        public enum Units : int {
            ValueOrSamples = 0,
            Seconds = 1
        }

        public Parameters(String paramSetName, ParamSetTypes paramSetType) {
            this.paramSetName = paramSetName;
            this.paramSetType = paramSetType;
        }

        public String ParamSetName {
            get { return this.paramSetName; }
        }
        
        public ParamSetTypes ParamSetType {
            get { return this.paramSetType; }
        }

        public List<iParam> getParameters() {
            return paramList;
        }

        public iParam addParameter<T>(String name, String desc, String stdValue) {
            return addParameter<T>(name, "", desc, "", "", stdValue, new String[0]);
        }
        public iParam addParameter<T>(String name, String desc, String minValue, String maxValue, String stdValue) {
            return addParameter<T>(name, "", desc, minValue, maxValue, stdValue, new String[0]);
        }
        public iParam addParameter<T>(String name, String desc, String minValue, String maxValue, String stdValue, String[] options) {
            return addParameter<T>(name, "", desc, minValue, maxValue, stdValue, options);
        }
        public iParam addParameter<T>(String name, String group, String desc, String minValue, String maxValue, String stdValue) {
            return addParameter<T>(name, group, desc, minValue, maxValue, stdValue, new String[0]);
        }
        public iParam addParameter<T>(String name, String group, String desc, String minValue, String maxValue, String stdValue, String[] options) {

            // check if a parameter with that name already exists, return without adding if this is the case
            if (getParameter(name) != null) {
                logger.Warn("A parameter with the name '" + name + "' already exists in parameter set '" + paramSetName + "', not added.");
                return null;
            }

            // create a new parameter and transfer the properties
            iParam param = null;
            Type paramType = typeof(T);
            if(paramType == typeof(ParamBool) || paramType == typeof(bool) || paramType == typeof(Boolean)) {
                
                param = new ParamBool(name, group, this, desc, options);

            } else if (paramType == typeof(ParamInt) || paramType == typeof(int)) {

                param = new ParamInt(name, group, this, desc, options);

            } else if (paramType == typeof(ParamDouble) || paramType == typeof(double)) {

                param = new ParamDouble(name, group, this, desc, options);

            } else if (paramType == typeof(ParamBoolArr) || paramType == typeof(bool[]) || paramType == typeof(Boolean[])) {

                param = new ParamBoolArr(name, group, this, desc, options);
                
            } else if (paramType == typeof(ParamIntArr) || paramType == typeof(int[])) {

                param = new ParamIntArr(name, group, this, desc, options);

            } else if (paramType == typeof(ParamDoubleArr) || paramType == typeof(double[])) {

                param = new ParamDoubleArr(name, group, this, desc, options);

            } else if (paramType == typeof(ParamBoolMat) || paramType == typeof(bool[][]) || paramType == typeof(Boolean[][])) {

                param = new ParamBoolMat(name, group, this, desc, options);
                
            } else if (paramType == typeof(ParamIntMat) || paramType == typeof(int[][])) {

                param = new ParamIntMat(name, group, this, desc, options);

            } else if (paramType == typeof(ParamDoubleMat) || paramType == typeof(double[][])) {

                param = new ParamDoubleMat(name, group, this, desc, options);

            } else if (paramType == typeof(ParamColor) || paramType == typeof(RGBColorFloat)) {

                param = new ParamColor(name, group, this, desc, options);
                                       
            } else {
                
                // message
                logger.Error("Unknown parameter (generic) type '" + paramType.Name + "' for parameter name '" + name + "' in parameter set '" + paramSetName + "', not added.");

                // return without adding parameter
                return null;
            }

            // check if the parameter is integer based
            if(param is ParamIntBase) {

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

            // set the standard value
            if (param.setStdValue(stdValue)) {
                // succesfully set standard value

                // For parameter types which hold just one value, the standard
                // value can be set initially to this value

                // check if the parameter is of the type that just holds a single value
                if (param is ParamBool || param is ParamInt || param is ParamDouble || param is ParamColor) {

                    // set the standard value as initial value
                    param.setValue(param.StdValue);

                }

            } else {
                // failed to set standard value

                // message
                logger.Error("Could not add parameter '" + name + "' in parameter set '" + paramSetName + "', standard value '" + stdValue + "' is empty or could not be parsed");

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
                logger.Error("Could not find parameter '" + paramName + "' in parameter set '" + paramSetName + "', returning 0");

                // return 0
                return (T)Convert.ChangeType(0, typeof(T));

            }
            
            // return the value
            return param.getValue<T>();

        }
        
        public int getValueInSamples(String paramName) {
            iParam param = getParameter(paramName);
            if (param == null) {

                // message
                logger.Error("Could not find parameter '" + paramName + "' in parameter set '" + paramSetName + "', returning 0");

                // return 0
                return 0;

            }
            
            // return the value
            return param.getValueInSamples();

        }

        public void setValue(String paramName, bool paramValue) {
            iParam param = getParameter(paramName);
            if (param == null) {
                logger.Error("Could not find parameter '" + paramName + "' in parameter set '" + paramSetName + "', value not set");
                return;
            }

            // check if the parameter is indeed used to store a boolean
            if (param.GetType() != typeof(ParamBool)) {
                logger.Error("Could not set parameter '" + paramName + "' in parameter set '" + paramSetName + "', trying to set a boolean value in a '" + param.GetType().Name + "' parameter");
                return;
            }

            // set the value
            ((ParamBool)param).Value = paramValue;

        }

        public void setValue(String paramName, int paramValue) {
            setValue(paramName, paramValue, Units.ValueOrSamples);
        }
        public void setValue(String paramName, int paramValue, Parameters.Units paramUnit) {
            iParam param = getParameter(paramName);
            if (param == null) {
                logger.Error("Could not find parameter '" + paramName + "' in parameter set '" + paramSetName + "', value not set");
                return;
            }

            // check if the parameter is indeed used to store a boolean
            if (param.GetType() != typeof(ParamInt)) {
                logger.Error("Could not set parameter '" + paramName + "' in parameter set '" + paramSetName + "', trying to set a integer value in a '" + param.GetType().Name + "' parameter");
                return;
            }

            // set the value
            ((ParamInt)param).Value = paramValue;
            ((ParamInt)param).Unit = paramUnit;

        }

        public void setValue(String paramName, double paramValue) {
            setValue(paramName, paramValue, Units.ValueOrSamples);
        }
        public void setValue(String paramName, double paramValue, Parameters.Units paramUnit) {
            iParam param = getParameter(paramName);
            if (param == null) {
                logger.Error("Could not find parameter '" + paramName + "' in parameter set '" + paramSetName + "', value not set");
                return;
            }

            // check if the parameter is indeed used to store a boolean
            if (param.GetType() != typeof(ParamDouble)) {
                logger.Error("Could not set parameter '" + paramName + "' in parameter set '" + paramSetName + "', trying to set a integer value in a '" + param.GetType().Name + "' parameter");
                return;
            }

            // set the value
            ((ParamDouble)param).Value = paramValue;
            ((ParamDouble)param).Unit = paramUnit;

        }

        public void setValue(String paramName, String paramValue) {
            iParam param = getParameter(paramName);
            if (param == null) {
                logger.Error("Could not find parameter '" + paramName + "' in parameter set '" + paramSetName + "', value not set");
                return;
            }

            // call setter of the parameter for further processing
            param.setValue(paramValue);

        }

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
        int getValueInSamples();
        bool setStdValue(String stdValue);
        void setValue(String value);

    }

    public abstract class Param {

        protected static Logger logger = LogManager.GetLogger("Parameter");

        // parameter properties
        private String name     = "";
        private String group    = "";
        private String desc     = "";
        protected String stdValue = "";
        protected String minValue = "";
        protected String maxValue = "";
        private String[] options = new String[0];
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

    public abstract class ParamBoolBase : Param {
        private bool boolStdValue = false;

        public ParamBoolBase(String name, String group, Parameters parentSet, String desc, String[] options) : base(name, group, parentSet, desc, options) { }

        public bool setStdValue(String stdValue) {

            // make lowercase and store the standardvalue
            this.stdValue = stdValue.ToLower();

            // interpret the standard value 
            this.boolStdValue = (this.stdValue.Equals("1") || this.stdValue.Equals("true"));

            // return true
            return true;

        }

    }

    public abstract class ParamIntBase : Param {
        
        private int intStdValue = 0;
        private Parameters.Units unitStdValue = Parameters.Units.ValueOrSamples;
        private int intMinValue = 0;
        private Parameters.Units unitMinValue = Parameters.Units.ValueOrSamples;
        private int intMaxValue = 0;
        private Parameters.Units unitMaxValue = Parameters.Units.ValueOrSamples;

        public ParamIntBase(String name, String group, Parameters parentSet, String desc, String[] options) : base(name, group, parentSet, desc, options) { }

        protected bool tryParseValue(String value, out int intValue, out Parameters.Units unit) {
            intValue = 0;
            unit = Parameters.Units.ValueOrSamples;

            // lower case and trim all whitespace
            value = value.ToLower().TrimAll();
            
            // check if value is in seconds
            if (value.Length > 1 && value.Substring(value.Length - 1).Equals("s")) {
                unit = Parameters.Units.Seconds;
                value = value.Substring(0, value.Length - 1);
            }

            // return false if the value is empty
            if (String.IsNullOrEmpty(value))    return false;

            // check if value is numeric and can be converted to an int
            // return false if unsucessful
            if (!int.TryParse(value, out intValue))     return false;

            // successfull parsing, return true
            return true;

        }

        public bool setMinValue(String minValue) {

            // check if a minimum is set
            if (!String.IsNullOrEmpty(minValue) && !minValue.Equals("%")) {
                // minimum is set

                // parse the minimum
                if (!tryParseValue(minValue, out intMinValue, out unitMinValue)) {
                    
                    // set the minvalue to be unlimited
                    this.minValue = "";

                    // return fail
                    return false;

                }

            }

            // store the minvalue
            this.minValue = minValue;

            // return success
            return true;

        }

        public bool setMaxValue(String maxValue) {

            // check if a maximum is set
            if (!String.IsNullOrEmpty(maxValue) && !maxValue.Equals("%")) {
                // maximum is set

                // parse the maximum
                if (!tryParseValue(minValue, out intMaxValue, out unitMaxValue)) {

                    // set the maxvalue to be unlimited
                    this.maxValue = "";

                    // return fail
                    return false;

                }

            }

            // store the maxvalue
            this.maxValue = maxValue;

            // return success
            return true;

        }


        public bool setStdValue(String stdValue) {

            // parse the standard value
            if (!tryParseValue(stdValue, out intStdValue, out unitStdValue)) {
                    
                // set the stdvalue to be unlimited
                this.stdValue = "";

                // return fail
                return false;

            }
            
            // make lowercase and store the stdvalue
            this.stdValue = stdValue.ToLower();

            // return success
            return true;

        }


        /*
        protected bool checkMinimum(ref int doubleValue, ref Parameters.Units unit) {

            // check if there is no minimum, if no minimum return true
            if (String.IsNullOrEmpty(this.minValue) || this.minValue.Equals("%"))   return true;

            // 
            if (unit == unitMinValue) {

            }

            // check in which unit the value to check is set
            if (unit == Parameters.Units.ValueOrSamples) {
                // ValueOrSamples

                if (unitMinValue == Parameters.Units.ValueOrSamples) {



                } else {

                }


            } else {
                // Seconds



            }


            return true;

        }
        */



    }

    public abstract class ParamDoubleBase : Param {

        private double doubleStdValue = 0;
        private Parameters.Units unitStdValue = Parameters.Units.ValueOrSamples;
        private double doubleMinValue = 0;
        private Parameters.Units unitMinValue = Parameters.Units.ValueOrSamples;
        private double doubleMaxValue = 0;
        private Parameters.Units unitMaxValue = Parameters.Units.ValueOrSamples;

        public ParamDoubleBase(String name, String group, Parameters parentSet, String desc, String[] options) : base(name, group, parentSet, desc, options) { }

        protected bool tryParseValue(String value, out double doubleValue, out Parameters.Units unit) {
            doubleValue = 0.0;
            unit = Parameters.Units.ValueOrSamples;
            
            // lower case and trim all whitespace
            value = value.ToLower().TrimAll();
            
            // check if value is in seconds
            if (value.Length > 1 && value.Substring(value.Length - 1).Equals("s")) {
                unit = Parameters.Units.Seconds;
                value = value.Substring(value.Length - 1);
            }

            // return false if the value is empty
            if (String.IsNullOrEmpty(value))    return false;

            // check if value is numeric and can be converted to a double
            // return false if unsucessful
            if (!double.TryParse(value, out doubleValue))     return false;

            // successfull parsing, return true
            return true;

        }


        public bool setMinValue(String minValue) {

            // check if a minimum is set
            if (!String.IsNullOrEmpty(minValue) && !minValue.Equals("%")) {
                // minimum is set

                // parse the minimum
                if (!tryParseValue(minValue, out doubleMinValue, out unitMinValue)) {
                    
                    // set the minvalue to be unlimited
                    this.minValue = "";

                    // return fail
                    return false;

                }

            }

            // store the minvalue
            this.minValue = minValue;

            // return success
            return true;

        }

        public bool setMaxValue(String maxValue) {

            // check if a maximum is set
            if (!String.IsNullOrEmpty(maxValue) && !maxValue.Equals("%")) {
                // maximum is set

                // parse the maximum
                if (!tryParseValue(minValue, out doubleMaxValue, out unitMaxValue)) {

                    // set the maxvalue to be unlimited
                    this.maxValue = "";

                    // return fail
                    return false;

                }

            }

            // store the maxvalue
            this.maxValue = maxValue;

            // return success
            return true;

        }

        public bool setStdValue(String stdValue) {

            // parse the standard value
            if (!tryParseValue(stdValue, out doubleStdValue, out unitStdValue)) {
                    
                // set the stdvalue to be unlimited
                this.stdValue = "";

                // return fail
                return false;

            }

            // make lowercase and store the stdvalue
            this.stdValue = stdValue.ToLower();

            // return success
            return true;

        }

    }

    public class ParamBool : ParamBoolBase, iParam {

        private bool value = false;

        public ParamBool(String name, String group, Parameters parentSet, String desc, String[] options) : base(name, group, parentSet, desc, options) {
            minValue = "0";
            maxValue = "1";
        }

        public T getValue<T>() {

            Type paramType = typeof(T);
            if(paramType == typeof(bool)) {     
                // request to return as bool

                // return value
                return (T)Convert.ChangeType(Value, typeof(bool));

            } else {
                // request to return as other

                // message and return false
                logger.Error("Could not retrieve the value for parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "') as '" + paramType.Name + "', can only return value as boolean. Returning false");
                return (T)Convert.ChangeType(false, typeof(T));    

            }
            
        }


        public int getValueInSamples() {

            // message
            logger.Error("Trying to retrieve the value for bool parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "') in number of samples, use getValue<T>() instead, returning 0");

            // return 0
            return 0;

        }
        
        public bool Value {
            get {   return this.value;  }
            set {   this.value = value; }
        }

        public void setValue(String value) {
            value = value.ToLower();
            this.value = (value.Equals("1") || value.Equals("true"));
        }

    }


    public class ParamInt : ParamIntBase, iParam {

        private int value = 0;
        private Parameters.Units unit = Parameters.Units.ValueOrSamples;

        public ParamInt(String name, String group, Parameters parentSet, String desc, String[] options) : base(name, group, parentSet, desc, options) { }

        public T getValue<T>() {

            Type paramType = typeof(T);
            if(paramType == typeof(int)) {     
                // request to return as int

                // return value
                return (T)Convert.ChangeType(value, typeof(int));

            } else {
                // request to return as other

                // message and return 0
                logger.Error("Could not retrieve the value for parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "') as '" + paramType.Name + "', can only return value as integer. Returning 0");
                return (T)Convert.ChangeType(0, typeof(T));    

            }
            
        }

        public int getValueInSamples() {

            // retrieve the value as integer
            int val = getValue<int>();
            
            // check if the unit is set in seconds
            if (unit == Parameters.Units.Seconds) {
                // flagged as seconds

                // convert and return
                return val * MainThread.SamplesPerSecond();

            } else {
                // not flagged as seconds
                
                // assume the value is in samples and return the value
                return val;

            }

        }

        public int Value {
            get {   return this.value;  }
            set {   this.value = value; }
        }

        public Parameters.Units Unit {
            get {   return this.unit;  }
            set {   this.unit = value; }
        }

        public void setValue(String value) {

            // try to parse the value
            int intValue;
            Parameters.Units unit;
            if (!tryParseValue(value, out intValue, out unit)) {

                // message
                logger.Error("Could not store the value for parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "'), value '" + value + "' could not be parsed as an integer");
                return;

            }

            // assign
            this.value = intValue;

        }
        
    }

    public class ParamDouble : ParamDoubleBase, iParam {

        private double value = 0.0;
        private Parameters.Units unit = Parameters.Units.ValueOrSamples;

        public ParamDouble(String name, String group, Parameters parentSet, String desc, String[] options) : base(name, group, parentSet, desc, options) { }
        public T getValue<T>() {

            Type paramType = typeof(T);
            if(paramType == typeof(double)) {     
                // request to return as double

                // return value
                return (T)Convert.ChangeType(Value, typeof(double));

            } else {
                // request to return as other

                // message and return 0
                logger.Error("Could not retrieve the value for parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "') as '" + paramType.Name + "', can only return value as double. Returning 0.0");
                return (T)Convert.ChangeType(0, typeof(T));    

            }
            
        }

        public int getValueInSamples() {

            // retrieve the value as double
            double val = getValue<double>();
            int intSamples = 0;

            // check if the unit is set in seconds
            if (unit == Parameters.Units.Seconds) {
                // flagged as seconds

                // convert, check rounding
                double samples = val * (double)MainThread.SamplesPerSecond();
                intSamples = (int)Math.Round(samples);
                if (samples != intSamples) {

                    // message
                    logger.Warn("Value for parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "') was retrieved in number of samples (" + val + " * " + MainThread.SamplesPerSecond() + "), but has been rounded from " + samples + " to " + intSamples);

                }

            } else {
                // not flagged as seconds
                
                // convert double to int, check rounding
                intSamples = (int)Math.Round(val);
                if (val != intSamples) {

                    // message
                    logger.Warn("Value for parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "') was retrieved in number of samples, but has been rounded from " + val + " to " + intSamples);

                }

            }

            // return number of samples
            return intSamples;

        }

        public double Value {
            get {   return this.value;  }
            set {   this.value = value; }
        }
        
        public Parameters.Units Unit {
            get {   return this.unit;  }
            set {   this.unit = value; }
        }
        
        public void setValue(String value) {

        }

    }


    
    public class ParamBoolArr : ParamBoolBase, iParam {

        private bool[] values = new bool[0];

        public ParamBoolArr(String name, String group, Parameters parentSet, String desc, String[] options) : base(name, group, parentSet, desc, options) {
            minValue = "0";
            maxValue = "1";
        }

        public T getValue<T>() {

            Type paramType = typeof(T);
            if(paramType == typeof(bool[])) {     
                // request to return as bool[]

                // return value
                return (T)Convert.ChangeType(Value, typeof(bool[]));

            } else {
                // request to return as other

                // message and return false
                logger.Error("Could not retrieve the value for parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "') as '" + paramType.Name + "', can only return value as an array of booleans (bool[]). Returning empty array");
                return (T)Convert.ChangeType(false, typeof(T));    

            }
            
        }

        public int getValueInSamples() {

            // message
            logger.Warn("Trying to retrieve the value for bool[] parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "') in number of samples, use getValue<T>() instead");

            // try normal getValue
            return getValue<int>();

        }

        public bool[] Value {
            get {   return this.values;  }
            set {   this.values = value; }
        }

        public void setValue(String value) {

        }

    }


    public class ParamIntArr : ParamIntBase, iParam {

        private int[] values = new int[0];
        private Parameters.Units[] units = new Parameters.Units[0];

        public ParamIntArr(String name, String group, Parameters parentSet, String desc, String[] options) : base(name, group, parentSet, desc, options) { }
        public T getValue<T>() {

            Type paramType = typeof(T);
            if(paramType == typeof(int[])) {     
                // request to return as int[]

                // return vlaue
                return (T)Convert.ChangeType(Value, typeof(int[]));

            } else {
                // request to return as other

                // message and return 0
                logger.Error("Could not retrieve the value for parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "') as '" + paramType.Name + "', can only return value as an array of integers (int[]). Returning empty array");
                return (T)Convert.ChangeType(0, typeof(T));    

            }
            
        }

        public int getValueInSamples() {
            
            // TODO:

            //
            return getValue<int>();

        }

        public int[] Value {
            get {   return this.values;  }
            set {

                // re-initialize the buffer holding the units for the values
                units = new Parameters.Units[value.Length];

                // set the values
                this.values = value;

            }
        }

        public Parameters.Units[] Unit {
            get {   return this.units;  }
            set {

                // check length
                if (values.Length != value.Length) {
                    
                    // message
                    logger.Error("Could not set the units for parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "'), the given length of the unit array does not match the length of the value array");
                    
                    // return without doing anything
                    return;
                
                }
                
                this.units = value; 
            }
        }
        
        public void setValue(String value) {

        }

    }

    public class ParamDoubleArr : ParamDoubleBase, iParam {

        private double[] values = new double[0];
        private Parameters.Units[] units = new Parameters.Units[0];

        public ParamDoubleArr(String name, String group, Parameters parentSet, String desc, String[] options) : base(name, group, parentSet, desc, options) { }
        public T getValue<T>() {

            Type paramType = typeof(T);
            if(paramType == typeof(double[])) {     
                // request to return as double[]

                // return vlaue
                return (T)Convert.ChangeType(Value, typeof(double[]));

            } else {
                // request to return as other

                // message and return 0
                logger.Error("Could not retrieve the value for parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "') as '" + paramType.Name + "', can only return value as an array of doubles (double[]). Returning empty array");
                return (T)Convert.ChangeType(0, typeof(T));    

            }
            
        }

        public int getValueInSamples() {
            
            // TODO:

            //
            return getValue<int>();

        }

        public double[] Value {
            get {   return this.values;  }
            set {

                // re-initialize the buffer holding the units for the values
                units = new Parameters.Units[value.Length];

                // set the values
                this.values = value;

            }
        }
        
        public void setValue(String value) {

        }

    }


    
    public class ParamBoolMat : ParamBoolBase, iParam {

        private bool[][] values = new bool[0][];

        public ParamBoolMat(String name, String group, Parameters parentSet, String desc, String[] options) : base(name, group, parentSet, desc, options) {
            minValue = "0";
            maxValue = "1";
        }

        public T getValue<T>() {

            Type paramType = typeof(T);
            if(paramType == typeof(bool[][])) {     
                // request to return as bool[][]

                // return value
                return (T)Convert.ChangeType(Value, typeof(bool[][]));

            } else {
                // request to return as other

                // message and return false
                logger.Error("Could not retrieve the value for parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "') as '" + paramType.Name + "', can only return value as a matrix of booleans (bool[][]). Returning empty matrix");
                return (T)Convert.ChangeType(false, typeof(T));    

            }
            
        }

        public int getValueInSamples() {

            // message
            logger.Warn("Trying to retrieve the value for bool[][] parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "') in number of samples, use getValue<T>() instead");
            
            // try normal getValue
            return getValue<int>();

        }

        public bool[][] Value {
            get {   return this.values;  }
            set {   this.values = value; }
        }
        
        public void setValue(String value) {

        }

    }


    public class ParamIntMat : ParamIntBase, iParam {

        private int[][] values = new int[0][];
        private Parameters.Units[][] units = new Parameters.Units[0][];

        public ParamIntMat(String name, String group, Parameters parentSet, String desc, String[] options) : base(name, group, parentSet, desc, options) { }
        public T getValue<T>() {

            Type paramType = typeof(T);
            if(paramType == typeof(int[][])) {     
                // request to return as int[][]

                // return vlaue
                return (T)Convert.ChangeType(Value, typeof(int[][]));

            } else {
                // request to return as other

                // message and return 0
                logger.Error("Could not retrieve the value for parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "') as '" + paramType.Name + "', can only return value as a matrix of integers (int[][]). Returning empty matrix");
                return (T)Convert.ChangeType(0, typeof(T));    

            }
            
        }

        public int getValueInSamples() {
            
            // TODO:

            //
            return getValue<int>();

        }

        public int[][] Value {
            get {   return this.values;  }
            set {

                // re-initialize the buffer holding the units for the values
                units = new Parameters.Units[value.Length][];
                for (int i = 0; i < value.Length; i++)  units[i] = new Parameters.Units[value[i].Length];

                // set the values
                this.values = value;

            }
        }

        public void setValue(String value) {

        }

    }

    public class ParamDoubleMat : ParamDoubleBase, iParam {

        private double[][] values = new double[0][];
        private Parameters.Units[][] units = new Parameters.Units[0][];

        public ParamDoubleMat(String name, String group, Parameters parentSet, String desc, String[] options) : base(name, group, parentSet, desc, options) { }
        public T getValue<T>() {

            Type paramType = typeof(T);
            if(paramType == typeof(double[][])) {     
                // request to return as double[][]

                // return vlaue
                return (T)Convert.ChangeType(Value, typeof(double[][]));

            } else {
                // request to return as other

                // message and return 0
                logger.Error("Could not retrieve the value for parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "') as '" + paramType.Name + "', can only return value as a matrix of doubles (double[][]). Returning empty matrix");
                return (T)Convert.ChangeType(0, typeof(T));    

            }
            
        }


        public int getValueInSamples() {
            
            // TODO: 
            
            // 
            return getValue<int>();
        }

        public double[][] Value {
            get {   return this.values;  }
            set {

                // re-initialize the buffer holding the units for the values
                units = new Parameters.Units[value.Length][];
                for (int i = 0; i < value.Length; i++)  units[i] = new Parameters.Units[value[i].Length];

                // set the values
                this.values = value;

            }

        }
        
        public void setValue(String value) {

        }

    }
    
    public class ParamColor : Param, iParam {

        private RGBColorFloat value = new RGBColorFloat();

        public ParamColor(String name, String group, Parameters parentSet, String desc, String[] options) : base(name, group, parentSet, desc, options) {
            minValue = "0";
            maxValue = "16777216";
        }

        public T getValue<T>() {

            Type paramType = typeof(T);
            if(paramType == typeof(RGBColorFloat)) {
                // request to return as RGBColorFloat

                // return value
                return (T)Convert.ChangeType(Value, typeof(RGBColorFloat));

            } else {
                // request to return as other

                // message and return false
                logger.Error("Could not retrieve the value for parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "') as '" + paramType.Name + "', can only return value as color (RGBColorFloat). Returning null");
                return (T)Convert.ChangeType(null, typeof(T));    

            }
            
        }

        public int getValueInSamples() {


            // message
            logger.Warn("Trying to retrieve the value for color parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "') in number of samples, use getValue<T>() instead, returning 0");
            
            // return 0
            return 0;

        }

        public RGBColorFloat Value {
            get {   return this.value;  }
            set {   this.value = value; }
        }
        
        public bool setStdValue(String stdValue) {
            return true;
        }

        public void setValue(String value) {

        }
        
    }


}
