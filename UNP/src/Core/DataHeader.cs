using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UNP.Core {

    public class DataHeader {

        
        public int version = 0;                     // version

        public int pipelineInputStreams = 0;        // number of pipeline input streams (needs to be > 0 for playback)
        public int numColumns = 0;                  // total number of columns per sample
        public int columnNamesLength = 0;           // the length of all the column names stored in the header (in bytes)

        public int numberOfRows = 0;           // total number of rows
        public int headerLength = 0;           // header length (in bytes)

        public int rowSize = 0;                // size of single row of data


    }

}
