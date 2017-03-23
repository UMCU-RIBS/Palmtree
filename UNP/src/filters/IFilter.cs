using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UNP.helpers;

namespace UNP.filters {

    interface IFilter {

        Parameters getParameters();
        bool configure(ref SampleFormat input, out SampleFormat output);
        void initialize();

        void start();
        void stop();
        bool isStarted();

        void process(double[] input, out double[] output);

        void destroy();

    }

}
