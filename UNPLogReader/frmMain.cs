using System;
using System.IO;
using System.Windows.Forms;
using UNP.Core.DataIO;
using UNP.Core.Helpers;

namespace UNPLogReader {

    public partial class frmMain : Form {

        private long readStep = 100;

        public frmMain() {
            InitializeComponent();

            txtInputFile.Text = "D:\\UNP\\other\\testrun\\test_20170724_Run_0.dat";
            txtInputFile.Text = "C:\\Users\\abcdef\\Desktop\\UNP001_multiclicks_motor_power_20170621001\\UNP001_multiclicks_motor_power_20170621S001R01.dat";

            


        }

        private void btnBrowse_Click(object sender, EventArgs e) {

            // open file dialog to open dat file
            OpenFileDialog dlgLoadDatFile = new OpenFileDialog();

            // set initial directory
            string folder = txtInputFile.Text;
            bool tryFolder = true;
            while (tryFolder) {
                try {
                    FileAttributes attr = File.GetAttributes(folder);
                    if ((attr & FileAttributes.Directory) == FileAttributes.Directory)
                        tryFolder = false;
                    else {
                        int lastIndex = folder.LastIndexOf('\\');
                        if (lastIndex == -1) {
                            tryFolder = false;
                            folder = "";
                        } else
                            folder = folder.Substring(0, lastIndex);
                    }
                } catch (Exception) {
                    if (folder.Length > 0)  folder = folder.Substring(0, folder.Length - 1);
                    int lastIndex = folder.LastIndexOf('\\');
                    if (lastIndex == -1) {
                        tryFolder = false;
                        folder = "";
                    } else
                        folder = folder.Substring(0, lastIndex);
                }

            }
            if (string.IsNullOrEmpty(folder))   dlgLoadDatFile.InitialDirectory = Directory.GetCurrentDirectory();
            else                                dlgLoadDatFile.InitialDirectory = folder;
            
            //
            dlgLoadDatFile.Filter = "Data files (*.dat)|*.dat|Data files (*.src)|*.src|All files (*.*)|*.*";
            dlgLoadDatFile.RestoreDirectory = true;            // restores current directory to the previously selected directory, potentially beneficial if other code relies on the currently set directory

            // check if ok has been clicked on the dialog
            if (dlgLoadDatFile.ShowDialog() == DialogResult.OK) {

                txtInputFile.Text = dlgLoadDatFile.FileName;
                
            }

        }

        private void btnRead_Click(object sender, EventArgs e) {

            // clear the output
            txtOutput.Text = "";

            // output string buffer
            string strOutput = "";

            // check if the file exists
            if (!File.Exists(txtInputFile.Text)) {
                MessageBox.Show("Could not find input file", "File error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // create a data reader
            DataReader reader = new DataReader(txtInputFile.Text);

            // open the reader
            if (!reader.open()) {
                MessageBox.Show("Could not interpret input file '" + txtInputFile.Text  + "'", "File error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // retrieve the header
            DataHeader header = reader.getHeader();

            // print header information
            strOutput = "Data file: " + txtInputFile.Text + Environment.NewLine;
            strOutput += "Internal extension: " + header.extension + Environment.NewLine;
            strOutput += "Pipeline sample rate: " + header.pipelineSampleRate + Environment.NewLine;
            strOutput += "Number of pipeline input streams: " + header.pipelineInputStreams + Environment.NewLine;
            strOutput += "Number of columns: " + header.numColumns + Environment.NewLine;
            strOutput += "Column names size (in bytes): " + header.columnNamesSize + Environment.NewLine;
            strOutput += "Column names: " + string.Join(", ", header.columnNames) + Environment.NewLine;
            strOutput += "Row size (in bytes): " + header.rowSize + Environment.NewLine;
            strOutput += "Number of rows: " + header.numRows + Environment.NewLine;
            strOutput += "Data start position: " + header.posDataStart + Environment.NewLine;

            strOutput += "Data:" + Environment.NewLine + Environment.NewLine;
            strOutput += string.Join("\t", header.columnNames) + Environment.NewLine;

            // make sure the data pointer is at the start of the data
            reader.resetDataPointer();

            // variable to hold how many rows to read (0 = all)
            int rowsToRead = 0;

            // check if it is a big file
            if (header.numRows > 1000) {

                DialogResult result = MessageBox.Show("The file holds a large number of samples, would you like to read only the first 1000 rows ('Yes') instead of the whole file('No')?", "Large file", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

                // check cancel
                if (result == DialogResult.Cancel) {
                    reader.close();
                    return;
                }

                // check yes
                if (result == DialogResult.Yes) {
                    rowsToRead = 1000;
                }

            }

            // loop until the end of the data
            bool readMore = true;
            long rowsReadCounter = 0;
            while(readMore) {
                
                uint[] samples = null;
                double[][] values = null;

                // read the next rows
                long rows = reader.readNextRows(readStep, out samples, out values);

                // check for error while reading, return if so
                if (rows == -1)     return;

                // loop through the rows in set
                strOutput += Environment.NewLine;
                for (long i = 0; i < rows; i++) {
                    string text = samples[i] + "\t";
                    for (int j = 0; j < values[i].Length; j++) {
                        if (j == 0) {
                            text += DoubleConverter.ToExactString(values[i][j]);
                        } else {
                            text += "\t";
                            text += values[i][j];
                        }
                    }
                    text += Environment.NewLine;
                    strOutput += text;
                }



                /*
                byte[] rowData = null;

                // read the next rows
                long rows = reader.readNextRows(readStep, out rowData);
                
                // check for error while reading, return if so
                if (rows == -1) return;

                // loop through the rows in set
                strOutput += Environment.NewLine;
                for (int i = 0; i < rows; i++) {

                    uint sampleCounter = BitConverter.ToUInt32(rowData, i * header.rowSize);
                    double elapsedTime = BitConverter.ToDouble(rowData, i * header.rowSize + sizeof(uint));

                    // convert remainder bytes to double array
                    double[] values = new double[header.numColumns - 2];
                    Buffer.BlockCopy(rowData, i * header.rowSize + sizeof(uint) + sizeof(double), values, 0, header.rowSize - (sizeof(double) + sizeof(uint)));

                    string text = sampleCounter + "\t" + elapsedTime + "\t";
                    text += string.Join("\t", values);
                    text += Environment.NewLine;
                    strOutput += text;

                }
                */

                // highten the rows read counter with the amount of rows read
                rowsReadCounter += rows;

                // check if more should be read after this
                readMore = ((rowsToRead == 0 && !reader.reachedEnd()) || (rowsToRead > 0 && rowsReadCounter < rowsToRead));
                
            }

            // update the output textbox
            txtOutput.Text = strOutput;

            // close the reader
            reader.close();

        }

        private bool readBCI2000dat(string filename, out Data_BCI2000 info, out double[][] samples) {

            FileStream dataStream = null;
            StreamReader dataReader = null;

            // check if the file exists
            if (!File.Exists(filename)) {
                MessageBox.Show("Could not find input file", "File error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                info = null;
                samples = null;
                return false;
            }

            try {

                // open file stream
                dataStream = new FileStream(filename, FileMode.Open);

                // open reader
                //dataReader = new System.IO.StreamReader(filestream, System.Text.Encoding.UTF8, true, 128);
                dataReader = new System.IO.StreamReader(dataStream);

            } catch (Exception) {

                // message
                MessageBox.Show("Could not create filestream to data file '" + filename + "' for reading");
                info = null;
                samples = null;
                return false;

            }

            // create a new bci2000 data object
            info = new Data_BCI2000();

            // retrieve the first line
            info.firstLine = dataReader.ReadLine();

            // retrieve HeaderLen
            string fieldName = "HeaderLen=";
            int fieldValueLength = 6;
            int pos = info.firstLine.IndexOf(fieldName);
            if (pos == -1 || info.firstLine.Length < pos + fieldName.Length) {
                MessageBox.Show("Could not retrieve " + fieldName + ", aborting");
                info = null;
                samples = null;
                return false;
            }
            bool result = Int32.TryParse(info.firstLine.Substring(pos + fieldName.Length, fieldValueLength), out info.headerLen);
            if (!result) {
                MessageBox.Show("Could not retrieve " + fieldName + ", aborting");
                info = null;
                samples = null;
                return false;
            }
            
            // retrieve SourceCh
            fieldName = "SourceCh=";
            fieldValueLength = 2;
            pos = info.firstLine.IndexOf(fieldName);
            if (pos == -1 || info.firstLine.Length < pos + fieldName.Length) {
                MessageBox.Show("Could not retrieve " + fieldName + ", aborting");
                info = null;
                samples = null;
                return false;
            }
            result = Int32.TryParse(info.firstLine.Substring(pos + fieldName.Length, fieldValueLength), out info.sourceCh);
            if (!result) {
                MessageBox.Show("Could not retrieve " + fieldName + ", aborting");
                info = null;
                samples = null;
                return false;
            }
            
            // retrieve StatevectorLen
            fieldName = "StatevectorLen=";
            fieldValueLength = 3;
            pos = info.firstLine.IndexOf(fieldName);
            if (pos == -1 || info.firstLine.Length < pos + fieldName.Length) {
                MessageBox.Show("Could not retrieve " + fieldName + ", aborting");
                info = null;
                samples = null;
                return false;
            }
            result = Int32.TryParse(info.firstLine.Substring(pos + fieldName.Length, fieldValueLength), out info.stateVectorLen);
            if (!result) {
                MessageBox.Show("Could not retrieve " + fieldName + ", aborting");
                info = null;
                samples = null;
                return false;
            }
            
            // retrieve entire header
            char[] buffer = new char[info.headerLen];
            dataStream.Position = 0;
            dataReader.DiscardBufferedData();
            dataReader.ReadBlock(buffer, 0, info.headerLen);
            info.header = new string(buffer);
            
            // set the dataStream position to the start of the data (needed, readblock before messed it up)
            dataStream.Position = info.headerLen;

            // set 
            int mDataSize = 2;      // each sample in BCI2000 is stored as an int16 (2 bytes)

            // calculate the number of samples
            info.numSamples = (int)(dataStream.Length - info.headerLen) / (mDataSize * info.sourceCh + info.stateVectorLen);
            
            // init matrix for all samples values
            samples = new double[info.numSamples][];

            // retrieve the data
            for (int i = 0; i < info.numSamples; i++) {

                // init the row of samples
                samples[i] = new double[info.sourceCh];

                // read the samples
                byte[] channels = new byte[mDataSize * info.sourceCh];
                dataStream.Read(channels, 0, mDataSize * info.sourceCh);

                // convert each channel
                for (int j = 0; j < info.sourceCh; j++) {
                    samples[i][j] = BitConverter.ToInt16(channels, j * mDataSize);
                }

                // read the state vector data
                byte[] stateVector = new byte[info.stateVectorLen];
                dataStream.Read(stateVector, 0, info.stateVectorLen);

                // TODO: interpret state vector

            }

            // close the datastream
            if (dataStream != null) dataStream.Close();
            dataReader = null;
            dataStream = null;

            // return success
            return true;

        }

        private void btnReadBCI2000_Click(object sender, EventArgs e) {

            // clear the output
            txtOutput.Text = "";

            // try to read the file
            Data_BCI2000 info = new Data_BCI2000();
            double[][] samples = null;
            bool result = readBCI2000dat(txtInputFile.Text, out info, out samples);

            // output
            txtOutput.Text = info.firstLine + Environment.NewLine + Environment.NewLine;
            txtOutput.Text += "Header length: " + info.headerLen + Environment.NewLine;
            txtOutput.Text += "Source channels: " + info.sourceCh + Environment.NewLine;
            txtOutput.Text += "Source channels: " + info.stateVectorLen + Environment.NewLine;
            txtOutput.Text += Environment.NewLine;
            //txtOutput.Text += "Header: " + Environment.NewLine + data.header;
            txtOutput.Text += "Number of samples: " + info.numSamples + Environment.NewLine + Environment.NewLine;


            string strOutput = "";
            for (int i = 0; i < info.numSamples; i++) {
                for (int j = 0; j < info.sourceCh; j++) {
                    if (j != 0)
                        strOutput += "\t";
                    strOutput += samples[i][j];
                }
                strOutput += Environment.NewLine;
            }
            
            txtOutput.Text += strOutput;

            

        }

        private void btnConvertBCIToUNP_Click(object sender, EventArgs e) {

            // clear the output
            txtOutput.Text = "";

            // try to read the file
            Data_BCI2000 info = new Data_BCI2000();
            double[][] samples = null;
            bool result = readBCI2000dat(txtInputFile.Text, out info, out samples);


        }
    }

}
