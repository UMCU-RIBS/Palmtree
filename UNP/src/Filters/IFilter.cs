using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UNP.Core.Helpers;
using UNP.Core.Params;

namespace UNP.Filters {

    interface IFilter {

        int getClassVersion();
        string getName();
        Parameters getParameters();

        bool configure(ref SampleFormat input, out SampleFormat output);
        void initialize();
        bool configureRunningFilter(Parameters newParameters, bool resetFilter);

        void start();
        void stop();
        bool isStarted();

        void process(double[] input, out double[] output);

        void destroy();

    }

}
