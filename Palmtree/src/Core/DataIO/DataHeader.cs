/**
 * The DataHeader class
 * 
 * ...
 * 
 * 
 * Copyright (C) 2017:  RIBS group (Nick Ramsey Lab), University Medical Center Utrecht (The Netherlands) & external contributors
 * Author(s):           Max van den Boom            (info@maxvandenboom.nl)
 *                      Benny van der Vijgh         (benny@vdvijgh.nl)
 * 
 * This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software
 * Foundation, either version 3 of the License, or (at your option) any later version. This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for
 * more details. You should have received a copy of the GNU General Public License along with this program. If not, see <https://www.gnu.org/licenses/>.
 */

namespace Palmtree.Core.DataIO {

    /// <summary>
    /// The <c>DataHeader</c> class.
    /// 
    /// ...
    /// </summary>
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
