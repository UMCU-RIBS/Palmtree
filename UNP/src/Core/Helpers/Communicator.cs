using NLog;
using System.IO;
using System.Diagnostics;
using System.Windows.Forms;


namespace UNP.Applications {

    public class Communicator : ExecWrapper {

        private static Logger logger = LogManager.GetLogger("Communicator");

        public Communicator(string filepath) : base(filepath) { }

        new public void close() {

            // Communicator has a button to quit when it receives a F13 keypress
            SendKeys.SendWait("{F13}");
        } 
    }
}
