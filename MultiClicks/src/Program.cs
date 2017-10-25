using System;
using System.IO;
using System.Runtime.InteropServices;
using UNP.Core;
using UNP.Applications;

namespace MultiClicksTask {


    class Program {

        static void Main(string[] args) {

            Type t = Type.GetType("MultiClicksTask.MultiClicksTask");
            MainBoot.Run(args, t);

        }

    }
}
