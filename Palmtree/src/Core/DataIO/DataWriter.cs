/**
 * The DataWriter class
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
        public static void writeBinaryHeader(FileStream writer, DataHeader header) {

            // retrieve code into local variable
            string headerCode = header.code;

            // make sure the header code is always 3 characters long
            if (headerCode.Length != 3) headerCode = "xxx";

            // determine whether the header is a plugin
            bool isPlugin = !(string.Compare(headerCode, "src") == 0 || string.Compare(headerCode, "dat") == 0);


            //
            // prepare column names
            //
            
            // variable holding the column names and columncount
            string columnNames = "";
            int numColumns = 0;

            // add sample id column
            if (header.version == 1)                                    columnNames += "Sample\t";
            else if (header.version == 2 || header.version == 3)        columnNames += "SamplePackage\t";
            numColumns++;

            // add elapsed column (when not plugin)
            if (!isPlugin) {
                columnNames += "Elapsed_ms\t";
                numColumns++;
            }

            // add include source input time column
            if (headerCode.Equals("src") && header.version == 3) {
                columnNames += "Source_time\t";
                numColumns++;
            }

            // add stream names to the local variable of that holds all the column names
            columnNames += string.Join("\t", header.columnNames);
            numColumns += header.columnNames.Length;

            // convert column names to binary
            byte[] columnNamesBinary = Encoding.ASCII.GetBytes(columnNames);


            //
            // write header
            //
            
            // version
            byte[] versionBinary = BitConverter.GetBytes(header.version);
            writer.Write(versionBinary, 0, versionBinary.Length);

            // code
            byte[] headerCodeBinary = Encoding.ASCII.GetBytes(headerCode);
            writer.Write(headerCodeBinary, 0, headerCodeBinary.Length);

            // epochs
            if (header.version == 2 || header.version == 3) {
                byte[] headerRunStartEpoch = BitConverter.GetBytes(header.runStartEpoch);
                writer.Write(headerRunStartEpoch, 0, headerRunStartEpoch.Length);

                byte[] headerFileStartEpoch = BitConverter.GetBytes(header.fileStartEpoch);
                writer.Write(headerFileStartEpoch, 0, headerFileStartEpoch.Length);
            }

            // include source input time (only in source data-file)
            if (headerCode.Equals("src") && header.version == 3) {
                writer.WriteByte(Convert.ToByte(header.includesSourceInputTime));
            }

            // sample rate
            byte[] sampleRateBinary = BitConverter.GetBytes(header.sampleRate);
            writer.Write(sampleRateBinary, 0, sampleRateBinary.Length);

            // playback streams
            byte[] numPlaybackInputStreamsBinary = BitConverter.GetBytes(header.numPlaybackStreams);
            writer.Write(numPlaybackInputStreamsBinary, 0, numPlaybackInputStreamsBinary.Length);

            // # streams + streams details (V2)
            if (header.version == 2 || header.version == 3) {
                byte[] numStreamsBinary = BitConverter.GetBytes(header.numStreams);
                writer.Write(numStreamsBinary, 0, numStreamsBinary.Length);
                for (int i = 0; i < header.numStreams; i++) {
                    writer.Write(new byte[] {header.streamDataTypes[i] }, 0, 1);
                    byte[] streamDataSamplesBinary = BitConverter.GetBytes(header.streamDataSamplesPerPackage[i]);
                    writer.Write(streamDataSamplesBinary, 0, streamDataSamplesBinary.Length);
                }
            }

            // # columns, column-names byte-length and names
            byte[] numColumnsBinary = BitConverter.GetBytes(numColumns);
            writer.Write(numColumnsBinary, 0, numColumnsBinary.Length);
            byte[] columnNamesLengthBinary = BitConverter.GetBytes(columnNamesBinary.Length);
            writer.Write(columnNamesLengthBinary, 0, columnNamesLengthBinary.Length);
            writer.Write(columnNamesBinary, 0, columnNamesBinary.Length);

            // Note: don't close writer. Data could/will be written after this

        }

    }

}
