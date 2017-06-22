using System;

using UNP.Applications;
using UNP.Core;

namespace UNP {
    class Program {

        static void Main(string[] args) {

            Type t = Type.GetType("UNP.Applications.EmptyTask");
            MainBoot.Run(args, t);

        }

    }
}
