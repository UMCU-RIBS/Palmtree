using System;
using System.IO;
using System.Runtime.InteropServices;

using UNP.Applications;
using UNP.Core;

namespace EmptyTask {


    class Program {

        static void Main(string[] args) {

            Type t = Type.GetType("EmptyTask.EmptyTask");
            MainBoot.Run(args, t);

        }

    }
}
