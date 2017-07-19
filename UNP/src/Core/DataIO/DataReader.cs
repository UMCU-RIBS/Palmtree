using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UNP.Core.DataIO {

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

                // make sure the header is read
                // return false if the header could not be read
                if (getHeader() == null)    return false;

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
                if (dataStream != null)     dataStream.Close();

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

        public byte[] readNextRows(long numRows) {
            
            // threadsafety
            lock (lockReader) {

                // check if the reader has not been opened
                if (header == null || dataStream == null) {

                    // message
                    logger.Error("Trying to read rows without opening the reader first, returning null");

                    // return
                    return null;

                }
                
                // check if the reading the given number of rows exceeds the end of the data
                // correct the number of rows to the maximum available rows
                if (currentRowIndex + numRows > header.numRows)
                    numRows = header.numRows - currentRowIndex;

                // if there are no rows left to read, return 0
                if (numRows == 0)   return null;

                // calculate the number of bytes to read
                long numBytes = header.rowSize * numRows;

                // check if the number of bytes is bigger than int (since the filestream's read function only takes int as length)
                if (numBytes > Int32.MaxValue) {

                    // message
                    logger.Error("The function 'readNextRows' is asked to read too much data at once, ask for a smaller amount of rows per call");

                    // return
                    return null;

                }

                // check if the number of bytes to be read exceeds the file length
                if (dataStream.Position + numBytes > dataStream.Length) {

                    // message
                    logger.Error("The function 'readNextRows' tries to read beyond the length of the file, something wrong with determining the number of rows, check code");

                    // return
                    return null;

                }

                // read the data
                byte[] rowData = new byte[numBytes];
                if (dataStream.Read(rowData, 0, (int)(numBytes)) < 0) {

                    // message
                    logger.Error("The function 'readNextRows' tries to read beyond the length of the file, something wrong with determining the number of rows, check code");

                    // return
                    return null;

                }

                // move the current row index up the number of read rows
                currentRowIndex += numRows;
                
                // return the rowData as byte array
                return rowData;

            }

        }


        public void readNextRows(long numRows, out uint[] arrSamples, out double[][] matValues) {
            byte[] bOutput = readNextRows(numRows);

            // check if an error occured while reading
            if (bOutput == null) {

                // set output to null
                arrSamples = null;
                matValues = null;

                // return
                return;

            }

            // threadsafety
            lock (lockReader) {

                // determine the number of rows in the result (since the function could have capped it)
                int outputNumRows = bOutput.Length / header.rowSize;

                // create new arrays
                arrSamples = new uint[outputNumRows];
                matValues = new double[outputNumRows][];

                // loop through the rows
                for (int i = 0; i < outputNumRows; i++) {

                    // store the samples
                    arrSamples[i] = BitConverter.ToUInt32(bOutput, i * header.rowSize);

                    // store the values
                    matValues[i] = new double[header.numColumns - 1];
                    Buffer.BlockCopy(bOutput, i * header.rowSize + sizeof(uint), matValues[i], 0, header.rowSize - sizeof(uint));

                }

            }       // end lock

        }

        public bool reachedEnd() {
            return currentRowIndex >= header.numRows;
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

                    // retrieve the extension from the header
                    byte[] bExtension = new byte[3];
                    dataStream.Read(bExtension, 0, 3);
                    header.extension = Encoding.ASCII.GetString(bExtension);

                    // retrieve the fixed fields from the header    (note that the pointer has already been moved till after the version bytes)
                    int fixedBytesInHeader = 3 * sizeof(int);   // 3 x int     (- the first int, representing the version, which is already taken before here; - 3 bytes, representing extension, also taken before)
                    byte[] bFixedHeader = new byte[fixedBytesInHeader];
                    dataStream.Read(bFixedHeader, 0, fixedBytesInHeader);

                    // interpret the information from the header's fixed fields
                    header.pipelineInputStreams = BitConverter.ToInt32(bFixedHeader, 0);
                    header.numColumns = BitConverter.ToInt32(bFixedHeader, 0 + sizeof(int));
                    header.columnNamesSize = BitConverter.ToInt32(bFixedHeader, 0 + sizeof(int) * 2);

                    // retrieve the column names from the header
                    byte[] bColumnNames = new byte[header.columnNamesSize];
                    dataStream.Read(bColumnNames, 0, header.columnNamesSize);
                    header.columnNames = Encoding.ASCII.GetString(bColumnNames).Split('\t');

                    // determine the size of one row (in bytes)
                    header.rowSize = sizeof(uint);                                    // sample id
                    header.rowSize += (header.numColumns - 1) * sizeof(double);      // data

                    // store the position where the data starts (= the current position of the pointer in the stream after reading the header)
                    header.posDataStart = dataStream.Position;

                    // determine the number of rows
                    header.numRows = (dataStream.Length - header.posDataStart) / header.rowSize;

                }

            } catch (Exception) {

                // close the data stream
                dataStream.Close();

                // return failure
                return null;

            } finally {

                // close the data stream
                dataStream.Close();

            }

            return header;
        }



    }
}
