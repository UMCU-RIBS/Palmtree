using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UNP.helpers;

namespace UNP.sources {

    interface ISource {

        Parameters getParameters();

        bool configure(out SampleFormat output);
        void initialize();

        int getSamplesPerSecond();

        void start();
        void stop();
        bool isRunning();
        bool isStarted();

        void destroy();

    }

}
