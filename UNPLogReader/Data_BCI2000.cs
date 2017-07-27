using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UNPLogReader {
    public class Data_BCI2000 {

        public string firstLine = "";
        public int headerLen = 0;
        public int sourceCh = 0;
        public int stateVectorLen = 0;

        public string header = "";
        public double samplingRate = 0;         // from parameters


        public int numSamples = 0;

    }
}
