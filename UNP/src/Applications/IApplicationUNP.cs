using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UNP.Core.Params;

namespace UNP.Applications {

    public interface IApplicationUNP {

        int getClassVersion();
        String getClassName();

        void UNP_start(Parameters parentParameters);
        void UNP_stop();                        // stops the task from running. The parent process should check isRunning (whether the task stopped) and is responsible for removing the object
        bool UNP_isRunning();                   // returns whether the task is running (will also return false when the task is finished)

        void UNP_process(double[] input, bool connectionLost);

        void UNP_resume();
        void UNP_suspend();

    }

}
