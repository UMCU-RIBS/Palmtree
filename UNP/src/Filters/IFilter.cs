using System;
using UNP.Core.Helpers;
using UNP.Core.Params;

namespace UNP.Filters {

    public interface IFilter {

        int getClassVersion();
        string getName();
        Parameters getParameters();

        bool configure(ref PackageFormat input, out PackageFormat output);
        void initialize();
        bool configureRunningFilter(Parameters newParameters, bool resetFilter);

        void start();
        void stop();
        bool isStarted();

        void process(double[] input, out double[] output);

        void destroy();

    }

}
