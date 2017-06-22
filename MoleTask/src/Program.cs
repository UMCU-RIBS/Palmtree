using System;
using System.IO;
using System.Runtime.InteropServices;
using UNP;
using UNP.Applications;
using UNP.Core;

namespace MoleTask {


    class Program {

        static void Main(string[] args) {

            Type t = Type.GetType("MoleTask.MoleTask");
            MainBoot.Run(args, t);

        }

    }
}
