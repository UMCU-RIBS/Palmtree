using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UNP.Core.Params {

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

}
