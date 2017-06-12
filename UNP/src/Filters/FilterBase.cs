using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UNP.Core.Params;

namespace UNP.Filters {

    public class FilterBase {

        protected string filterName = "";
        protected Logger logger = null;
        protected Parameters parameters = null;

        protected bool mEnableFilter = false;                   // Filter is enabled or disabled
        protected bool mLogSampleStreams = false;               // stores whether the initial configuration has the logging of sample streams enabled or disabled
        protected bool mLogSampleStreamsRuntime = false;        // stores whether during runtime the sample streams should be logged    (if it was on, then it can be switched off, resulting in 0's being logged)

        protected uint inputChannels = 0;
        protected uint outputChannels = 0;

        public string getName() {
            return filterName;
        }

        public Parameters getParameters() {
            return parameters;
        }

    }

}
