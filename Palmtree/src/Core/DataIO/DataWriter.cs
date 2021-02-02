/**
 * The DataWriter class
 * 
 * ...
 * 
 * 
 * Copyright (C) 2017:  RIBS group (Nick Ramsey Lab), University Medical Center Utrecht (The Netherlands) & external contributors
 * Author(s):           Benny van der Vijgh         (benny@vdvijgh.nl)
 *                      Max van den Boom            (info@maxvandenboom.nl)
 * 
 * This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software
 * Foundation, either version 3 of the License, or (at your option) any later version. This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for
 * more details. You should have received a copy of the GNU General Public License along with this program. If not, see <https://www.gnu.org/licenses/>.
 */
using System;
using System.IO;
using System.Text;

namespace Palmtree.Core.DataIO {

    /// <summary>
    /// The <c>DataWriter</c> class.
    /// 
    /// ...
    /// </summary>
    public class DataWriter {

        /**
         * Create header of .dat (plugin or filter) or .src file
         * 
         * boolean timing either includes (true) or excludes (false) extra column in header file for storing elapsed time 
         **/
        public static void writeBinaryHeader(BinaryWriter writer, DataHeader header) {

            // retrieve code into local variable
            string headerCode = header.code;

            // make sure the header code is always 3 characters long
            if (headerCode.Length != 3) headerCode = "xxx";

            // determine whether the header is a plugin
            bool isPlugin = !(string.Compare(headerCode, "src") == 0 || string.Compare(headerCode, "dat") == 0);

            //
            //
            //

            // variable holding the column names and columncount
            string columnNames = "";
            int numColumns = 0;

            // add sample id column
            columnNames += "Sample\t";
            numColumns++;

            // add timing column if desired
            if (!isPlugin) {
                columnNames += "Elapsed_ms\t";
                numColumns++;
            }

            // add streams as columns
            columnNames += string.Join("\t", header.columnNames);
            numColumns += header.columnNames.Length;

            // convert column names to binary and store the length (in bytes)
            byte[] columnNamesBinary = Encoding.ASCII.GetBytes(columnNames);



            //
            //
            //

            // store number of columns and of source channels [bytes] 
            byte[] versionBinary = BitConverter.GetBytes(header.version);
            byte[] headerCodeBinary = Encoding.ASCII.GetBytes(headerCode);
            byte[] sampleRateBinary = BitConverter.GetBytes(header.sampleRate);
            byte[] numPlaybackInputStreamsBinary = BitConverter.GetBytes(header.numPlaybackStreams);
            byte[] ncolBinary = BitConverter.GetBytes(numColumns);
            byte[] columnNamesLengthBinary = BitConverter.GetBytes(columnNamesBinary.Length);

            // determine the length of the header
            int headerLength = 0;
            headerLength += versionBinary.Length;                   // sizeof(int)
            headerLength += headerCodeBinary.Length;                // sizeof(int)
            headerLength += sampleRateBinary.Length;                // sizeof(double)
            headerLength += numPlaybackInputStreamsBinary.Length;   // sizeof(int)
            headerLength += ncolBinary.Length;                      // sizeof(int)
            headerLength += columnNamesLengthBinary.Length;         // sizeof(int)
            headerLength += columnNamesBinary.Length;               // chars * sizeof(char)

            // create an output header
            byte[] headerOut = new byte[headerLength];
            int filePointer = 0;

            // version
            Buffer.BlockCopy(versionBinary, 0, headerOut, filePointer, versionBinary.Length);
            filePointer += versionBinary.Length;

            // header code
            Buffer.BlockCopy(headerCodeBinary, 0, headerOut, filePointer, headerCodeBinary.Length);
            filePointer += headerCodeBinary.Length;

            // sample rate
            Buffer.BlockCopy(sampleRateBinary, 0, headerOut, filePointer, sampleRateBinary.Length);
            filePointer += sampleRateBinary.Length;

            // playback input streams
            Buffer.BlockCopy(numPlaybackInputStreamsBinary, 0, headerOut, filePointer, numPlaybackInputStreamsBinary.Length);
            filePointer += numPlaybackInputStreamsBinary.Length;

            // number of streams/columns
            Buffer.BlockCopy(ncolBinary, 0, headerOut, filePointer, ncolBinary.Length);
            filePointer += ncolBinary.Length;

            // total length of the header names
            Buffer.BlockCopy(columnNamesLengthBinary, 0, headerOut, filePointer, columnNamesLengthBinary.Length);
            filePointer += columnNamesLengthBinary.Length;

            // column names
            Buffer.BlockCopy(columnNamesBinary, 0, headerOut, filePointer, columnNamesBinary.Length);

            // write header to file
            writer.Write(headerOut);

        }


    }

}
