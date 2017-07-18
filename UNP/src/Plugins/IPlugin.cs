using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UNP.Core.Helpers;
using UNP.Core.Params;

namespace UNP.Plugins {

    interface IPlugin {

        int getClassVersion();
        string getName();
        Parameters getParameters();

        bool configure();
        void initialize();

        void start();
        void stop();
        bool isStarted();

        void preFiltersProcess();      // execute before the filters
        void postFiltersProcess();     // execute after the filters

        void destroy();

    }

}
