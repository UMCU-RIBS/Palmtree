using System;
using System.IO;
using System.Runtime.InteropServices;
using UNP;
using UNP.Core;
using UNP.Applications;

namespace FollowTask {


    class Program {

        static void Main(string[] args) {

            // check if all dependencies exists
            


            // TODO: Add startup arguments (args)
            // - nogui = start without GUI
            // - parameter file =
            // - autosetconfig = 
            // - autostart = 
            
            Type t = Type.GetType("FollowTask.FollowTask");
            MainBoot.Run(t);

        }

    }
}
