using System;
using System.IO;
using System.Runtime.InteropServices;
using UNP;
using UNP.Core;
using UNP.Applications;

namespace FollowTask {


    class Program {

        static void Main(string[] args) {

            Type t = Type.GetType("FollowTask.FollowTask");
            MainBoot.Run(args, t);

        }

    }
}
