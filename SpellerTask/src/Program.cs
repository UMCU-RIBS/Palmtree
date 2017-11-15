using System;
using UNP.Core;

namespace SpellerTask {

    class Program {

        static void Main(string[] args) {

            Type t = Type.GetType("SpellerTask.SpellerTask");
            MainBoot.Run(args, t);
        }
    }
}
