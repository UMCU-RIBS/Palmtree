/**
 * SamplePackageFormat class
 * 
 * Used to define the format of the sample-packages that are being passed through the Palmtree framework
 * 
 * 
 * Copyright (C) 2017:  RIBS group (Nick Ramsey Lab), University Medical Center Utrecht (The Netherlands) & external contributors
 * Author(s):           Max van den Boom            (info@maxvandenboom.nl)
 *                      Benny van der Vijgh         (benny@vdvijgh.nl)
 * 
 * This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software
 * Foundation, either version 3 of the License, or (at your option) any later version. This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for
 * more details. You should have received a copy of the GNU General Public License along with this program. If not, see <https://www.gnu.org/licenses/>.
 */

namespace Palmtree.Core {

    /// <summary>
    /// SamplePackageFormat class.
    /// 
    /// ...
    /// </summary>
    public class SamplePackageFormat {
        
        public enum ValueOrder {
            ChannelMajor = 0,
            SampleMajor = 1
        }

        public int numChannels = 1;                                 // number of sampling-channels in each package
        public int numSamples = 1;                                  // theoretical number of samples in each package (might vary)
        public double packageRate = 5;                              // (estimated) rate at which packages are passed (in packages per second/hz)
        public ValueOrder valueOrder = ValueOrder.SampleMajor;      // if SampleMajor, then the input is ordered by sample first (sample elements contiguously in memory) and channels second (i.e. <smpl0 - ch0> - <smpl0 - ch1> ...)
                                                                    // if ChannelMajor, then the input is ordered by channel first (channel elements contiguously in memory) and samples second (i.e. <smpl0 - ch0> - <smpl1 - ch0> ...)
        //private int[] types = null;                               // 
        //private string[] channelNames;                            // Channel names in package description?

        public SamplePackageFormat(int channels, int samples, double packageRate, ValueOrder valueOrder) {
            this.numChannels = channels;
            this.numSamples = samples;
            this.packageRate = packageRate;
            this.valueOrder = valueOrder;
        }
        
    }

}
