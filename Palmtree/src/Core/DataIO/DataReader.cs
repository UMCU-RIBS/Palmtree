/**
 * DataReader class
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
    /// DataReader class.
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

            lock (lockReader) {

                // check if the reader has not been opened
                if (header == null || dataStream == null) {

                    // message and return failure
                    logger.Error("Trying to read rows without opening the reader first, returning null");
                    buffer = null;
                    return -1;

                }
                
                // 
                if (header.version != 1) {
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

                // return null and failure
                arrSamples = null;
                matValues = null;
                return -1;

            }

            // threadsafety
            lock (lockReader) {

                // 
                if (header.version != 1) {
                    arrSamples = null;
                    matValues = null;
                    return -1;
                }
                
                // initialize new arrays
                arrSamples = new uint[numRows];
                matValues = new double[numRows][];

                // loop through the rows
                for (int i = 0; i < numRows; i++) {

                    // store the samples
                    // TODO: can be done faster I think
                    matValues[i] = new double[header.numColumns - 1];
                    if (header.code == "src" || header.code == "dat") {

                        arrSamples[i] = BitConverter.ToUInt32(bOutput, i * header.rowSize);
                        Buffer.BlockCopy(bOutput, i * header.rowSize + sizeof(uint), matValues[i], 0, header.rowSize - sizeof(uint));

                    } else {
                        
                        arrSamples[i] = (uint)BitConverter.ToDouble(bOutput, i * header.rowSize);
                        Buffer.BlockCopy(bOutput, i * header.rowSize + sizeof(double), matValues[i], 0, header.rowSize - sizeof(double));
                    
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

            FileStream fileStream = null;
            try {

                // open file stream
                fileStream = new FileStream(fileName, FileMode.Open);

                // retrieve version number
                byte[] bVersion = new byte[sizeof(int)];
                fileStream.Read(bVersion, 0, sizeof(int));
                header.version = BitConverter.ToInt32(bVersion, 0);

                // check version
                if (header.version != 1 && header.version != 2 && header.version != 3)
                    throw new Exception("Unknown data version");

                // retrieve the code from the header
                byte[] bCode = new byte[3];
                fileStream.Read(bCode, 0, bCode.Length);
                header.code = Encoding.ASCII.GetString(bCode);
                
                // retrieve the epochs (V2 & V3)
                if (header.version == 2 || header.version == 3) {
                    byte[] bRunStartEpoch = new byte[8];
                    fileStream.Read(bRunStartEpoch, 0, bRunStartEpoch.Length);
                    header.runStartEpoch = BitConverter.ToInt64(bRunStartEpoch, 0);

                    byte[] bFileStartEpoch = new byte[8];
                    fileStream.Read(bFileStartEpoch, 0, bFileStartEpoch.Length);
                    header.fileStartEpoch = BitConverter.ToInt64(bFileStartEpoch, 0);
                }
    
                // retrieve whether source input time is included (only in source data-file & V3)
                if (header.code == "src" && header.version == 3) {
                    header.includesSourceInputTime = fileStream.ReadByte() == 1;
                }

                // retrieve the sample rate
                byte[] bSampleRate = new byte[8];
                fileStream.Read(bSampleRate, 0, bSampleRate.Length);
                header.sampleRate = BitConverter.ToDouble(bSampleRate, 0);

                // retrieve the number of playback input streams
                byte[] bNumPlaybackStreams = new byte[4];
                fileStream.Read(bNumPlaybackStreams, 0, bNumPlaybackStreams.Length);
                header.numPlaybackStreams = BitConverter.ToInt32(bNumPlaybackStreams, 0);

                // # streams + streams details (V2 & V3)
                if (header.version == 2 || header.version == 3) {
                    byte[] bNumStreams = new byte[4];
                    fileStream.Read(bNumStreams, 0, bNumStreams.Length);
                    header.numStreams = BitConverter.ToInt32(bNumStreams, 0);
                    for (int i = 0; i < header.numStreams; i++) {
                        byte[] bStreamDataType = new byte[1];
                        fileStream.Read(bStreamDataType, 0, bStreamDataType.Length);
                        header.streamDataTypes.Add(bStreamDataType[0]);
                        
                        byte[] bStreamSamplesPer = new byte[2];
                        fileStream.Read(bStreamSamplesPer, 0, bStreamSamplesPer.Length);
                        header.streamDataSamplesPerPackage.Add(BitConverter.ToUInt16(bStreamSamplesPer, 0));
                    }
                }

                // retrieve the number of columns
                byte[] bNumColumns = new byte[4];
                fileStream.Read(bNumColumns, 0, bNumColumns.Length);
                header.numColumns = BitConverter.ToInt32(bNumColumns, 0);
                
                // retrieve the size of the column names
                byte[] bColumnNamesSize = new byte[4];
                fileStream.Read(bColumnNamesSize, 0, bColumnNamesSize.Length);
                header.columnNamesSize = BitConverter.ToInt32(bColumnNamesSize, 0);

                // retrieve the column names from the header
                byte[] bColumnNames = new byte[header.columnNamesSize];
                fileStream.Read(bColumnNames, 0, header.columnNamesSize);
                header.columnNames = Encoding.ASCII.GetString(bColumnNames).Split('\t');



                //
                // calculate worker variables
                //

                // store the position where the data starts (= the current position of the pointer in the stream after reading the header)
                header.posDataStart = fileStream.Position;

                if (header.version == 1) {

                    // determine the rowsize based on the header code (src/dat or plugin file)
                    if (header.code == "src" || header.code == "dat") {
                        header.rowSize = sizeof(uint);                                  // sample id
                        header.rowSize += (header.numColumns - 1) * sizeof(double);     // data                        
                    } else {
                        header.rowSize = header.numColumns * sizeof(double);            // sampleid <double> + stream values 
                    }
                    
                    // determine the number of rows
                    // casting to integer will make numrows round down in case of incomplete rows
                    header.numRows = (fileStream.Length - header.posDataStart) / header.rowSize;
                
                } else if (header.version == 2 || header.version == 3) {

                    // determine the highest number of samples that any data stream in the pipeline would want to log
                    header.maxSamplesStream = 0;
                    for (int i = 0; i < header.numStreams; i++) {
                        if (header.streamDataSamplesPerPackage[i] > header.maxSamplesStream)
                            header.maxSamplesStream = header.streamDataSamplesPerPackage[i];
                    }

                }


                
            } catch (Exception) {

                // close the data stream
                if (fileStream != null)     fileStream.Close();

                // return failure
                return null;

            } finally {

                // close the data stream
                if (fileStream != null)     fileStream.Close();

            }

            return header;
        }



    }
}
