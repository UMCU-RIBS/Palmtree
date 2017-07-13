using System;
using UNP.Core.Helpers;
using UNP.Core.Params;

namespace UNP.Applications {

    public interface IApplication {

        int getClassVersion();
        Parameters getParameters();
        string getClassName();

        bool configure(ref SampleFormat input);
        void initialize();

        void start();
        void stop();
        bool isStarted();

        void process(double[] input);

        void destroy();



    }
}
