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
using UNP.Core;

namespace UNPLogReader {

    public partial class frmMain : Form {

        public frmMain() {
            InitializeComponent();

            txtInputFile.Text = "D:\\UNP\\other\\testrun\\test_20170718_run_1.dat";
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

            // read the header
            DataHeader header = DataCommon.readHeader(txtInputFile.Text);
            if (header == null) {
                MessageBox.Show("Could not interpret input file", "File error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            txtOutput.Text = "Data file: " + txtInputFile.Text + Environment.NewLine;
            txtOutput.Text += "Internal extension: " + header.extension + Environment.NewLine;
            txtOutput.Text += "Number of pipeline input streams: " + header.pipelineInputStreams + Environment.NewLine;
            txtOutput.Text += "Number of columns: " + header.numColumns + Environment.NewLine;
            txtOutput.Text += "Column names length (in bytes): " + header.columnNamesLength + Environment.NewLine;
            txtOutput.Text += "Column names: " + string.Join(", ", header.columnNames) + Environment.NewLine;
            txtOutput.Text += "Data:" + Environment.NewLine + Environment.NewLine;



        }
    }
}
