/**
 * frmMain class
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
using System.Windows.Forms;
using Palmtree.Core.DataIO;
using Palmtree.Core.Helpers;

namespace PalmtreeLogReader {

    /// <summary>
    /// frmMain class
    /// 
    /// ...
    /// </summary>
    public partial class frmMain : Form {

        private long readStep = 100;

        public frmMain() {
            InitializeComponent();

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
            strOutput += "Header code: " + header.code + Environment.NewLine;
            if (header.version == 2 || header.version == 3) {
                strOutput += "Run start epoch: " + header.runStartEpoch + Environment.NewLine;
                strOutput += "Run file epoch: " + header.fileStartEpoch + Environment.NewLine;
            }
            if (header.code == "src" && header.version == 3) {
                strOutput += "Include source input time: " + header.includesSourceInputTime + Environment.NewLine;
            }
            strOutput += "Sample rate: " + header.sampleRate + Environment.NewLine;
            strOutput += "Number of playback input streams: " + header.numPlaybackStreams + Environment.NewLine;
            if (header.version == 2 || header.version == 3) {
                strOutput += "Number of streams: " + header.numStreams + Environment.NewLine;
                for (int i = 0; i < header.numStreams; i++) {
                    strOutput += "   " + i + "  - type: " + header.streamDataTypes[i] + "  - samplesPer: " + header.streamDataSamplesPerPackage[i] + Environment.NewLine;
                }
            }
            strOutput += "Number of columns: " + header.numColumns + Environment.NewLine;
            strOutput += "Column names size (in bytes): " + header.columnNamesSize + Environment.NewLine;
            strOutput += "Column names: " + string.Join(", ", header.columnNames) + Environment.NewLine;
            strOutput += "Data start position: " + header.posDataStart + Environment.NewLine;

            strOutput += "Row size (in bytes): " + header.rowSize + Environment.NewLine;
            strOutput += "Number of rows: " + header.numRows + Environment.NewLine;
            
            
            if (header.version == 1) {
                
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
                bool readMore = header.numRows > 0;
                long rowsReadCounter = 0;
                while(readMore) {

                    uint[] samples = null;
                    double[][] values = null;

                    // read the next rows
                    long rows = reader.readNextRows(readStep, out samples, out values);

                    // check for error while reading
                    if (rows == -1) {
                        MessageBox.Show("Error while reading rows");
                        return;
                    }

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


                    // highten the rows read counter with the amount of rows read
                    rowsReadCounter += rows;

                    // check if more should be read after this
                    readMore = ((rowsToRead == 0 && !reader.reachedEnd()) || (rowsToRead > 0 && rowsReadCounter < rowsToRead));

                }
                
            }

            // update the output textbox
            txtOutput.Text = strOutput;

            // close the reader
            reader.close();

        }

        private void btnReadBCI2000_Click(object sender, EventArgs e) {

            // clear the output
            txtOutput.Text = "";

            // try to read the file
            Data_BCI2000 info = new Data_BCI2000();
            double[][] samples = null;
            bool result = readBCI2000dat(txtInputFile.Text, out info, out samples);
            if (!result) return;

            // output
            txtOutput.Text = info.firstLine + Environment.NewLine + Environment.NewLine;
            txtOutput.Text += "Header length: " + info.headerLen + Environment.NewLine;
            txtOutput.Text += "Source channels: " + info.sourceCh + Environment.NewLine;
            txtOutput.Text += "State vector length: " + info.stateVectorLen + Environment.NewLine;
            txtOutput.Text += Environment.NewLine;
            //txtOutput.Text += "Header: " + Environment.NewLine + data.header;
            txtOutput.Text += "Sample rate: " + info.samplingRate + Environment.NewLine;
            txtOutput.Text += Environment.NewLine;
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

        private void btnConvertBCIToPalmtree_Click(object sender, EventArgs e) {

            // clear the output
            txtOutput.Text = "";

            // try to read the file
            Data_BCI2000 info = new Data_BCI2000();
            double[][] samples = null;
            bool result = readBCI2000dat(txtInputFile.Text, out info, out samples);

            // open file dialog to save dat file
            SaveFileDialog dlgSaveDatFile = new SaveFileDialog();
            dlgSaveDatFile.Filter = "Data files (*.dat)|*.dat|Data files (*.src)|*.src|All files (*.*)|*.*";
            dlgSaveDatFile.RestoreDirectory = true;            // restores current directory to the previously selected directory, potentially beneficial if other code relies on the currently set directory

            // check if ok has been clicked on the dialog
            if (dlgSaveDatFile.ShowDialog() != DialogResult.OK)     return;
            
            FileStream dataStream = null;

            try {

                // create filestream: create file if it does not exists, allow to write, do not share with other processes and use buffer of 8192 bytes (roughly 1000 samples)
                dataStream = new FileStream(dlgSaveDatFile.FileName, FileMode.Create, FileAccess.Write, FileShare.None, 8192);
                
            } catch (Exception exc) {

                MessageBox.Show("Unable to create data file at " + dlgSaveDatFile.FileName + " (" + exc.ToString() + ")");
                return;

            }

            // generate stream names for the header (bci2000 does not have these)
            string[] streamNames = new string[info.sourceCh];
            for (int i = 0; i < streamNames.Length; i++) streamNames[i] = "Ch" + (i + 1);

            // create header
            DataHeader header = new DataHeader();
            header.version = 1;
            header.code = "dat";
            header.columnNames = streamNames;
            header.numPlaybackStreams = streamNames.Length;
            header.sampleRate = info.samplingRate;

            // write header
            if (dataStream != null)
                DataWriter.writeBinaryHeader(dataStream, header);

            // write data
            uint dataSampleCounter = 0;
            byte[] dataElapsedTimeBinary = BitConverter.GetBytes((1000.0 / header.sampleRate));
            for (int i = 0; i < info.numSamples; i++) {

                // transform variables that will be stored in .dat to binary arrays (except for dataStreamValues array which is copied directly)
                byte[] dataSampleCounterBinary = BitConverter.GetBytes(dataSampleCounter);

                // transfer the values for output
                double[] dataStreamValues = new double[info.sourceCh];
                for (int j = 0; j < info.sourceCh; j++) {
                    dataStreamValues[j] = samples[i][j];
                }

                // create new array to hold all bytes
                int l1 = dataSampleCounterBinary.Length;
                int l2 = dataElapsedTimeBinary.Length;
                int l3 = dataStreamValues.Length * sizeof(double);
                byte[] streamOut = new byte[l1 + l2 + l3];

                // blockcopy all bytes to this array
                Buffer.BlockCopy(dataSampleCounterBinary, 0, streamOut, 0, l1);
                Buffer.BlockCopy(dataElapsedTimeBinary, 0, streamOut, l1, l2);
                Buffer.BlockCopy(dataStreamValues, 0, streamOut, l1 + l2, l3);

                // write data to file
                if (dataStream != null)
                    dataStream.Write(streamOut, 0, streamOut.Length);

                // increase sample counter
                dataSampleCounter++;
                
            }

            // set output text
            txtOutput.Text = "Done";

            // clear
            dataStream.Close();
            dataStream = null;

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
            if (pos == -1 || info.firstLine.Length < pos + fieldName.Length + fieldValueLength) {
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
            if (pos == -1 || info.firstLine.Length < pos + fieldName.Length + fieldValueLength) {
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
            //fieldValueLength = 3;
            pos = info.firstLine.IndexOf(fieldName);
            fieldValueLength = Math.Min(3, info.firstLine.Length - (pos + fieldName.Length));               // adjusted: fieldValueLength is variable: so it is either three, or in case the string is shorter, the remainder of the string: either 1 or 2 
            if (pos == -1 || info.firstLine.Length < pos + fieldName.Length + fieldValueLength) {
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

            // retrieve the sample rate from the header
            if (!getDoubleParameterFromHeader(info.header, "SamplingRate", out info.samplingRate)) {
                MessageBox.Show("Could not retrieve sample rate from header, aborting");
                info = null;
                samples = null;
                return false;
            }

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

        private bool getDoubleParameterFromHeader(string header, string searchText, out double value) {
            
            // try to find the field
            int pos = header.IndexOf(searchText);
            if (pos == -1 || header.Length < pos + searchText.Length + 1) {
                value = -1;
                return false;
            }
            pos += searchText.Length;

            // till the start of the value
            string strValue = header.Substring(pos);
            while (strValue.Length > 0 && (strValue.StartsWith(" ") || strValue.StartsWith("="))) {
                strValue = strValue.Substring(1);
            }

            // find the end of the value (space)
            int endPos = strValue.IndexOf(" ");
            if (pos == -1 || pos == 0) {
                value = -1;
                return false;
            }

            // extract the value
            strValue = strValue.Substring(0, endPos);

            // check if Hz is at the end
            if (strValue.EndsWith("hz", true, null)) {
                strValue = strValue.Substring(0, strValue.Length - 2);
            }

            // parse the value
            bool result = Double.TryParse(strValue, out value);
            if (!result) {
                MessageBox.Show("Could not retrieve parse value '" + strValue + "'");
                value = -1;
                return false;
            }
            
            // return success
            return true;

        }


    }

}
