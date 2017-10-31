using System;
using UNP.Core.Helpers;
using UNP.Core.Params;

namespace UNP.Sources {

    public interface ISource {

        int getClassVersion();
        string getClassName(); 
        Parameters getParameters();

        bool configure(out PackageFormat output);
        void initialize();

        double getInputSamplesPerSecond();
        double getOutputSamplesPerSecond();
        
        void start();
        void stop();
        bool isRunning();
        bool isStarted();

        void destroy();

    }

}
