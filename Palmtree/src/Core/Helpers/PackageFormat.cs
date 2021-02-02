/**
 * The PackageFormat class
 * 
 * ...
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

namespace UNP.Core.Helpers {

    /// <summary>
    /// The <c>PackageFormat</c> class.
    /// 
    /// Abc.
    /// </summary>
    public class PackageFormat {
        
        private int channels = 1;                       // number of channels in each package
        private int samples = 1;                        // number of samples in each package
        private double rate = 5;                        // the rate at which packages are passed (in packages per second/hz)
        //private int[] types = null;                     // 

        public PackageFormat(int channels, int samples, double rate) {
            this.channels = channels;
            this.samples = samples;
            this.rate = rate;
        }

        public int getNumberOfChannels() {
            return channels;
        }

        public int getSamples() {
            return samples;
        }

        public double getRate() {
            return rate;
        }

    }

}
