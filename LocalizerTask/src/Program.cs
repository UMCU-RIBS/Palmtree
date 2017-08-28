using System;
using System.IO;
using System.Runtime.InteropServices;

using UNP.Applications;
using UNP.Core;

namespace LocalizerTask {


    class Program {

        static void Main(string[] args) {

            Type t = Type.GetType("LocalizerTask.LocalizerTask");
            MainBoot.Run(args, t);

        }

    }
}
