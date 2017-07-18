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

                    // retrieve the fixed fields from the header    (note that the pointer has already been moved till after the version bytes)
                    int fixedBytesInHeader = sizeof(int) * 3;
                    byte[] bFixedHeader = new byte[fixedBytesInHeader];
                    dataStream.Read(bFixedHeader, 0, fixedBytesInHeader);

                    // interpret the information from the header's fixed fields
                    header.pipelineInputStreams     = BitConverter.ToInt32(bFixedHeader, 0);
                    header.numColumns               = BitConverter.ToInt32(bFixedHeader, sizeof(int));
                    header.columnNamesLength        = BitConverter.ToInt32(bFixedHeader, sizeof(int) * 2);

                    // retrieve the column numes from the header (variable length)
                    byte[] bColumnNames = new byte[header.columnNamesLength];
                    dataStream.Read(bColumnNames, 0, header.columnNamesLength);


                    string strheader = Encoding.ASCII.GetString(bColumnNames);
                    // try to split up the string
                    logger.Warn(strheader);
                    string[] strHeaders = strheader.Split('\t');
                    for (int i = 0; i < strHeaders.Length; i++) {
                        logger.Error(strHeaders[i]);
                    }

                    
                    //byte[] bColumnNames = stream.Skip(3 * sizeof(int)).Take(headerLen).ToArray();           // TODO: Linq expression, change
                    //string header = Encoding.ASCII.GetString(headerBinary);

                    
                
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
