/**
 * The SignalHelper class
 * 
 * ...
 * 
 * 
 * Copyright (C) 2017:  RIBS group (Nick Ramsey Lab), University Medical Center Utrecht (The Netherlands) & external contributors
 * Author(s):           Benny van der Vijgh         (benny@vdvijgh.nl)
 * 
 * This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software
 * Foundation, either version 3 of the License, or (at your option) any later version. This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for
 * more details. You should have received a copy of the GNU General Public License along with this program. If not, see <https://www.gnu.org/licenses/>.
 */
using System;

namespace Palmtree.Core.Helpers {

    /// <summary>
    /// The <c>SignalHelper</c> class.
    /// 
    /// ...
    /// </summary>
    public class SignalHelper {

        // whether the test data is random or based on sinusoids
        public enum DebugSignalType : int {
            Rand,
            Sinus
        };            

        public static double[] generateTestData(double[] amp, double[] f, double fs, int N, DebugSignalType type) {

            double[] data = new double[N];

            if (type == DebugSignalType.Rand) {

                Random rand = new Random(Guid.NewGuid().GetHashCode());
                for (int i = 0; i < N; i++) { data[i] = rand.Next(1, 1000); }

            } else if (type == DebugSignalType.Sinus) {

                // init vars
                double samplingFrequency = fs;

                // cycle through all signals to be generated
                for (int w = 0; w < amp.Length; w++) {

                    // transfer variables
                    double amplitude = amp[w];
                    double frequency = f[w];

                    // allow test data to be cast to ushort
                    if (amplitude > ushort.MaxValue) amplitude = ushort.MaxValue;

                    // determine signal parameters: set signal frequency
                    double b = 1 / ((2 * Math.PI) * frequency);

                    // create signal, equal to length of data epochs filter expects
                    for (int i = 0; i < N; i++) {

                        // determine signal parameters: distance between datapoints is equal to sampling interval, ie 1 / samplingFrequency.
                        double x = i * (1 / samplingFrequency);

                        // create sine wave with frequency equal to given frequency and amplitude
                        data[i] += amplitude * Math.Sin(x / b);
                    }
                }
            }

            return data;
        }


    }

}
