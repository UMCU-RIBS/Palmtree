using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UNP.Core.Helpers;
using UNP.Core.Params;

namespace UNP.Plugins {

    interface IPlugin {

        int getClassVersion();
        Parameters getParameters();

        bool configure();
        void initialize();

        void start();
        void stop();
        bool isStarted();

        void process();

        void destroy();

    }

}
