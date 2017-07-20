using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using UNP.Core.DataIO;

namespace UNPLogReader {

    public partial class frmMain : Form {

        public frmMain() {
            InitializeComponent();

            txtInputFile.Text = "D:\\UNP\\other\\testrun\\test_20170720_Run_0.dat";

            
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

            // loop until the end of the data
            while(!reader.reachedEnd()) {
                
                uint[] samples = null;
                double[][] values = null;

                // read the next rows
                long rows = reader.readNextRows(4, out samples, out values);

                // check for error while reading, return if so
                if (rows == -1)     return;

                // loop through the rows in set
                strOutput += Environment.NewLine;
                for (long i = 0; i < rows; i++) {

                    string text = samples[i] + "\t";
                    text += string.Join("\t", values[i]);
                    text += Environment.NewLine;
                    strOutput += text;
                }
                


                /*
                byte[] rowData = null;

                // read the next rows
                long rows = reader.readNextRows(4, out rowData);
                
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



            }

            // update the output textbox
            txtOutput.Text = strOutput;

        }
    }
}
