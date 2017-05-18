using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UNP.Core.Helpers;
using UNP.Core.Params;

namespace UNP.Sources {

    interface ISource {

        Parameters getParameters();

        bool configure(out SampleFormat output);
        void initialize();

        double getSamplesPerSecond();

        void start();
        void stop();
        bool isRunning();
        bool isStarted();

        void destroy();

    }

}
