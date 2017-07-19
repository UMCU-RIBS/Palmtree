using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UNP.Core.DataIO {

    public class DataHeader {

        
        public int version = 0;                         // version
        public string extension = "";                   // extension

        public int pipelineInputStreams = 0;            // number of pipeline input streams (needs to be > 0 for playback)
        public int numColumns = 0;                      // total number of columns per sample
        public int columnNamesSize = 0;                 // the size (in bytes) of all the column names stored in the header
        public string[] columnNames = new string[0];    // the column names

        public int rowSize = 0;                         // the size (in bytes) of one row
        public long numRows = 0;                         // total number of rows

        public long posDataStart = 0;                   // position (counted in bytes) to the start of the data in the file

    }

}
