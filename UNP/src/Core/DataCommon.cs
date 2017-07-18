using NLog;
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

                    // retrieve the extension from the header
                    byte[] bExtension = new byte[3];
                    dataStream.Read(bExtension, 0, 3);
                    header.extension = Encoding.ASCII.GetString(bExtension);    

                    // retrieve the fixed fields from the header    (note that the pointer has already been moved till after the version bytes)
                    int fixedBytesInHeader = 3 * sizeof(int);   // 3 x int     (- the first int, representing the version, which is already taken before here; - 3 bytes, representing extension, also taken before)
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
