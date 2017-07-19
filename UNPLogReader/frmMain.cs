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

            txtInputFile.Text = "D:\\UNP\\other\\testrun\\test_20170718_run_1.dat";
            //txtInputFile.Text = "D:\\UNP\\other\\testrun\\test_20170720_Run_0.dat";

            
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

            if (!File.Exists(txtInputFile.Text)) {
                MessageBox.Show("Could not find input file", "File error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // create a data reader
            DataReader reader = new DataReader(txtInputFile.Text);

            // open the reader
            if (!reader.open()) {
                MessageBox.Show("Could not interpret input file '" + txtOutput.Text  + "'", "File error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // retrieve the header
            DataHeader header = reader.getHeader();

            // print header information
            txtOutput.Text = "Data file: " + txtInputFile.Text + Environment.NewLine;
            txtOutput.Text += "Internal extension: " + header.extension + Environment.NewLine;
            txtOutput.Text += "Number of pipeline input streams: " + header.pipelineInputStreams + Environment.NewLine;
            txtOutput.Text += "Number of columns: " + header.numColumns + Environment.NewLine;
            txtOutput.Text += "Column names size (in bytes): " + header.columnNamesSize + Environment.NewLine;
            txtOutput.Text += "Column names: " + string.Join(", ", header.columnNames) + Environment.NewLine;
            txtOutput.Text += "Row size (in bytes): " + header.rowSize + Environment.NewLine;
            txtOutput.Text += "Number of rows: " + header.numRows + Environment.NewLine;
            txtOutput.Text += "Data start position: " + header.posDataStart + Environment.NewLine;

            txtOutput.Text += "Data:" + Environment.NewLine + Environment.NewLine;
            txtOutput.Text += string.Join("\t", header.columnNames) + Environment.NewLine;

            // make sure the data pointer is at the start of the data
            reader.resetDataPointer();

            // loop until the end of the data
            while(!reader.reachedEnd()) {

                uint[] samples = null;
                double[][] values = null;

                // read the next rows
                reader.readNextRows(4, out samples, out values);

                // loop through the rows in set
                txtOutput.Text += Environment.NewLine;
                for (int i = 0; i < samples.Length; i++) {

                    string text = samples[i] + "\t";
                    text += string.Join("\t", values[i]);
                    text += Environment.NewLine;
                    txtOutput.Text += text;
                }
                
                /*
                // read the next rows
                byte[] rowData = reader.readNextRows(4);
                
                // determine the number of rows returned
                int rows = rowData.Length / header.rowSize;

                // loop through the rows in set
                txtOutput.Text += Environment.NewLine;
                for (int i = 0; i < rows; i++) {

                    uint sampleCounter = BitConverter.ToUInt32(rowData, i * header.rowSize);
                    double elapsedTime = BitConverter.ToDouble(rowData, i * header.rowSize + sizeof(uint));

                    // convert remainder bytes to double array
                    double[] values = new double[header.numColumns - 2];
                    Buffer.BlockCopy(rowData, i * header.rowSize + sizeof(uint) + sizeof(double), values, 0, header.rowSize - (sizeof(double) + sizeof(uint)));

                    string text = sampleCounter + "\t" + elapsedTime + "\t";
                    text += string.Join("\t", values);
                    text += Environment.NewLine;
                    txtOutput.Text += text;

                }
                */
                
                
            }

            /*
            uint[] samples = null;
            double[] values = null;

            reader.readNextRows(2, ref samples, ref values);
            if (samples == null || values == null) {
                // error

            } else {
                // successfull read

            }
            //txtOutput.Text
            */
        }
    }
}
