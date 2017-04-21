using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UNP.Core.Helpers;

namespace UNP.Applications {

    public interface IApplication {

        bool configure(ref SampleFormat input);
        void initialize();

        void start();
        void stop();
        bool isStarted();

        void process(double[] input);

        void destroy();



    }
}
