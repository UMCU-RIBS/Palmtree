using System;
using System.IO;
using System.Runtime.InteropServices;

using UNP.Applications;
using UNP.Core;

namespace CursorTask {


    class Program {

        static void Main(string[] args) {

            Type t = Type.GetType("CursorTask.CursorTask");
            MainBoot.Run(args, t);

        }

    }
}
