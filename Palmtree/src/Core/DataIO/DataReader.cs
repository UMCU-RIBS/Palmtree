/**
 * The DataReader class
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
using NLog;
using System;
using System.IO;
using System.Text;

namespace Palmtree.Core.DataIO {

    /// <summary>
    /// The <c>DataReader</c> class.
    /// 
    /// ...
    /// </summary>
    public class DataReader {

        private static Logger logger = LogManager.GetLogger("DataReader");

        private Object lockReader = new Object();                          // threadsafety lock for reader

        private string filename = "";
        private DataHeader header = null;
        private FileStream dataStream = null;
        private long currentRowIndex = 0;

        public DataReader(string filename) {
            this.filename = filename;
        }

        public DataHeader getHeader() {

            // get the header information
            if (header == null)     header = readHeader(filename);

            // return the header information
            return header;

        }

        public bool open() {

            // threadsafety
            lock (lockReader) {

                // check if file does not exists
                if (!File.Exists(filename)) {

                    // message
                    logger.Error("Could not open data file '" + filename + "' for reading");

                    // return failure
                    return false;

                }
                DataHeader a = getHeader();

                // make sure the header is read
                // return false if the header could not be read
                if (getHeader() == null || string.IsNullOrEmpty(getHeader().code)) {
                    return false;
                }

                try {

                    // open file stream
                    dataStream = new FileStream(filename, FileMode.Open);

                } catch (Exception) {

                    // message
                    logger.Error("Could not create filestream to data file '" + filename + "' for reading");

                    // return failure
                    return false;

                }

                // set the data pointer to the beginning
                resetDataPointer();

                // return success
                return true;

            }   // end lock

        }

        public void close() {

            // threadsafety
            lock (lockReader) {

                // if a datastream is open, close it
                if (dataStream != null) {
                    dataStream.Close();
                    dataStream = null;
                }

                // clear the header
                header = null;

                // reset the row index
                currentRowIndex = 0;

            }

        }

        public void resetDataPointer() {

            // threadsafety
            lock (lockReader) {

                // check if the reader has not been opened
                if (header == null || dataStream == null) {

                    // message
                    logger.Error("Trying to reset data pointer without opening the reader first");

                    // return
                    return;

                }

                // set the pointer to the start of the data
                if (header.posDataStart < dataStream.Length)
                    dataStream.Position = header.posDataStart;

                // set the row index to the first row
                currentRowIndex = 0;

            }

        }


        public long readNextRows(long numRows, out byte[] buffer) {

            // threadsafety
            lock (lockReader) {

                // check if the reader has not been opened
                if (header == null || dataStream == null) {

                    // message
                    logger.Error("Trying to read rows without opening the reader first, returning null");

                    // return
                    buffer = null;
                    return -1;

                }

                // check if the reading the given number of rows exceeds the end of the data
                // correct the number of rows to the maximum available rows
                if (currentRowIndex + numRows > header.numRows)
                    numRows = header.numRows - currentRowIndex;

                // if there are no rows left to read, return -1
                if (numRows == 0) {
                    buffer = null;
                    return -1;
                }

                // calculate the number of bytes to read
                long numBytes = header.rowSize * numRows;

                // check if the number of bytes is bigger than int (since the filestream's read function only takes int as length)
                if (numBytes > Int32.MaxValue) {

                    // message
                    logger.Error("The function 'readNextRows' is asked to read too much data at once, ask for a smaller amount of rows per call");

                    // return
                    buffer = null;
                    return -1;

                }

                // check if the number of bytes to be read exceeds the file length
                if (dataStream.Position + numBytes > dataStream.Length) {

                    // message
                    logger.Error("The function 'readNextRows' tries to read beyond the length of the file, something wrong with determining the number of rows, check code");

                    // return
                    buffer = null;
                    return -1;

                }

                // read the data
                buffer = new byte[numBytes];
                if (dataStream.Read(buffer, 0, (int)(numBytes)) < 0) {

                    // message
                    logger.Error("The function 'readNextRows' tries to read beyond the length of the file, something wrong with determining the number of rows, check code");

                    // return
                    buffer = null;
                    return -1;

                }

                // move the current row index up the number of read rows
                currentRowIndex += numRows;

                // return the rowData as byte array
                return numRows;

            } // end lock

        }
        
        public long readNextRows(long numRows, out uint[] arrSamples, out double[][] matValues) {
            byte[] bOutput = null;

            // read the next rows
            numRows = readNextRows(numRows, out bOutput);

            // check if an error occured while reading
            if (numRows == -1) {

                // set output to null
                arrSamples = null;
                matValues = null;

                // return
                return -1;

            }

            // threadsafety
            lock (lockReader) {
                
                // create new arrays
                arrSamples = new uint[numRows];
                matValues = new double[numRows][];

                // loop through the rows
                for (int i = 0; i < numRows; i++) {

                    // store the samples
                    // TODO: hack to make sure wsp files can be read, need to conform sp files to data format: use uint for sampleId, not a double
                    if (header.code == "wsp") {
                        arrSamples[i] = (uint)BitConverter.ToDouble(bOutput, i * header.rowSize);
                    } else {
                        arrSamples[i] = BitConverter.ToUInt32(bOutput, i * header.rowSize);
                    }

                    // store the values
                    matValues[i] = new double[header.numColumns - 1];
                    // TODO: hack to make sure wsp files can be read, need to conform sp files to data format: use uint for sampleId, not a double
                    if (header.code == "wsp") {
                        Buffer.BlockCopy(bOutput, i * header.rowSize + sizeof(double), matValues[i], 0, header.rowSize - sizeof(double));
                    } else {
                        Buffer.BlockCopy(bOutput, i * header.rowSize + sizeof(uint), matValues[i], 0, header.rowSize - sizeof(uint));
                    }
                }

                // return the numbers of rows
                return numRows;

            } // end lock

        }

        public bool reachedEnd() {

            // threadsafety
            lock (lockReader) {

                return currentRowIndex >= header.numRows;

            }

        }

        public static DataHeader readHeader(String fileName) {

            // create a new data header object
            DataHeader header = new DataHeader();

            FileStream dataStream = null;
            try {

                // open file stream
                dataStream = new FileStream(fileName, FileMode.Open);

                // retrieve version number
                byte[] bVersion = new byte[sizeof(int)];
                dataStream.Read(bVersion, 0, sizeof(int));
                header.version = BitConverter.ToInt32(bVersion, 0);

                if (header.version == 1) {

                    // retrieve the code from the header
                    byte[] bCode = new byte[3];
                    dataStream.Read(bCode, 0, 3);
                    header.code = Encoding.ASCII.GetString(bCode);

                    // retrieve the fixed fields from the header    (note that the pointer has already been moved till after the version bytes)
                    int fixedBytesInHeader = 0;
                    fixedBytesInHeader += sizeof(double);   // sample rate
                    fixedBytesInHeader += sizeof(int);      // number of playback input streams
                    fixedBytesInHeader += sizeof(int);      // number of columns
                    fixedBytesInHeader += sizeof(int);      // size of the column names
                    byte[] bFixedHeader = new byte[fixedBytesInHeader];
                    dataStream.Read(bFixedHeader, 0, fixedBytesInHeader);

                    // pointer to a variable in the fixed header
                    int ptrFixedHeader = 0;

                    // retrieve the sample rate
                    header.sampleRate = BitConverter.ToDouble(bFixedHeader, ptrFixedHeader);
                    ptrFixedHeader += sizeof(double);

                    // retrieve the number of playback input streams
                    header.numPlaybackStreams = BitConverter.ToInt32(bFixedHeader, ptrFixedHeader);
                    ptrFixedHeader += sizeof(int);

                    // retrieve the number of columns
                    header.numColumns = BitConverter.ToInt32(bFixedHeader, ptrFixedHeader);
                    ptrFixedHeader += sizeof(int);

                    // retrieve the size of the column names
                    header.columnNamesSize = BitConverter.ToInt32(bFixedHeader, ptrFixedHeader);
                    ptrFixedHeader += sizeof(int);

                    // retrieve the column names from the header
                    byte[] bColumnNames = new byte[header.columnNamesSize];
                    dataStream.Read(bColumnNames, 0, header.columnNamesSize);
                    header.columnNames = Encoding.ASCII.GetString(bColumnNames).Split('\t');

                    // determine the size of one row (in bytes)
                    // TODO: hack to make sure wsp files can be read, need to conform sp files to data format: use uint for sampleId, not a double
                    if (header.code=="wsp") {
                        header.rowSize = header.numColumns * sizeof(double);
                    } else {
                        header.rowSize = sizeof(uint);                                    // sample id
                        header.rowSize += (header.numColumns - 1) * sizeof(double);      // data
                    }

                    // store the position where the data starts (= the current position of the pointer in the stream after reading the header)
                    header.posDataStart = dataStream.Position;

                    // determine the number of rows
                    header.numRows = (dataStream.Length - header.posDataStart) / header.rowSize;

                }

            } catch (Exception) {

                // close the data stream
                if (dataStream != null)     dataStream.Close();

                // return failure
                return null;

            } finally {

                // close the data stream
                if (dataStream != null)     dataStream.Close();

            }

            return header;
        }



    }
}
