using System;
using UNP;
using UNP.applications;

namespace UNP {
    class Program {

        static void Main(string[] args) {

            // check if all dependencies exists

            // TODO: Add startup arguments (args)
            // - nogui = start without GUI
            // - parameter file =
            // - autosetconfig = 
            // - autostart = 
            
            Type t = Type.GetType("UNP.applications.EmptyTask");
            MainBoot.Run(t);

        }

    }
}
