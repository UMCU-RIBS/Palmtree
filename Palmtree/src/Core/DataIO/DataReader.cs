/**
 * DataReader class
 * 
 * ...
 * 
 * 
 * Copyright (C) 2024:  RIBS group (Nick Ramsey Lab), University Medical Center Utrecht (The Netherlands) & external contributors
 * Author(s):           Max van den Boom            (info@maxvandenboom.nl)
 *                      Benny van der Vijgh         (benny@vdvijgh.nl)
 * 
 * This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software
 * Foundation, either version 3 of the License, or (at your option) any later version. This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for
 * more details. You should have received a copy of the GNU General Public License along with this program. If not, see <https://www.gnu.org/licenses/>.
 */
using NLog;
using System;
using System.Collections.Generic;
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
        private int fileType = -1;
        private long currentReadIndex = 0;                              // the index of the row (V1) or package (V2+) that is next up for reading


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

            lock (lockReader) {

                // check if file does not exists
                if (!File.Exists(filename)) {
                    logger.Error("Could not open data file '" + filename + "' for reading");
                    return false;
                }

                // read the header
                DataHeader header = getHeader();
                if (header == null || string.IsNullOrEmpty(header.code))
                    return false;

                // open file stream
                try {
                    dataStream = new FileStream(filename, FileMode.Open);

                } catch (Exception) {
                    logger.Error("Could not create filestream to data file '" + filename + "' for reading");
                    return false;
                }

                // determine the file type
                fileType = 2; // 0 = source, 1 = pipeline, 2 = plugin
                if (header.code.Equals("src", StringComparison.OrdinalIgnoreCase))
                    fileType = 0;
                else if (header.code.Equals("dat", StringComparison.OrdinalIgnoreCase))
                    fileType = 1;

                // set the data pointer to the beginning
                resetDataPointer();

                // return success
                return true;

            }   // end lock
        }


        public void close() {

            lock (lockReader) {

                // if a datastream is open, close it
                if (dataStream != null) {
                    dataStream.Close();
                    dataStream = null;
                }

                // clear the header
                header = null;

                // reset the row index
                currentReadIndex = 0;

            }   // end lock
        }


        public void resetDataPointer() {

            lock (lockReader) {

                // check if the reader has not been opened
                if (header == null || dataStream == null) {
                    logger.Error("Trying to reset data pointer without opening the reader first");
                    return;

                }

                // set the pointer to the start of the data
                if (header.posDataStart < dataStream.Length)
                    dataStream.Position = header.posDataStart;

                // set the row index to the first row
                currentReadIndex = 0;

            }   // end lock
        }





        //
        //
        //


        /**
         * numPackages: number of packages to read
         * buffer: 
         */
        public long readNextPackages(long numPackages, out double[][] buffer, out double[] bufferElapsedStamps) {
            buffer = null;
            bufferElapsedStamps = null;

            lock (lockReader) {

                // check if the reader has not been opened
                if (header == null || dataStream == null) {
                    logger.Error("Trying to read packages without opening the reader first");
                    return -1;
                }
                
                // 
                if (header.version == 1) {
                    logger.Error("Cannot read packages in version 1");
                    return -1;
                }

                // check if the reading the given number of packages exceeds the end of the data
                // correct the number of packages to the maximum available packages
                if (currentReadIndex + numPackages > header.totalPackages)
                    numPackages = header.totalPackages - currentReadIndex;

                // if there are no packages left to read, return -1
                if (numPackages == 0)
                    return -1;


                // read the data
                buffer              = new double[numPackages][];
                bufferElapsedStamps = new double[numPackages];
                for (int i = 0; i < numPackages; i++) {

                    uint samplePackageId;
                    double sourceInputTime;
                    if (!readNextPackageData(out samplePackageId, out bufferElapsedStamps[i], out sourceInputTime, out buffer[i])) {
                        logger.Error("An error occurred while reading the next packages from the file");
                        return -1;
                    }

                    // move the read package counter up after successfully reading a package
                    currentReadIndex++;
                    
                }
                
                // return the number of packages read
                return numPackages;

            } // end lock
        }
        

        public long readNextRows_V1(long numRows, out byte[] buffer) {

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
                if (currentReadIndex + numRows > header.numRows)
                    numRows = header.numRows - currentReadIndex;

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
                    logger.Error("The 'readNextRows_V1' is asked to read too much data at once, ask for a smaller amount of rows per call");

                    // return
                    buffer = null;
                    return -1;

                }

                // check if the number of bytes to be read exceeds the file length
                if (dataStream.Position + numBytes > dataStream.Length) {

                    // message
                    logger.Error("The 'readNextRows_V1' tries to read beyond the length of the file, something wrong with determining the number of rows, check code");

                    // return
                    buffer = null;
                    return -1;

                }

                // read the data
                buffer = new byte[numBytes];
                if (dataStream.Read(buffer, 0, (int)(numBytes)) < 0) {

                    // message
                    logger.Error("The 'readNextRows_V1' tries to read beyond the length of the file, something wrong with determining the number of rows, check code");

                    // return
                    buffer = null;
                    return -1;

                }

                // move the current row index up the number of read rows
                currentReadIndex += numRows;

                // return the number of rows read
                return numRows;

            } // end lock
        }
        
        public long readNextRows_V1(long numRows, out uint[] arrSamples, out double[][] matValues) {
            byte[] bOutput = null;

            // read the next rows
            numRows = readNextRows_V1(numRows, out bOutput);

            // check if an error occured while reading
            if (numRows == -1) {

                // return null and failure
                arrSamples = null;
                matValues = null;
                return -1;

            }

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

            lock (lockReader) {
                if (header.version == 2 || header.version == 3) {
                    return currentReadIndex >= header.totalPackages;
                } else if (header.version == 1) {
                    return currentReadIndex >= header.numRows;
                }
                return false;
            }

        }





        //
        // helper (non-static) functions
        //


        private bool readNextPackageData(out uint samplePackageId, out double elapsed, out double sourceInputTime, out double[] data) {
            
            // default returns on error
            samplePackageId = 0;
            elapsed         = -1;
            sourceInputTime = -1;

            // determine whether source-input-timestamps are included
            bool includesInputTime = fileType == 0 && header.includesSourceInputTime;

            // allocate a data matrix (based on the header information, make
            // sure ['total_samples'] is determined for efficient allocation)
            data = new double[header.packagesMaxSamplesStream[(int)currentReadIndex] * header.numStreams];

            // linear pointer for the data
            long dataIndex = 0;

            // determine the package header size
            int packageHeaderSize;
            if (fileType == 0) {
                if (includesInputTime)
                    packageHeaderSize = 22;   // .src = SamplePackageID <uint32> + elapsed <double> + source-input-time <double> + #samples <uint16> = 22 bytes
                else
                    packageHeaderSize = 14;   // .src = SamplePackageID <uint32> + elapsed <double> + #samples <uint16> = 14 bytes
            } else if (fileType == 1)
                packageHeaderSize = 12;   // .dat = SamplePackageID <uint32> + elapsed <double> = 12 bytes
            else
                return false;

            // check if there is at least a sample-package header available (from the current position)
            if (header.fileSize < dataStream.Position + packageHeaderSize)
                return false;

            // read the sample-package id
            byte[] bSamplePackageId = new byte[sizeof(uint)];
            dataStream.Read(bSamplePackageId, 0, bSamplePackageId.Length);
            samplePackageId = BitConverter.ToUInt32(bSamplePackageId, 0);

            // read the elapsed
            byte[] bElapsed = new byte[sizeof(double)];
            dataStream.Read(bElapsed, 0, bElapsed.Length);
            elapsed = BitConverter.ToDouble(bElapsed, 0);

            // read the source input time
            if (includesInputTime) {
                byte[] bSourceInputTime = new byte[sizeof(double)];
                dataStream.Read(bSourceInputTime, 0, bSourceInputTime.Length);
                sourceInputTime = BitConverter.ToDouble(bSourceInputTime, 0);
            }

            //
            if (fileType == 1 && header.expectedMaxSamplesStream > 1) {
                // pipeline data where at least one of the streams has more than one single sample

                // variable to store the current stream that is being read in this package
                int streamIndex = 0;

                // loop for data-chunks as long as there are streams left for this sample-package and
                // another sample-chunk header is available (uint16 + uint16 = 4 bytes)
                while (streamIndex < header.numStreams && dataStream.Position + 4 <= header.fileSize) {

                    // retrieve the number of streams from the sample-chunk header
                    byte[] bChunkNumStreams = new byte[sizeof(ushort)];
                    dataStream.Read(bChunkNumStreams, 0, bChunkNumStreams.Length);
                    ushort chunkNumStreams = BitConverter.ToUInt16(bChunkNumStreams, 0);

                    // retrieve the number of samples from the sample-chunk header
                    byte[] bChunkNumSamples = new byte[sizeof(ushort)];
                    dataStream.Read(bChunkNumSamples, 0, bChunkNumSamples.Length);
                    ushort chunkNumSamples = BitConverter.ToUInt16(bChunkNumSamples, 0);

                    // calculate the number of expected values
                    int chunkNumValues = chunkNumStreams * chunkNumSamples;

                    // check if all the sample-values are there
                    if (dataStream.Position + (chunkNumValues * 8) <= header.fileSize) {

                        // TODO: improve performance, should be able to read directly to databuffer, instead of first byte array
                        byte[] bChunkData = new byte[chunkNumValues * sizeof(double)];
                        dataStream.Read(bChunkData, 0, bChunkData.Length);
                        for (int i = 0; i < chunkNumValues; i++) {
                            data[dataIndex] = BitConverter.ToDouble(bChunkData, i * 8);
                            dataIndex++;
                        }

                    } else {
                        logger.Error("Trying to read partial data");
                        return false;
                    }

                    // add to the number of streams
                    streamIndex += chunkNumStreams;

                }

                // check if the expected number of streams were found
                if (streamIndex != header.numStreams) {
                    logger.Error("Trying to read partial data");
                    return false;
                }

            } else {
                // source data, or pipeline data where each of the streams has just one single sample

                ushort packageNumSamples;
                int packageNumValues;
                    
                // read the rest of the sample-package
                if (fileType == 0) { 
                    // source

                    // retrieve the number of samples from the sample-package header
                    byte[] bPackageNumSamples = new byte[sizeof(ushort)];
                    dataStream.Read(bPackageNumSamples, 0, bPackageNumSamples.Length);
                    packageNumSamples = BitConverter.ToUInt16(bPackageNumSamples, 0);

                    // calculate the number of expected values
                    packageNumValues = header.numStreams * packageNumSamples;

                } else {
                    // pipeline data, each of the streams has just one single sample

                    // set to one sample per package and set the number of values to be read to exactly the number of streams
                    packageNumSamples = 1;
                    packageNumValues = header.numStreams;

                }

                // check if all the sample-values are there
                if (dataStream.Position + (packageNumValues * 8) <= header.fileSize) {

                    // count the samples and packages
                    header.totalSamples += packageNumSamples;
                    header.totalPackages += 1;

                    // TODO: improve performance, should be able to read directly to databuffer, instead of first byte array
                    byte[] bPackageData = new byte[packageNumValues * sizeof(double)];
                    dataStream.Read(bPackageData, 0, bPackageData.Length);
                    for (int i = 0; i < packageNumValues; i++) {
                        data[dataIndex] = BitConverter.ToDouble(bPackageData, i * 8);
                        dataIndex++;
                    }
                    
                } else {
                    logger.Error("Trying to read partial data");
                    return false;
                }

            }

            // return success
            return true;

        }  // end of readNextPackageData





















        //
        // general read file functions
        // TODO: move somewhere else
        //

        public static DataHeader readHeader(String fileName) {

            // create a new data header object
            DataHeader header = new DataHeader();

            FileStream fileStream = null;
            try {

                // open file stream
                fileStream = new FileStream(fileName, FileMode.Open);

                // retrieve the file size (in bytes)
                header.fileSize = fileStream.Length;

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
                header.includesSourceInputTime = false;
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

                // store the position where the data starts (= the current position of the pointer in the stream after reading the header)
                header.posDataStart = fileStream.Position;


                //
                // extra fields (worker variables) and reading the data
                //

                // determine the file type
                int fileType = 2; // 0 = source, 1 = pipeline, 2 = plugin
                if (header.code.Equals("src", StringComparison.OrdinalIgnoreCase))
                    fileType = 0;
                else if (header.code.Equals("dat", StringComparison.OrdinalIgnoreCase))
                    fileType = 1;

                //
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
                    
                    // determine the expected maximum number of samples that any stream logged
                    // based on the streams information in the general header
                    header.expectedMaxSamplesStream = 0;
                    for (int i = 0; i < header.numStreams; i++) {
                        if (header.streamDataSamplesPerPackage[i] > header.expectedMaxSamplesStream)
                            header.expectedMaxSamplesStream = header.streamDataSamplesPerPackage[i];
                    }

                    //
                    header.totalSamples = 0;
                    header.totalPackages = 0;
                    //header.packagesMaxSamplesStream = new List<ushort>();
                    header.actualMaxSamplesStream = 0;
                    header.averagePackageInterval = 0;

                    // read the headers of all the packages (and - if applicable - and sample-chunks)
                    if (readPackagesHeader(ref fileStream, ref header, fileType, true) == false) { 
                        // failure on read
                        return null;
                    }
                }

                
            } catch (Exception) {
                // return failure
                return null;

            } finally {

                // close the data stream
                if (fileStream != null)     fileStream.Close();

            }

            return header;
        }

        /**
         * 
         * getPackageDetails: store the details of each packages, 
         **/
        private static bool readPackagesHeader(ref FileStream fileStream, ref DataHeader header, int fileType, bool packageDetails) {

            // determine whether source-input-timestamps are included
            bool includes_input_time = fileType == 0 && header.includesSourceInputTime;

            // set the read cursor at the start of the data
            fileStream.Seek(header.posDataStart, SeekOrigin.Begin);

            // determine the package header size
            int packageHeaderSize;
            if (fileType == 0) {
                if (includes_input_time)
                    packageHeaderSize = 22;   // .src = SamplePackageID <uint32> + elapsed <double> + source-input-time <double> + #samples <uint16> = 22 bytes
                else
                    packageHeaderSize = 14;   // .src = SamplePackageID <uint32> + elapsed <double> + #samples <uint16> = 14 bytes
            } else if (fileType == 1)
                packageHeaderSize = 12;   // .dat = SamplePackageID <uint32> + elapsed <double> = 12 bytes
            else {
                logger.Error("Could not determine package header size, not reading data");
                return false;
            }

            // 
            if (packageDetails)
                header.packagesMaxSamplesStream = new List<ushort>();

            // variables to calculate differences between packages
            // Note: setting the first sample-package id to -2 will cause the first package to be skipped
            long prevSamplePackageId = -2;
            double prevElapsed = 0;
            double averageElapsedTotal = 0;
            uint averageElapsedPackages = 0;

            // loop as long as there another sample-package header is available
            while (fileStream.Position + packageHeaderSize <= header.fileSize) {

                // read the sample-package header
                byte[] bSamplePackageId = new byte[sizeof(uint)];
                fileStream.Read(bSamplePackageId, 0, bSamplePackageId.Length);
                uint samplePackageId = BitConverter.ToUInt32(bSamplePackageId, 0);

                byte[] bElapsed = new byte[sizeof(double)];
                fileStream.Read(bElapsed, 0, bElapsed.Length);
                double elapsed = BitConverter.ToDouble(bElapsed, 0);

                // skip the source input time
                if (includes_input_time)
                    fileStream.Seek(8, SeekOrigin.Current);

                // if subsequent package, add elapsed to average
                if (samplePackageId - 1 == prevSamplePackageId) {
                    averageElapsedTotal += elapsed - prevElapsed;
                    averageElapsedPackages++;
                }

                //
                if (fileType == 1 && header.expectedMaxSamplesStream > 1) {
                    // pipeline data where at least one of the streams has more than one single sample

                    // variable to store the current stream that is being read in this package
                    int streamIndex = 0;

                    // create a list to store the number of samples in each sample-chunk for this package
                    List<ushort> packageChunksNumSamples = new List<ushort>();

                    // loop for data-chunks as long as there are streams left for this sample-package and
                    // another sample-chunk header is available (uint16 + uint16 = 4 bytes)
                    while (streamIndex < header.numStreams && fileStream.Position + 4 <= header.fileSize) {

                        // retrieve the number of streams from the sample-chunk header
                        byte[] bChunkNumStreams = new byte[sizeof(ushort)];
                        fileStream.Read(bChunkNumStreams, 0, bChunkNumStreams.Length);
                        ushort chunkNumStreams = BitConverter.ToUInt16(bChunkNumStreams, 0);

                        // retrieve the number of samples from the sample-chunk header
                        byte[] bChunkNumSamples = new byte[sizeof(ushort)];
                        fileStream.Read(bChunkNumSamples, 0, bChunkNumSamples.Length);
                        ushort chunkNumSamples = BitConverter.ToUInt16(bChunkNumSamples, 0);

                        // calculate the number of expected values
                        int chunkNumValues = chunkNumStreams * chunkNumSamples;

                        // check if all the sample-values are there
                        if (fileStream.Position + (chunkNumValues * 8) <= header.fileSize) {

                            // move the read cursor (skip data)
                            fileStream.Seek((chunkNumValues * 8), SeekOrigin.Current);

                            // store the number of samples in this sample-chunk for this package
                            packageChunksNumSamples.Add(chunkNumSamples);

                        } else {
                            logger.Warn("Not all values in the last sample-chunk are written, discarding last sample-chunk and therefore sample-package. Stop reading.");
                            if (averageElapsedPackages > 0)     header.averagePackageInterval = averageElapsedTotal / averageElapsedPackages;
                            return true;     // Leave prematurely. However, consider a partial read as successful, warning is enough
                        }

                        // add to the number of streams
                        streamIndex += chunkNumStreams;

                    }

                    // for this package, determine the highest number of samples of all data-chunks
                    ushort packageChunksMaxSamples = 0;
                    for (int i = 0; i < packageChunksNumSamples.Count; i++) {
                        if (packageChunksNumSamples[i] > packageChunksMaxSamples)
                            packageChunksMaxSamples = packageChunksNumSamples[i];
                    }

                    // check if the expected number of streams were found
                    if (streamIndex == header.numStreams) {

                        // count the samples
                        // Note: use the highest number of samples of all data-chunks. Data-chunks are allowed to differ
                        //       in size (i.e. streams are able to log less or more samples than other streams). However, to
                        //       allocate a matrix that would fix all values we need to consider the stream with the largest
                        //       number of samples
                        header.totalSamples += packageChunksMaxSamples;

                        // count the packages
                        header.totalPackages += 1;

                        // store the maximum number of samples over all sample-chunks (for this package)
                        if (packageDetails)
                            header.packagesMaxSamplesStream.Add(packageChunksMaxSamples);

                        // store the highest, either the maximum number of samples over all sample-chunks (for this package) or the existing maximum
                        if (packageChunksMaxSamples > header.actualMaxSamplesStream)
                            header.actualMaxSamplesStream = packageChunksMaxSamples;

                    } else {
                        logger.Warn("Not all streams in the last sample-package are written, discarding last sample-package. Stop reading.");
                        if (averageElapsedPackages > 0)     header.averagePackageInterval = averageElapsedTotal / averageElapsedPackages;
                        return true;    // Leave prematurely. However, consider a partial read as successful, warning is enough
                    }

                } else {
                    // source data, or
                    // pipeline data where each of the streams has just one single sample

                    ushort packageNumSamples;
                    int packageNumValues;
                    
                    // read the rest of the sample-package
                    if (fileType == 0) { 
                        // source

                        // retrieve the number of samples from the sample-package header
                        byte[] bPackageNumSamples = new byte[sizeof(ushort)];
                        fileStream.Read(bPackageNumSamples, 0, bPackageNumSamples.Length);
                        packageNumSamples = BitConverter.ToUInt16(bPackageNumSamples, 0);

                        // calculate the number of expected values
                        packageNumValues = header.numStreams * packageNumSamples;

                    } else {
                        // pipeline data, each of the streams has just one single sample

                        // set to one sample per package and set the number of values to be read to exactly the number of streams
                        packageNumSamples = 1;
                        packageNumValues = header.numStreams;

                    }

                    // check if all the sample-values are there
                    if (fileStream.Position + (packageNumValues * 8) <= header.fileSize) {

                        // store the number of samples (for this package)
                        if (packageDetails)
                            header.packagesMaxSamplesStream.Add(packageNumSamples);

                        // store the highest, either the number of samples (for this package) or the existing maximum
                        if (packageNumSamples > header.actualMaxSamplesStream)
                            header.actualMaxSamplesStream = packageNumSamples;

                        // count the samples and packages
                        header.totalSamples += packageNumSamples;
                        header.totalPackages += 1;

                        // move the read cursor
                        fileStream.Seek((packageNumValues * 8), SeekOrigin.Current);

                    } else {
                        logger.Warn("Not all values in the last sample-package are written, discarding last sample-package. Stop reading.");
                        if (averageElapsedPackages > 0)     header.averagePackageInterval = averageElapsedTotal / averageElapsedPackages;
                        return true;    // Leave prematurely. However, consider a partial read as successful, warning is enough
                    }

                }

                // update the current to become the previous
                prevSamplePackageId = samplePackageId;
                prevElapsed = elapsed;
            
            }   // end of sample-package loop

            // return success
            if (averageElapsedPackages > 0)     header.averagePackageInterval = averageElapsedTotal / averageElapsedPackages;
            return true;

        }  // end of readPackagesHeader

    }
}
