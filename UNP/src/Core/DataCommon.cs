﻿using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UNP.Core {
    public class DataCommon {

        private static Logger logger = LogManager.GetLogger("DataCommon");

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

                    // retrieve the extention from the header
                    byte[] bExtention = new byte[3];
                    dataStream.Read(bExtention, 0, 3);
                    header.extention = Encoding.ASCII.GetString(bExtention);    

                    // retrieve the fixed fields from the header    (note that the pointer has already been moved till after the version bytes)
                    int fixedBytesInHeader = 3 * sizeof(int);   // 3 x int     (- the first int, representing the version, which is already taken before here; - 3 bytes, representing extention, also taken before)
                    byte[] bFixedHeader = new byte[fixedBytesInHeader];
                    dataStream.Read(bFixedHeader, 0, fixedBytesInHeader);
                    
                    // interpret the information from the header's fixed fields
                    header.pipelineInputStreams     = BitConverter.ToInt32(bFixedHeader, 0);
                    header.numColumns               = BitConverter.ToInt32(bFixedHeader, 0 + sizeof(int));
                    header.columnNamesLength        = BitConverter.ToInt32(bFixedHeader, 0 + sizeof(int) * 2);

                    // retrieve the column names from the header
                    byte[] bColumnNames = new byte[header.columnNamesLength];
                    dataStream.Read(bColumnNames, 0, header.columnNamesLength);
                    header.columnNames = Encoding.ASCII.GetString(bColumnNames).Split('\t');


                    for (int i = 0; i < header.columnNames.Length; i++) {
                        logger.Error(header.columnNames[i]);
                    }

                    
                
                }



            } catch (IOException) {

                // close the data stream
                dataStream.Close();

                // return failure
                return null;

            } finally {

                // close the data stream
                dataStream.Close();

            }

            /*

            BinaryReader br = new BinaryReader(dataStream);

            // read the complete stream
            using (var ms = new MemoryStream()) {
                FileStream dataStream.CopyTo(ms);
                stream = ms.ToArray();
            }

            // get dedicated fields: amount of source channels (in case of .src file) and amount of columns in file 
            int sourceChannels = BitConverter.ToInt32(stream, 0);
            int amountStreams = BitConverter.ToInt32(stream, sizeof(int));

            // get header
            int headerLen = BitConverter.ToInt32(stream, 2 * sizeof(int));
            byte[] headerBinary = stream.Skip(3 * sizeof(int)).Take(headerLen).ToArray();           // TODO: Linq expression, change
            string header = Encoding.ASCII.GetString(headerBinary);
            */


            return header;
        }




    }
}
