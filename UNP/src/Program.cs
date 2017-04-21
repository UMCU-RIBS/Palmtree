using System;
using UNP;
using UNP.Applications;
using UNP.Core;

namespace UNP {
    class Program {

        static void Main(string[] args) {

            // check if all dependencies exists

            // TODO: Add startup arguments (args)
            // - nogui = start without GUI
            // - parameter file =
            // - autosetconfig = 
            // - autostart = 
            
            Type t = Type.GetType("UNP.Applications.EmptyTask");
            MainBoot.Run(t);

        }

    }
}
