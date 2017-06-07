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

        protected bool mEnableFilter = false;
        protected bool mLogSampleStreams = false;
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
