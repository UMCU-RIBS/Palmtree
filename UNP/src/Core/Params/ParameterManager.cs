using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UNP.Core.Params {

    // ParameterManager class
    // 
    // Since all the calls in this class refer to (or give a reference to) the Dictionary object, this class is thread safe
    // 
    public static class ParameterManager {

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

    }

}
