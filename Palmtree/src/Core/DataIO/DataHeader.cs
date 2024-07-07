/**
 * The DataHeader class
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
using System.Collections.Generic;

namespace Palmtree.Core.DataIO {

    /// <summary>
    /// The <c>DataHeader</c> class.
    /// 
    /// ...
    /// </summary>
    public class DataHeader {

        public long fileSize                            = 0;                    // the size of the entire data file
        public int version                              = 0;                    // data format version
        public string code                              = "";                   // data code (src, dat or other)

        public double sampleRate                        = 0;                    // the sample rate (in Hz); in source-data this is the rate (in Hz) at which the source provides sample measurements
                                                                                //                          in pipeline-data this is the total number of samples in Hz (over packages) that are expected to come into the pipeline
        public int numPlaybackStreams                   = 0;                    // number of playback input streams         (needs to be > 0 for playback)
        public int numStreams                           = 0;                    // total number of streams per sample       (used in version 1 as a worker variable, in version 2+ a data variable)

        public int numColumns                           = 0;                    // total number of columns per sample       (this includes the sample/package ID and time elapsed columns)
        public int columnNamesSize                      = 0;                    // the size (in bytes) of all the column names stored in the header
        public string[] columnNames                     = new string[0];        // all column names                         (this includes the sample/package ID and time elapsed columns)
        
        // version 2 variables
        public long runStartEpoch                       = 0;                    // the start epoch of the run data-set      (only used in version 2+)
        public long fileStartEpoch                      = 0;                    // the start epoch of the run data-set      (only used in version 2+)
        public List<byte> streamDataTypes               = new List<byte>(0);    // store, for each stream, the data type    (currently not used, but stored anyway; 0 = double)
        public List<ushort> streamDataSamplesPerPackage = new List<ushort>(0);  // store, for each stream, the number of samples per call/package  (not used in source/.src data, the number of samples is allowed to vary per package; is used in pipeline/.dat data)

        // version 3 variable - source data-file only
        public bool includesSourceInputTime = false;                            // whether a source input timestamp is included in the source-data file



        //
        // extra variables, induced/calculated by reading (or applying) metadata
        //

        // V1
        public long posDataStart                        = 0;                // position (counted in bytes) to the start of the data in the file
        public int rowSize                              = 0;                // the size (in bytes) of one row
        public long numRows                             = 0;                // total number of complete rows

        // V2 & V3
        public long totalSamples                        = 0;                // the total number of samples
        public long totalPackages                       = 0;                // the total number of sample-packages
        public ushort expectedMaxSamplesStream          = 0;                // the expected highest number of samples that any stream logged, based on the streams information in the general header
        public List<ushort> packagesMaxSamplesStream    = null;             // for each package, the highest number of samples (if applicable also over sample-chunks)
        public ushort actualMaxSamplesStream            = 0;                // the actual highest number of samples that any stream in the pipeline logged, after reading all sample-packages/chunks
        public double averagePackageInterval            = 0;

    }

}
