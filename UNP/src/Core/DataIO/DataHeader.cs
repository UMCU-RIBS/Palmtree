using System;

namespace UNP.Core.DataIO {

    public class DataHeader {

        
        public int version = 0;                         // version
        public string code = "";                        // code

        public double sampleRate = 0;                   // the sample rate (in Hz)
        public int numPlaybackStreams = 0;              // number of playback input streams (needs to be > 0 for playback)
        public int numColumns = 0;                      // total number of columns per sample
        public int columnNamesSize = 0;                 // the size (in bytes) of all the column names stored in the header
        public string[] columnNames = new string[0];    // the column names

        public int rowSize = 0;                         // the size (in bytes) of one row
        public long numRows = 0;                        // total number of rows

        public long posDataStart = 0;                   // position (counted in bytes) to the start of the data in the file

    }

}
