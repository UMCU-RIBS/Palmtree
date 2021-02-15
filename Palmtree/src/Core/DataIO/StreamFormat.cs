/**
 * StreamFormat class
 * 
 * Used to define the streams that store the data
 * 
 * 
 * Copyright (C) 2021:  RIBS group (Nick Ramsey Lab), University Medical Center Utrecht (The Netherlands) & external contributors
 * Author(s):           Max van den Boom            (info@maxvandenboom.nl)
 * 
 * This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software
 * Foundation, either version 3 of the License, or (at your option) any later version. This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for
 * more details. You should have received a copy of the GNU General Public License along with this program. If not, see <https://www.gnu.org/licenses/>.
 */

namespace Palmtree.Core.DataIO {

    /// <summary>
    /// StreamFormat class.
    /// 
    /// ...
    /// </summary>
    public class StreamFormat {
        
        public int numSamples = 1;                                  // number of incoming samples in the stream
        public double rate = 5;                                     // estimated rate at which stream samples are passed (in stream-packages per second/hz)
        //private int types = null;                                 // 

        public StreamFormat(int samples, double rate) {
            this.numSamples = samples;
            this.rate = rate;
        }
        
    }

}
